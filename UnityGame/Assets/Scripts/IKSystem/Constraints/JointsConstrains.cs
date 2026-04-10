using IKSystem.Contracts;
using IKSystem.Data;
using UnityEngine;
using ConditionalAttribute = System.Diagnostics.ConditionalAttribute;

namespace IKSystem.Constraints
{
    public sealed class JointConstraints : IJointConstraints
    {
        private const float axis_epsilon = 1e-8f;
        private const float direction_epsilon = 1e-8f;

        public JointConstraintResult Constrain_Joint_Step(
            in IkJoint joint,
            ref IkJointConstraintState joint_state,
            ChainSolveDirection solve_direction,
            ChainAnchorMode anchor_mode,
            Vector3 parent_world_position,
            Vector3 joint_world_position,
            ref Vector3 proposed_joint_world_position,
            Vector3 child_world_position,
            float epsilon)
        {
            epsilon = Mathf.Max(1e-12f, epsilon);

            Debug_Assert_Finite(parent_world_position, "Parent world position was not finite.");
            Debug_Assert_Finite(joint_world_position, "Joint world position was not finite.");
            Debug_Assert_Finite(proposed_joint_world_position, "Proposed joint world position was not finite.");
            Debug_Assert_Finite(child_world_position, "Child world position was not finite.");

            if (!Is_Finite(parent_world_position) ||
                !Is_Finite(joint_world_position) ||
                !Is_Finite(proposed_joint_world_position) ||
                !Is_Finite(child_world_position))
            {
                return JointConstraintResult.InvalidInput;
            }

            JointModel joint_model = joint.Model;

            if (joint_model == JointModel.Free)
            {
                return JointConstraintResult.Skipped;
            }

            if (joint_model == JointModel.Hinge2D)
            {
                bool applied = Constrain_Hinge_2d(
                    in joint,
                    ref joint_state,
                    solve_direction,
                    anchor_mode,
                    parent_world_position,
                    joint_world_position,
                    ref proposed_joint_world_position,
                    child_world_position,
                    epsilon);

                return applied ? JointConstraintResult.Applied : JointConstraintResult.Skipped;
            }

            if (joint_model == JointModel.BallSocket)
            {
                bool applied = Constrain_Ball_Socket_Placeholder(
                    in joint,
                    ref joint_state,
                    solve_direction,
                    anchor_mode,
                    parent_world_position,
                    joint_world_position,
                    ref proposed_joint_world_position,
                    child_world_position,
                    epsilon);

                return applied ? JointConstraintResult.Applied : JointConstraintResult.Skipped;
            }

            Debug.LogWarning("Joint model was not recognized.");
            return JointConstraintResult.Skipped;
        }

        private static bool Constrain_Hinge_2d(
            in IkJoint joint,
            ref IkJointConstraintState joint_state,
            ChainSolveDirection solve_direction,
            ChainAnchorMode anchor_mode,
            Vector3 parent_world_position,
            Vector3 joint_world_position,
            ref Vector3 proposed_joint_world_position,
            Vector3 child_world_position,
            float epsilon)
        {
            Vector3 hinge_axis_world = Resolve_Hinge_Axis_World(in joint, in parent_world_position, in joint_world_position);
            hinge_axis_world = Safe_Normalize(hinge_axis_world, Vector3.forward, epsilon);

            Vector3 parent_to_child_vector = child_world_position - parent_world_position;
            Vector3 parent_to_joint_previous = joint_world_position - parent_world_position;
            Vector3 parent_to_joint_proposed = proposed_joint_world_position - parent_world_position;

            if (!Is_Finite(parent_to_child_vector) ||
                !Is_Finite(parent_to_joint_previous) ||
                !Is_Finite(parent_to_joint_proposed))
            {
                return false;
            }

            Vector3 parent_to_child_plane = Project_On_Plane(parent_to_child_vector, hinge_axis_world);
            Vector3 previous_joint_plane = Project_On_Plane(parent_to_joint_previous, hinge_axis_world);
            Vector3 proposed_joint_plane = Project_On_Plane(parent_to_joint_proposed, hinge_axis_world);

            float parent_to_joint_distance = parent_to_joint_proposed.magnitude;
            if (parent_to_joint_distance <= epsilon)
            {
                parent_to_joint_distance = parent_to_joint_previous.magnitude;
            }

            if (parent_to_joint_distance <= epsilon)
            {
                return false;
            }

            Vector3 reference_direction_plane = parent_to_child_plane;
            if (reference_direction_plane.sqrMagnitude <= direction_epsilon * direction_epsilon)
            {
                reference_direction_plane = previous_joint_plane;
            }
            if (reference_direction_plane.sqrMagnitude <= direction_epsilon * direction_epsilon)
            {
                reference_direction_plane = Vector3.right;
            }

            Vector3 proposed_direction_plane = proposed_joint_plane;
            if (proposed_direction_plane.sqrMagnitude <= direction_epsilon * direction_epsilon)
            {
                proposed_direction_plane = previous_joint_plane;
            }
            if (proposed_direction_plane.sqrMagnitude <= direction_epsilon * direction_epsilon)
            {
                proposed_direction_plane = reference_direction_plane;
            }

            reference_direction_plane = Safe_Normalize(reference_direction_plane, Vector3.right, epsilon);
            proposed_direction_plane = Safe_Normalize(proposed_direction_plane, reference_direction_plane, epsilon);

            float signed_rotation_angle = Signed_Angle_On_Axis(
                reference_direction_plane,
                proposed_direction_plane,
                hinge_axis_world);

            float absolute_rotation_angle = Mathf.Abs(signed_rotation_angle);

            Vector3 candidate_direction_a =
                Quaternion.AngleAxis(signed_rotation_angle, hinge_axis_world) * reference_direction_plane;

            Vector3 candidate_direction_b =
                Quaternion.AngleAxis(-signed_rotation_angle, hinge_axis_world) * reference_direction_plane;

            candidate_direction_a = Safe_Normalize(candidate_direction_a, reference_direction_plane, epsilon);
            candidate_direction_b = Safe_Normalize(candidate_direction_b, reference_direction_plane, epsilon);

            int branch_a = 1;
            int branch_b = 0;

            int chosen_branch = Choose_Branch(
                in joint,
                ref joint_state,
                branch_a,
                candidate_direction_a,
                branch_b,
                candidate_direction_b,
                reference_direction_plane,
                hinge_axis_world,
                absolute_rotation_angle);

            Vector3 chosen_direction =
                chosen_branch == branch_b ? candidate_direction_b : candidate_direction_a;

            Vector3 previous_direction_plane =
                Safe_Normalize(previous_joint_plane, chosen_direction, epsilon);

            float maximum_delta_degrees = joint.MaxDeltaDegrees;
            if (maximum_delta_degrees > 0f)
            {
                float desired_delta_degrees =
                    Vector3.Angle(previous_direction_plane, chosen_direction);

                if (desired_delta_degrees > maximum_delta_degrees &&
                    desired_delta_degrees > epsilon)
                {
                    float blend_factor = maximum_delta_degrees / desired_delta_degrees;
                    chosen_direction =
                        Vector3.Slerp(previous_direction_plane, chosen_direction, blend_factor);

                    chosen_direction =
                        Safe_Normalize(chosen_direction, previous_direction_plane, epsilon);
                }
            }

            Vector3 constrained_world_position =
                parent_world_position + chosen_direction * parent_to_joint_distance;

            Debug_Assert_Finite(constrained_world_position, "Projected hinge position was not finite.");

            if (!Is_Finite(constrained_world_position))
            {
                return false;
            }

            Vector3 correction_delta =
                constrained_world_position - proposed_joint_world_position;

            proposed_joint_world_position = constrained_world_position;

            Update_State_After_Projection(
                ref joint_state,
                chosen_branch,
                chosen_direction);

            return correction_delta.sqrMagnitude > (epsilon * epsilon);
        }

        private static bool Constrain_Ball_Socket_Placeholder(
            in IkJoint joint,
            ref IkJointConstraintState joint_state,
            ChainSolveDirection solve_direction,
            ChainAnchorMode anchor_mode,
            Vector3 parent_world_position,
            Vector3 joint_world_position,
            ref Vector3 proposed_joint_world_position,
            Vector3 child_world_position,
            float epsilon)
        {
            return false;
        }

        private static int Choose_Branch(
            in IkJoint joint,
            ref IkJointConstraintState joint_state,
            int branch_a,
            Vector3 direction_a,
            int branch_b,
            Vector3 direction_b,
            Vector3 reference_direction,
            Vector3 hinge_axis_world,
            float angle_magnitude)
        {
            JointBranchPreference branch_preference = joint.BranchPreference;
            if (branch_preference == JointBranchPreference.BranchA)
            {
                return branch_a;
            }
            if (branch_preference == JointBranchPreference.BranchB)
            {
                return branch_b;
            }

            if (joint_state.has_last_direction)
            {
                Vector3 last_direction = joint_state.last_direction_world;
                float dot_a = Vector3.Dot(last_direction, direction_a);
                float dot_b = Vector3.Dot(last_direction, direction_b);

                if (dot_a > dot_b)
                {
                    return branch_a;
                }
                if (dot_b > dot_a)
                {
                    return branch_b;
                }
            }

            Vector3 preferred_bend_normal_world =
                Resolve_Preferred_Bend_Normal_World(in joint);

            if (preferred_bend_normal_world.sqrMagnitude >
                axis_epsilon * axis_epsilon)
            {
                preferred_bend_normal_world =
                    Safe_Normalize(preferred_bend_normal_world, hinge_axis_world, 1e-12f);

                float signed_area_a =
                    Vector3.Dot(Vector3.Cross(reference_direction, direction_a), preferred_bend_normal_world);

                float signed_area_b =
                    Vector3.Dot(Vector3.Cross(reference_direction, direction_b), preferred_bend_normal_world);

                if (signed_area_a > signed_area_b)
                {
                    return branch_a;
                }
                if (signed_area_b > signed_area_a)
                {
                    return branch_b;
                }
            }

            float branch_deadband_degrees = joint.BranchSwitchDeadbandDegrees;
            if (branch_deadband_degrees > 0f &&
                angle_magnitude <= branch_deadband_degrees)
            {
                if (joint_state.last_branch == branch_a ||
                    joint_state.last_branch == branch_b)
                {
                    return joint_state.last_branch;
                }
            }

            return branch_a;
        }

        private static void Update_State_After_Projection(
            ref IkJointConstraintState joint_state,
            int branch,
            Vector3 chosen_direction_world)
        {
            joint_state.last_branch = branch;
            joint_state.last_direction_world = chosen_direction_world;
            joint_state.has_last_direction = true;

            joint_state.Debug_Assert_Finite("Joint state");
        }

        private static Vector3 Resolve_Hinge_Axis_World(
            in IkJoint joint,
            in Vector3 parent_world_position,
            in Vector3 joint_world_position)
        {
            Transform pivot_transform = joint.PivotTransform;
            Vector3 axis_local = joint.PrimaryAxisLocal;

            if (pivot_transform != null)
            {
                Vector3 axis_world =
                    pivot_transform.TransformDirection(axis_local);

                if (Is_Finite(axis_world))
                {
                    return axis_world;
                }
            }

            if (Is_Finite(axis_local))
            {
                return axis_local;
            }

            return Vector3.forward;
        }

        private static Vector3 Resolve_Preferred_Bend_Normal_World(
            in IkJoint joint)
        {
            Transform pivot_transform = joint.PivotTransform;
            Vector3 normal_local = joint.PreferredBendNormalLocal;

            if (pivot_transform != null)
            {
                Vector3 normal_world =
                    pivot_transform.TransformDirection(normal_local);

                if (Is_Finite(normal_world))
                {
                    return normal_world;
                }
            }

            if (Is_Finite(normal_local))
            {
                return normal_local;
            }

            return Vector3.zero;
        }

        private static Vector3 Project_On_Plane(
            Vector3 vector,
            Vector3 plane_normal)
        {
            float projection_distance =
                Vector3.Dot(vector, plane_normal);

            return vector - plane_normal * projection_distance;
        }

        private static float Signed_Angle_On_Axis(
            Vector3 from_direction,
            Vector3 to_direction,
            Vector3 rotation_axis)
        {
            float unsigned_angle =
                Vector3.Angle(from_direction, to_direction);

            Vector3 cross_product =
                Vector3.Cross(from_direction, to_direction);

            float sign =
                Mathf.Sign(Vector3.Dot(cross_product, rotation_axis));

            return unsigned_angle * sign;
        }

        private static Vector3 Safe_Normalize(
            Vector3 vector,
            Vector3 fallback,
            float epsilon)
        {
            if (!Is_Finite(vector))
            {
                return fallback;
            }

            float magnitude = vector.magnitude;
            if (!float.IsFinite(magnitude) || magnitude <= epsilon)
            {
                return fallback;
            }

            return vector / magnitude;
        }

        private static bool Is_Finite(Vector3 vector)
        {
            if (!float.IsFinite(vector.x)) { return false; }
            if (!float.IsFinite(vector.y)) { return false; }
            if (!float.IsFinite(vector.z)) { return false; }
            return true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void Debug_Assert_Finite(
            Vector3 value,
            string message)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
