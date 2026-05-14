using Unity.Burst;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Solver
{
    [BurstCompile]
    public static class BurstJointSolver
    {
        private const float epsilon_default = 1e-12f;
        private const float epsilon_normal = 0.000001f;

        /*
         * Applies the rotation constraint for the selected joint model.
         *
         * @param current_local_rotation Current local rotation before solving.
         * @param desired_local_rotation Desired local rotation from the IK direction.
         * @param joint_static Static joint model and constraint data.
         */
        public static quaternion ApplyJointRotationConstraint(
            quaternion current_local_rotation,
            quaternion desired_local_rotation,
            JointStaticData joint_static)
        {
            if (joint_static.model == JointModel.Hinge2D)
            {
                return SolveHinge2DRotation(
                    current_local_rotation,
                    desired_local_rotation,
                    joint_static);
            }

            if (joint_static.model == JointModel.BallSocket)
            {
                return SolveBallSocketRotation(
                    desired_local_rotation,
                    joint_static);
            }

            return desired_local_rotation;
        }

        /*
         * Solves a hinge rotation by clamping the primary axis around the hinge axis.
         *
         * @param current_local_rotation Current local rotation before solving.
         * @param desired_local_rotation Desired local rotation from the IK direction.
         * @param joint_static Static joint model and constraint data.
         */
        public static quaternion SolveHinge2DRotation(
            quaternion current_local_rotation,
            quaternion desired_local_rotation,
            JointStaticData joint_static)
        {
            Hinge2DJointData hinge_data = joint_static.constraint.hinge_2d;

            float3 hinge_axis = NormalizeSafe(
                hinge_data.hinge_axis_local,
                new float3(0f, 0f, 1f));

            float3 reference_direction = NormalizeSafe(
                ProjectOnPlane(hinge_data.reference_direction_local, hinge_axis),
                new float3(1f, 0f, 0f));

            float3 desired_direction = math.mul(
                desired_local_rotation,
                joint_static.primary_axis_local);

            desired_direction = NormalizeSafe(
                ProjectOnPlane(desired_direction, hinge_axis),
                reference_direction);

            if (joint_static.max_delta_degrees > 0f)
            {
                float3 current_direction = math.mul(
                    current_local_rotation,
                    joint_static.primary_axis_local);

                current_direction = NormalizeSafe(
                    ProjectOnPlane(current_direction, hinge_axis),
                    reference_direction);

                desired_direction = LimitDirectionDelta(
                    current_direction,
                    desired_direction,
                    hinge_axis,
                    joint_static.max_delta_degrees);
            }

            if (hinge_data.use_limits != 0)
            {
                desired_direction = ClampSignedAngleFromReference(
                    reference_direction,
                    desired_direction,
                    hinge_axis,
                    hinge_data.min_angle_degrees,
                    hinge_data.max_angle_degrees);
            }

            quaternion constrained_rotation = CreateRotationFromPrimaryAxis(
                joint_static.primary_axis_local,
                desired_direction,
                hinge_axis,
                desired_local_rotation);

            return constrained_rotation;
        }

        /*
         * Solves a ball socket rotation by clamping swing and twist.
         *
         * @param desired_local_rotation Desired local rotation from the IK direction.
         * @param joint_static Static joint model and constraint data.
         */
        public static quaternion SolveBallSocketRotation(
            quaternion desired_local_rotation,
            JointStaticData joint_static)
        {
            BallSocketJointData ball_socket_data = joint_static.constraint.ball_socket;

            float3 swing_axis = NormalizeSafe(
                ball_socket_data.swing_axis_local,
                new float3(1f, 0f, 0f));

            quaternion swing_rotation;
            quaternion twist_rotation;

            DecomposeSwingTwist(
                desired_local_rotation,
                swing_axis,
                out swing_rotation,
                out twist_rotation);

            if (ball_socket_data.use_swing_limit != 0)
            {
                swing_rotation = ClampSwing(
                    swing_rotation,
                    ball_socket_data.reference_direction_local,
                    ball_socket_data.max_swing_degrees);
            }

            if (ball_socket_data.use_twist_limit != 0)
            {
                twist_rotation = ClampTwist(
                    twist_rotation,
                    swing_axis,
                    ball_socket_data.min_twist_degrees,
                    ball_socket_data.max_twist_degrees);
            }

            return math.mul(swing_rotation, twist_rotation);
        }

        /*
         * Solves a skeletal length constraint between two points.
         *
         * @param point_a First point.
         * @param point_b Second point.
         * @param rest_length Desired distance between points.
         * @param weight Share of correction applied to point a.
         * @param tolerance Accepted length error.
         * @param fallback_direction Direction used when the segment is too small.
         */
        public static void SolveSkeletalLength(
            ref float3 point_a,
            ref float3 point_b,
            float rest_length,
            float weight,
            float tolerance,
            float3 fallback_direction)
        {
            float3 delta = point_b - point_a;
            float distance = math.length(delta);
            float3 direction = NormalizeSafe(delta, fallback_direction);

            float error = distance - rest_length;

            if (math.abs(error) <= tolerance)
            {
                return;
            }

            float parent_share = math.clamp(weight, 0f, 1f);
            float child_share = 1f - parent_share;

            float3 correction = direction * error;

            point_a += correction * parent_share;
            point_b -= correction * child_share;
        }

        /*
         * Creates a rotation that points the primary local axis toward a constrained direction.
         *
         * @param primary_axis_local Local axis being aimed.
         * @param desired_direction_local Desired local direction.
         * @param secondary_axis_local Secondary axis used to stabilize roll.
         * @param fallback_rotation Fallback rotation.
         */
        private static quaternion CreateRotationFromPrimaryAxis(
            float3 primary_axis_local,
            float3 desired_direction_local,
            float3 secondary_axis_local,
            quaternion fallback_rotation)
        {
            primary_axis_local = NormalizeSafe(primary_axis_local, new float3(1f, 0f, 0f));
            desired_direction_local = NormalizeSafe(desired_direction_local, primary_axis_local);

            quaternion aim_delta = FromToRotationSafe(
                primary_axis_local,
                desired_direction_local);

            if (!IsFiniteQuaternion(aim_delta))
            {
                return fallback_rotation;
            }

            return aim_delta;
        }

        /*
         * Decomposes a rotation into swing and twist around a local axis.
         *
         * @param rotation Input rotation.
         * @param twist_axis Local axis used for twist.
         * @param swing_rotation Output swing rotation.
         * @param twist_rotation Output twist rotation.
         */
        private static void DecomposeSwingTwist(
            quaternion rotation,
            float3 twist_axis,
            out quaternion swing_rotation,
            out quaternion twist_rotation)
        {
            twist_axis = NormalizeSafe(twist_axis, new float3(1f, 0f, 0f));

            float4 value = rotation.value;
            float3 vector_part = new float3(value.x, value.y, value.z);
            float3 projected = twist_axis * math.dot(vector_part, twist_axis);

            twist_rotation = new quaternion(
                projected.x,
                projected.y,
                projected.z,
                value.w);

            twist_rotation = NormalizeSafe(twist_rotation);

            swing_rotation = math.mul(
                rotation,
                math.inverse(twist_rotation));

            swing_rotation = NormalizeSafe(swing_rotation);
        }

        /*
         * Clamps swing rotation to a maximum angular cone.
         *
         * @param swing_rotation Swing rotation.
         * @param reference_direction_local Reference direction for stable fallback.
         * @param max_swing_degrees Maximum swing angle.
         */
        private static quaternion ClampSwing(
            quaternion swing_rotation,
            float3 reference_direction_local,
            float max_swing_degrees)
        {
            float angle_radians;
            float3 axis;

            ToAxisAngleSafe(
                swing_rotation,
                reference_direction_local,
                out axis,
                out angle_radians);

            float max_radians = math.radians(max_swing_degrees);

            if (angle_radians <= max_radians)
            {
                return swing_rotation;
            }

            return quaternion.AxisAngle(axis, max_radians);
        }

        /*
         * Clamps twist rotation to a signed angular range.
         *
         * @param twist_rotation Twist rotation.
         * @param twist_axis Local twist axis.
         * @param min_twist_degrees Minimum twist angle.
         * @param max_twist_degrees Maximum twist angle.
         */
        private static quaternion ClampTwist(
            quaternion twist_rotation,
            float3 twist_axis,
            float min_twist_degrees,
            float max_twist_degrees)
        {
            twist_axis = NormalizeSafe(twist_axis, new float3(1f, 0f, 0f));

            float angle_degrees = SignedTwistAngleDegrees(
                twist_rotation,
                twist_axis);

            float clamped_degrees = math.clamp(
                angle_degrees,
                min_twist_degrees,
                max_twist_degrees);

            return quaternion.AxisAngle(
                twist_axis,
                math.radians(clamped_degrees));
        }

        /*
         * Computes signed twist angle in degrees.
         *
         * @param twist_rotation Twist rotation.
         * @param twist_axis Local twist axis.
         */
        private static float SignedTwistAngleDegrees(
            quaternion twist_rotation,
            float3 twist_axis)
        {
            twist_rotation = NormalizeSafe(twist_rotation);
            twist_axis = NormalizeSafe(twist_axis, new float3(1f, 0f, 0f));

            float4 value = twist_rotation.value;
            float angle = 2f * math.atan2(
                math.length(new float3(value.x, value.y, value.z)),
                value.w);

            float3 vector_part = new float3(value.x, value.y, value.z);
            float sign = math.sign(math.dot(vector_part, twist_axis));

            if (sign == 0f)
            {
                return 0f;
            }

            return math.degrees(angle) * sign;
        }

        /*
         * Projects a vector onto a plane.
         *
         * @param value Input vector.
         * @param normal Plane normal.
         */
        private static float3 ProjectOnPlane(float3 value, float3 normal)
        {
            return value - normal * math.dot(value, normal);
        }

        /*
         * Normalizes a vector with a fallback direction.
         *
         * @param value Vector to normalize.
         * @param fallback Fallback direction.
         */
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float length = math.length(value);

            if (length <= epsilon_normal)
            {
                return fallback;
            }

            return value / length;
        }

        /*
         * Normalizes a quaternion with identity fallback.
         *
         * @param value Quaternion to normalize.
         */
        private static quaternion NormalizeSafe(quaternion value)
        {
            float length = math.length(value.value);

            if (length <= epsilon_normal)
            {
                return quaternion.identity;
            }

            return new quaternion(value.value / length);
        }

        /*
         * Limits angular movement between two directions.
         *
         * @param previous_direction Previous normalized direction.
         * @param proposed_direction Proposed normalized direction.
         * @param axis Rotation axis.
         * @param max_delta_degrees Maximum angular movement.
         */
        private static float3 LimitDirectionDelta(
            float3 previous_direction,
            float3 proposed_direction,
            float3 axis,
            float max_delta_degrees)
        {
            previous_direction = NormalizeSafe(previous_direction, new float3(1f, 0f, 0f));
            proposed_direction = NormalizeSafe(proposed_direction, previous_direction);
            axis = NormalizeSafe(axis, new float3(0f, 0f, 1f));

            float dot_value = math.clamp(
                math.dot(previous_direction, proposed_direction),
                -1f,
                1f);

            float angle = math.acos(dot_value);
            float max_angle = math.radians(max_delta_degrees);

            if (angle <= max_angle)
            {
                return proposed_direction;
            }

            if (angle <= epsilon_default)
            {
                return proposed_direction;
            }

            float amount = max_angle / angle;
            float3 limited_direction = math.lerp(
                previous_direction,
                proposed_direction,
                amount);

            return NormalizeSafe(limited_direction, previous_direction);
        }

        /*
         * Clamps a direction to a signed angular range from a reference direction.
         *
         * @param reference_direction Direction treated as zero degrees.
         * @param proposed_direction Direction to clamp.
         * @param axis Signed angle axis.
         * @param min_angle_degrees Minimum signed angle.
         * @param max_angle_degrees Maximum signed angle.
         */
        private static float3 ClampSignedAngleFromReference(
            float3 reference_direction,
            float3 proposed_direction,
            float3 axis,
            float min_angle_degrees,
            float max_angle_degrees)
        {
            reference_direction = NormalizeSafe(reference_direction, new float3(1f, 0f, 0f));
            proposed_direction = NormalizeSafe(proposed_direction, reference_direction);
            axis = NormalizeSafe(axis, new float3(0f, 0f, 1f));

            float signed_angle = SignedAngleDegrees(
                reference_direction,
                proposed_direction,
                axis);

            float clamped_angle = math.clamp(
                signed_angle,
                min_angle_degrees,
                max_angle_degrees);

            quaternion rotation = quaternion.AxisAngle(
                axis,
                math.radians(clamped_angle));

            return math.mul(rotation, reference_direction);
        }

        /*
         * Computes a signed angle in degrees.
         *
         * @param from_direction Start direction.
         * @param to_direction End direction.
         * @param axis Signed angle axis.
         */
        private static float SignedAngleDegrees(
            float3 from_direction,
            float3 to_direction,
            float3 axis)
        {
            from_direction = NormalizeSafe(from_direction, new float3(1f, 0f, 0f));
            to_direction = NormalizeSafe(to_direction, from_direction);
            axis = NormalizeSafe(axis, new float3(0f, 0f, 1f));

            float dot_value = math.clamp(
                math.dot(from_direction, to_direction),
                -1f,
                1f);

            float angle = math.degrees(math.acos(dot_value));
            float sign = math.sign(math.dot(axis, math.cross(from_direction, to_direction)));

            if (sign == 0f)
            {
                return 0f;
            }

            return angle * sign;
        }

        /*
         * Creates a quaternion rotating one direction to another.
         *
         * @param from_direction Starting direction.
         * @param to_direction Target direction.
         */
        private static quaternion FromToRotationSafe(
            float3 from_direction,
            float3 to_direction)
        {
            from_direction = NormalizeSafe(from_direction, new float3(1f, 0f, 0f));
            to_direction = NormalizeSafe(to_direction, from_direction);

            float dot_value = math.clamp(
                math.dot(from_direction, to_direction),
                -1f,
                1f);

            if (dot_value > 0.999999f)
            {
                return quaternion.identity;
            }

            float3 axis = math.cross(from_direction, to_direction);

            if (math.lengthsq(axis) <= epsilon_normal)
            {
                axis = FindPerpendicularAxis(from_direction);
            }

            axis = NormalizeSafe(axis, new float3(0f, 0f, 1f));

            float angle = math.acos(dot_value);

            return quaternion.AxisAngle(axis, angle);
        }

        /*
         * Finds a stable perpendicular axis.
         *
         * @param direction Direction to build from.
         */
        private static float3 FindPerpendicularAxis(float3 direction)
        {
            direction = NormalizeSafe(direction, new float3(1f, 0f, 0f));

            float3 axis = math.cross(direction, new float3(0f, 1f, 0f));

            if (math.lengthsq(axis) > epsilon_normal)
            {
                return NormalizeSafe(axis, new float3(0f, 0f, 1f));
            }

            axis = math.cross(direction, new float3(0f, 0f, 1f));

            return NormalizeSafe(axis, new float3(0f, 1f, 0f));
        }

        /*
         * Converts a quaternion to axis angle safely.
         *
         * @param rotation Input rotation.
         * @param fallback_axis Fallback axis.
         * @param axis Output axis.
         * @param angle_radians Output angle in radians.
         */
        private static void ToAxisAngleSafe(
            quaternion rotation,
            float3 fallback_axis,
            out float3 axis,
            out float angle_radians)
        {
            rotation = NormalizeSafe(rotation);

            float4 value = rotation.value;
            float vector_length = math.length(new float3(value.x, value.y, value.z));

            if (vector_length <= epsilon_normal)
            {
                axis = NormalizeSafe(fallback_axis, new float3(1f, 0f, 0f));
                angle_radians = 0f;
                return;
            }

            axis = new float3(value.x, value.y, value.z) / vector_length;
            angle_radians = 2f * math.atan2(vector_length, value.w);
        }

        /*
         * Returns true when the quaternion contains finite values.
         *
         * @param value Quaternion to test.
         */
        private static bool IsFiniteQuaternion(quaternion value)
        {
            return math.all(math.isfinite(value.value));
        }
    }
}
