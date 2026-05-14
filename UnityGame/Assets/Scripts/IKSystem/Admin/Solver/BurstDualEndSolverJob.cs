using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Solver
{
    [BurstCompile]
    public static class BurstTransformUtils
    {
        /*
         * Returns true when an element index represents a rotating joint.
         *
         * @param element_index Element index inside the chain.
         * @param state Chain topology state.
         */
        public static bool IsJointElement(int element_index, IKChainState state)
        {
            if (!state.alternating_topology)
            {
                return true;
            }

            bool even_index = element_index % 2 == 0;
            bool is_joint = even_index == state.first_is_joint;

            return is_joint;
        }

        /*
         * Normalizes a vector with a fallback direction.
         *
         * @param value Input vector.
         * @param fallback Direction used when the input is too small.
         */
        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float length = math.length(value);

            if (length <= 0.000001f)
            {
                return fallback;
            }

            return value / length;
        }

        /*
         * Creates a world rotation that points local X toward a world direction.
         *
         * @param direction_world Direction in world space.
         * @param fallback_rotation Rotation used when direction is invalid.
         */
        public static quaternion CreateWorldRotationFromXAxis(float3 direction_world, quaternion fallback_rotation)
        {
            float3 x_axis = NormalizeSafe(direction_world, new float3(1f, 0f, 0f));

            float3 z_axis = new float3(0f, 0f, 1f);
            float3 y_axis = math.cross(z_axis, x_axis);

            float y_length = math.length(y_axis);

            if (y_length <= 0.000001f)
            {
                return fallback_rotation;
            }

            y_axis = y_axis / y_length;
            z_axis = math.cross(x_axis, y_axis);

            float3x3 matrix = new float3x3(x_axis, y_axis, z_axis);
            return new quaternion(matrix);
        }

        /*
         * Converts a world rotation to local rotation.
         *
         * @param parent_world_rotation Parent world rotation.
         * @param world_rotation Desired world rotation.
         */
        public static quaternion WorldToLocalRotation(quaternion parent_world_rotation, quaternion world_rotation)
        {
            return math.mul(math.inverse(parent_world_rotation), world_rotation);
        }

        /*
         * Converts a local rotation to world rotation.
         *
         * @param parent_world_rotation Parent world rotation.
         * @param local_rotation Local rotation.
         */
        public static quaternion LocalToWorldRotation(quaternion parent_world_rotation, quaternion local_rotation)
        {
            return math.mul(parent_world_rotation, local_rotation);
        }
    }

    [BurstCompile]
    public struct BurstDualEndSolverJob : IJobParallelFor
    {
        private const float score_epsilon = 0.0000001f;
        private const int look_ahead_iterations = 4;
        private const int minimum_iterations = 2;

        public float DeltaTime;

        [ReadOnly] public NativeArray<int> PhaseIndices;
        [ReadOnly] public NativeArray<IKChainState> ChainStates;
        [ReadOnly] public NativeArray<DualEndSolverSettings> Settings;
        [ReadOnly] public NativeArray<int> TransformIndices;

        [ReadOnly] public NativeArray<JointState> JointStates;
        [ReadOnly] public NativeArray<BoneStaticData> BoneStatics;
        [ReadOnly] public NativeArray<JointStaticData> JointStatics;

        [NativeDisableParallelForRestriction] public NativeArray<float3> SolvePositions;
        [NativeDisableParallelForRestriction] public NativeArray<float3> PreviousSolvePositions;
        [NativeDisableParallelForRestriction] public NativeArray<float3> BestSolvePositions;
        [NativeDisableParallelForRestriction] public NativeArray<float3> TempSolvePositions;

        [NativeDisableParallelForRestriction] public NativeArray<float3> OutputWorldPositions;

        [NativeDisableParallelForRestriction] public NativeArray<quaternion> OutputLocalRotations;
        [NativeDisableParallelForRestriction] public NativeArray<quaternion> OutputWorldRotations;

        /*
         * Solves one IK chain from the scheduled phase list.
         */
        public void Execute(int index)
        {
            // Get all sub chains
            int chain_index = PhaseIndices[index]; // Tail, Spine, or limb
            IKChainState chain = ChainStates[chain_index];
            DualEndSolverSettings settings = Settings[chain_index];

            // If it is close enough return
            if (chain.SolveWeight <= 0.001f)
            {
                return;
            }

            // Get sub chain length
            int start = chain.ElementStartIndex;
            int count = chain.ElementCount;

            if (count < 2)
            {
                return;
            }

            LoadCurrentElementPositions(start, count, settings);

            float3 root_position = chain.StartTargetPos;
            float3 target_position = chain.EndTargetPos;

            if (settings.Planar2D)
            {
                root_position.z = SolvePositions[start].z;
                target_position.z = SolvePositions[start + count - 1].z;
            }

            SolvePositions(start, count, root_position, target_position, settings, chain);
            SolveLocalRotations(start, count, chain, settings);
        }

        /*
        * We need the chain indecies but 
        * correlated to the map of transforms
        */
        private void LoadCurrentElementPositions(
            int start,
            int count,
            DualEndSolverSettings settings)
        {
            for (int element_index = 0; element_index < count; element_index++)
            {
                int global_element_index = start + element_index;
                int transform_index = TransformIndices[global_element_index];

                PreviousSolvePositions[global_element_index] = SolvePositions[global_element_index];

                if (transform_index < 0)
                {
                    SolvePositions[global_element_index] = float3.zero;
                    continue;
                }

                float3 world_position = OutputWorldPositions[transform_index];
                SolvePositions[global_element_index] = math.transform(settings.WorldToSolveSpace, world_position);
            }
        }

        /*
        *
        *
        */
        private void SolveTargetFromCurrentPose(
            int start,
            int count,
            float3 root_position,
            float3 target_position,
            DualEndSolverSettings settings,
            IKChainState chain)
        {
            SolvePositions[start + count - 1] = target_position;

            ReprojectBackwardLengths(
                start,
                count,
                settings);

            ReprojectForwardLengthsConstrained(
                start,
                count,
                root_position,
                settings,
                chain);
        }

        /*
        * Transforms a direction by a matrix without applying translation.
        *
        * @param matrix Transform matrix.
        * @param direction Direction to transform.
        */
        private float3 TransformDirection(
            float4x4 matrix,
            float3 direction)
        {
            float3x3 rotation_scale = new float3x3(
                matrix.c0.xyz,
                matrix.c1.xyz,
                matrix.c2.xyz);

            return math.mul(rotation_scale, direction);
        }

        /*
        * Applies pull before the target solve so pull influences the initial guess without overriding the target.
        *
        * @param start Global element start index.
        * @param count Number of chain elements.
        * @param pull_position Pull target position.
        * @param settings Solver settings.
        */
        private void ApplyPullPreSolveBias(
            int start,
            int count,
            float3 pull_position,
            DualEndSolverSettings settings)
        {
            if (settings.PullStrength <= 0f)
            {
                return;
            }

            if (settings.PullPasses <= 0)
            {
                return;
            }

            if (count <= 2)
            {
                return;
            }

            float pull_strength = math.clamp(
                settings.PullStrength,
                0f,
                1f);

            float pull_bias = math.clamp(
                settings.PullBias,
                0f,
                1f);

            for (int pass_index = 0; pass_index < settings.PullPasses; pass_index++)
            {
                for (int element_index = 1; element_index < count - 1; element_index++)
                {
                    int global_element_index = start + element_index;

                    float normalized_index = (float)element_index / (float)(count - 1);
                    float root_weight = 1f - normalized_index;
                    float tip_weight = normalized_index;

                    float bell_weight = math.sin(normalized_index * math.PI);
                    float biased_weight = math.lerp(root_weight, tip_weight, pull_bias);
                    float final_weight = bell_weight * biased_weight * pull_strength;

                    float3 current_position = SolvePositions[global_element_index];

                    float3 pulled_position = math.lerp(
                        current_position,
                        pull_position,
                        final_weight);

                    if (settings.Planar2D)
                    {
                        pulled_position.z = current_position.z;
                    }

                    SolvePositions[global_element_index] = pulled_position;
                }
            }
        }

        /*
        * Returns true when the pull biased candidate should replace the no pull candidate.
        *
        * @param pull_target_error_sq Pull candidate target error squared.
        * @param pull_pull_error_sq Pull candidate pull error squared.
        * @param pull_motion_error_sq Pull candidate motion error squared.
        * @param no_pull_target_error_sq No pull candidate target error squared.
        * @param no_pull_pull_error_sq No pull candidate pull error squared.
        * @param no_pull_motion_error_sq No pull candidate motion error squared.
        * @param tolerance_sq Target tolerance squared.
        */
        private bool ShouldUsePullCandidate(
            float pull_target_error_sq,
            float pull_pull_error_sq,
            float pull_motion_error_sq,
            float no_pull_target_error_sq,
            float no_pull_pull_error_sq,
            float no_pull_motion_error_sq,
            float tolerance_sq)
        {
            float target_slack = math.max(
                tolerance_sq,
                score_epsilon);

            if (pull_target_error_sq > no_pull_target_error_sq + target_slack)
            {
                return false;
            }

            if (pull_target_error_sq < no_pull_target_error_sq - target_slack)
            {
                return true;
            }

            if (pull_pull_error_sq < no_pull_pull_error_sq - score_epsilon)
            {
                return true;
            }

            float pull_delta = math.abs(
                pull_pull_error_sq - no_pull_pull_error_sq);

            if (pull_delta > score_epsilon)
            {
                return false;
            }

            if (pull_motion_error_sq < no_pull_motion_error_sq - score_epsilon)
            {
                return true;
            }

            return false;
        }

        /*
        * Solves positions by using pull as a pre solve bias and committing only the best legal pose.
        *
        * @param start Global element start index.
        * @param count Number of chain elements.
        * @param root_position Fixed root position.
        * @param target_position Desired tip position.
        * @param settings Solver settings.
        * @param chain Chain topology state.
        */
        private void SolvePositions(
            int start,
            int count,
            float3 root_position,
            float3 target_position,
            DualEndSolverSettings settings,
            IKChainState chain)
        {
            float3 pull_position = settings.PullEndLocal;
            if (settings.Planar2D)
            {
                pull_position.z = target_position.z;
            }

            int iterations = settings.Iterations;

            if (iterations < minimum_iterations)
            {
                iterations = minimum_iterations;
            }

            float tolerance_sq = settings.Tolerance * settings.Tolerance;
            float best_target_error_sq = float.MaxValue;
            float best_pull_error_sq = float.MaxValue;
            float best_motion_error_sq = float.MaxValue;

            int stale_iterations = 0;

            CopyPose(SolvePositions, BestSolvePositions, start, count);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                CopyPose(BestSolvePositions, SolvePositions, start, count);

                SolveTargetFromCurrentPose(
                    start,
                    count,
                    root_position,
                    target_position,
                    settings,
                    chain);

                float no_pull_target_error_sq = GetTargetErrorSq(
                    start,
                    count,
                    target_position);

                float no_pull_pull_error_sq = GetPullErrorSq(
                    start,
                    count,
                    pull_position);

                float no_pull_motion_error_sq = GetMotionErrorSq(
                    start,
                    count);

                CopyPose(SolvePositions, TempSolvePositions, start, count);

                CopyPose(BestSolvePositions, SolvePositions, start, count);

                ApplyPullPreSolveBias(
                    start,
                    count,
                    pull_position,
                    settings);

                SolveTargetFromCurrentPose(
                    start,
                    count,
                    root_position,
                    target_position,
                    settings,
                    chain);

                float pull_target_error_sq = GetTargetErrorSq(
                    start,
                    count,
                    target_position);

                float pull_pull_error_sq = GetPullErrorSq(
                    start,
                    count,
                    pull_position);

                float pull_motion_error_sq = GetMotionErrorSq(
                    start,
                    count);

                bool use_pull_candidate = ShouldUsePullCandidate(
                    pull_target_error_sq,
                    pull_pull_error_sq,
                    pull_motion_error_sq,
                    no_pull_target_error_sq,
                    no_pull_pull_error_sq,
                    no_pull_motion_error_sq,
                    tolerance_sq);

                if (!use_pull_candidate)
                {
                    CopyPose(TempSolvePositions, SolvePositions, start, count);

                    pull_target_error_sq = no_pull_target_error_sq;
                    pull_pull_error_sq = no_pull_pull_error_sq;
                    pull_motion_error_sq = no_pull_motion_error_sq;
                }

                bool improved = IsBetterPose(
                    pull_target_error_sq,
                    pull_pull_error_sq,
                    pull_motion_error_sq,
                    best_target_error_sq,
                    best_pull_error_sq,
                    best_motion_error_sq,
                    tolerance_sq);

                if (improved)
                {
                    best_target_error_sq = pull_target_error_sq;
                    best_pull_error_sq = pull_pull_error_sq;
                    best_motion_error_sq = pull_motion_error_sq;
                    stale_iterations = 0;

                    CopyPose(SolvePositions, BestSolvePositions, start, count);
                }
                else
                {
                    stale_iterations++;
                    CopyPose(BestSolvePositions, SolvePositions, start, count);
                }

                if (iteration >= minimum_iterations)
                {
                    if (stale_iterations >= look_ahead_iterations)
                    {
                        break;
                    }
                }
            }

            CopyPose(BestSolvePositions, SolvePositions, start, count);
        }

        /*
         * Returns true when a new pose is meaningfully better than the previous best.
         *
         * @param target_error_sq Current target error squared.
         * @param pull_error_sq Current pull error squared.
         * @param motion_error_sq Current motion error squared.
         * @param best_target_error_sq Best target error squared.
         * @param best_pull_error_sq Best pull error squared.
         * @param best_motion_error_sq Best motion error squared.
         * @param tolerance_sq Target tolerance squared.
         */
        private bool IsBetterPose(
            float target_error_sq,
            float pull_error_sq,
            float motion_error_sq,
            float best_target_error_sq,
            float best_pull_error_sq,
            float best_motion_error_sq,
            float tolerance_sq)
        {
            float meaningful_target_improvement = math.max(tolerance_sq * 0.25f, score_epsilon);

            if (target_error_sq < best_target_error_sq - meaningful_target_improvement)
            {
                return true;
            }

            float target_delta = math.abs(target_error_sq - best_target_error_sq);

            if (target_delta > meaningful_target_improvement)
            {
                return false;
            }

            if (target_error_sq > tolerance_sq)
            {
                return false;
            }

            if (pull_error_sq < best_pull_error_sq - score_epsilon)
            {
                return true;
            }

            float pull_delta = math.abs(pull_error_sq - best_pull_error_sq);

            if (pull_delta > score_epsilon)
            {
                return false;
            }

            if (motion_error_sq < best_motion_error_sq - score_epsilon)
            {
                return true;
            }

            return false;
        }

        /*
         * Reprojects all lengths from tip to root.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         * @param settings Solver settings.
         */
        private void ReprojectBackwardLengths(
            int start,
            int count,
            DualEndSolverSettings settings)
        {
            for (int element_index = count - 2; element_index >= 0; element_index--)
            {
                int current_index = start + element_index;
                int child_index = current_index + 1;

                float length = GetElementLength(start, element_index);
                float3 direction = SolvePositions[current_index] - SolvePositions[child_index];

                if (settings.Planar2D)
                {
                    direction.z = 0f;
                }

                direction = BurstTransformUtils.NormalizeSafe(
                    direction,
                    new float3(1f, 0f, 0f));

                SolvePositions[current_index] = SolvePositions[child_index] + direction * length;
            }
        }

        /*
         * Reprojects all lengths from root to tip while applying joint constraints.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         * @param root_position Fixed root position.
         * @param settings Solver settings.
         * @param chain Chain topology state.
         */
        private void ReprojectForwardLengthsConstrained(
            int start,
            int count,
            float3 root_position,
            DualEndSolverSettings settings,
            IKChainState chain)
        {
            SolvePositions[start] = root_position;

            int root_transform_index = TransformIndices[start];

            if (root_transform_index < 0)
            {
                ReprojectForwardLengthsUnconstrained(start, count, settings);
                return;
            }

            quaternion parent_world_rotation = GetRootParentWorldRotation(root_transform_index);

            for (int element_index = 0; element_index < count - 1; element_index++)
            {
                int current_index = start + element_index;
                int child_index = current_index + 1;
                int transform_index = TransformIndices[current_index];

                float length = GetElementLength(start, element_index);
                float3 direction_world = SolvePositions[child_index] - SolvePositions[current_index];

                if (settings.Planar2D)
                {
                    direction_world.z = 0f;
                }

                direction_world = BurstTransformUtils.NormalizeSafe(
                    direction_world,
                    new float3(1f, 0f, 0f));

                if (transform_index < 0)
                {
                    SolvePositions[child_index] = SolvePositions[current_index] + direction_world * length;
                    continue;
                }

                quaternion current_local_rotation = OutputLocalRotations[transform_index];

                quaternion current_world_rotation = BurstTransformUtils.LocalToWorldRotation(
                    parent_world_rotation,
                    current_local_rotation);

                quaternion desired_world_rotation = BurstTransformUtils.CreateWorldRotationFromXAxis(
                    direction_world,
                    current_world_rotation);

                quaternion desired_local_rotation = BurstTransformUtils.WorldToLocalRotation(
                    parent_world_rotation,
                    desired_world_rotation);

                bool should_apply_joint_constraint = chain.ConstraintsEnabled;

                if (should_apply_joint_constraint)
                {
                    should_apply_joint_constraint = BurstTransformUtils.IsJointElement(
                        element_index,
                        chain);
                }

                if (should_apply_joint_constraint)
                {
                    if (current_index < JointStatics.Length)
                    {
                        JointStaticData joint_static = JointStatics[current_index];

                        desired_local_rotation = BurstJointSolver.ApplyJointRotationConstraint(
                            current_local_rotation,
                            desired_local_rotation,
                            joint_static);

                        desired_world_rotation = BurstTransformUtils.LocalToWorldRotation(
                            parent_world_rotation,
                            desired_local_rotation);

                        direction_world = ResolveConstrainedDirectionWorld(
                            desired_world_rotation,
                            joint_static,
                            direction_world,
                            settings);
                    }
                }

                SolvePositions[child_index] = SolvePositions[current_index] + direction_world * length;

                parent_world_rotation = desired_world_rotation;
            }
        }

        /*
         * Reprojects all lengths from root to tip without joint constraints.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         * @param settings Solver settings.
         */
        private void ReprojectForwardLengthsUnconstrained(
            int start,
            int count,
            DualEndSolverSettings settings)
        {
            for (int element_index = 0; element_index < count - 1; element_index++)
            {
                int current_index = start + element_index;
                int child_index = current_index + 1;

                float length = GetElementLength(start, element_index);
                float3 direction = SolvePositions[child_index] - SolvePositions[current_index];

                if (settings.Planar2D)
                {
                    direction.z = 0f;
                }

                direction = BurstTransformUtils.NormalizeSafe(
                    direction,
                    new float3(1f, 0f, 0f));

                SolvePositions[child_index] = SolvePositions[current_index] + direction * length;
            }
        }

        /*
         * Resolves the child direction after a constrained joint rotation.
         *
         * @param constrained_world_rotation World rotation after constraint.
         * @param joint_static Static joint data.
         * @param fallback_direction Fallback direction.
         * @param settings Solver settings.
         */
        private float3 ResolveConstrainedDirectionWorld(
            quaternion constrained_world_rotation,
            JointStaticData joint_static,
            float3 fallback_direction,
            DualEndSolverSettings settings)
        {
            float3 primary_axis_local = joint_static.primary_axis_local;

            if (math.lengthsq(primary_axis_local) <= 0.000001f)
            {
                primary_axis_local = new float3(1f, 0f, 0f);
            }

            float3 direction_world = math.mul(
                constrained_world_rotation,
                primary_axis_local);

            if (settings.Planar2D)
            {
                direction_world.z = 0f;
            }

            direction_world = BurstTransformUtils.NormalizeSafe(
                direction_world,
                fallback_direction);

            return direction_world;
        }

        /*
         * Solves local rotations from the final best constrained positions.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         * @param chain Chain state.
         * @param settings Solver settings.
         */
        private void SolveLocalRotations(
            int start,
            int count,
            IKChainState chain,
            DualEndSolverSettings settings)
        {
            int root_transform_index = TransformIndices[start];

            if (root_transform_index < 0)
            {
                return;
            }

            quaternion parent_world_rotation = GetRootParentWorldRotation(root_transform_index);

            for (int element_index = 0; element_index < count; element_index++)
            {
                int global_element_index = start + element_index;
                int transform_index = TransformIndices[global_element_index];

                if (transform_index < 0)
                {
                    continue;
                }

                quaternion current_local_rotation = OutputLocalRotations[transform_index];

                quaternion current_world_rotation = BurstTransformUtils.LocalToWorldRotation(
                    parent_world_rotation,
                    current_local_rotation);

                bool is_joint = BurstTransformUtils.IsJointElement(element_index, chain);

                if (!is_joint)
                {
                    OutputLocalRotations[transform_index] = current_local_rotation;
                    OutputWorldRotations[transform_index] = current_world_rotation;
                    parent_world_rotation = current_world_rotation;
                    continue;
                }

                if (element_index >= count - 1)
                {
                    OutputLocalRotations[transform_index] = current_local_rotation;
                    OutputWorldRotations[transform_index] = current_world_rotation;
                    parent_world_rotation = current_world_rotation;
                    continue;
                }

                float3 current_position = SolvePositions[global_element_index];
                float3 child_position = SolvePositions[global_element_index + 1];
                float3 direction_solve = child_position - current_position;

                if (settings.Planar2D)
                {
                    direction_solve.z = 0f;
                }

                float3 direction_world = TransformDirection(
                    settings.SolveSpaceToWorld,
                    direction_solve);

                quaternion desired_world_rotation = BurstTransformUtils.CreateWorldRotationFromXAxis(
                    direction_world,
                    current_world_rotation);

                quaternion desired_local_rotation = BurstTransformUtils.WorldToLocalRotation(
                    parent_world_rotation,
                    desired_world_rotation);

                if (chain.ConstraintsEnabled)
                {
                    if (global_element_index < JointStatics.Length)
                    {
                        JointStaticData joint_static = JointStatics[global_element_index];

                        desired_local_rotation = BurstJointSolver.ApplyJointRotationConstraint(
                            current_local_rotation,
                            desired_local_rotation,
                            joint_static);

                        desired_world_rotation = BurstTransformUtils.LocalToWorldRotation(
                            parent_world_rotation,
                            desired_local_rotation);
                    }
                }

                OutputLocalRotations[transform_index] = desired_local_rotation;
                OutputWorldRotations[transform_index] = desired_world_rotation;

                parent_world_rotation = desired_world_rotation;
            }
        }

        /*
         * Copies a chain pose from one position buffer to another.
         *
         * @param source Source position buffer.
         * @param destination Destination position buffer.
         * @param start Global element start index.
         * @param count Number of chain elements.
         */
        private void CopyPose(
            NativeArray<float3> source,
            NativeArray<float3> destination,
            int start,
            int count)
        {
            for (int element_index = 0; element_index < count; element_index++)
            {
                int global_element_index = start + element_index;
                destination[global_element_index] = source[global_element_index];
            }
        }

        /*
         * Gets the squared target error for the current pose.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         * @param target_position Desired tip position.
         */
        private float GetTargetErrorSq(
            int start,
            int count,
            float3 target_position)
        {
            int tip_index = start + count - 1;
            return math.lengthsq(SolvePositions[tip_index] - target_position);
        }

        /*
         * Gets the squared pull preference error for the current pose.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         * @param pull_position Pull target position.
         */
        private float GetPullErrorSq(
            int start,
            int count,
            float3 pull_position)
        {
            if (count <= 2)
            {
                return 0f;
            }

            float result = 0f;

            for (int element_index = 1; element_index < count - 1; element_index++)
            {
                int global_element_index = start + element_index;
                result += math.lengthsq(SolvePositions[global_element_index] - pull_position);
            }

            return result;
        }

        /*
         * Gets the squared motion error from previous frame positions.
         *
         * @param start Global element start index.
         * @param count Number of chain elements.
         */
        private float GetMotionErrorSq(
            int start,
            int count)
        {
            float result = 0f;

            for (int element_index = 1; element_index < count - 1; element_index++)
            {
                int global_element_index = start + element_index;
                result += math.lengthsq(SolvePositions[global_element_index] - PreviousSolvePositions[global_element_index]);
            }

            return result;
        }

        /*
         * Gets the stored length for the segment after an element.
         *
         * @param start Global element start index.
         * @param element_index Local element index.
         */
        private float GetElementLength(int start, int element_index)
        {
            int static_index = start + element_index;

            if (static_index < 0)
            {
                return 0f;
            }

            if (static_index >= BoneStatics.Length)
            {
                return 0f;
            }

            float length = BoneStatics[static_index].RestLength;

            if (length <= 0.000001f)
            {
                return 0.000001f;
            }

            return length;
        }

        /*
         * Estimates the parent world rotation for the root element.
         *
         * @param root_transform_index Global transform index of the root element.
         */
        private quaternion GetRootParentWorldRotation(int root_transform_index)
        {
            quaternion root_world_rotation = OutputWorldRotations[root_transform_index];
            quaternion root_local_rotation = OutputLocalRotations[root_transform_index];

            return math.mul(root_world_rotation, math.inverse(root_local_rotation));
        }
    }
}
