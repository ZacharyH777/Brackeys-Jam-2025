using UnityEngine;
using IKSystem.Data;
using IKSystem.Safety;

namespace IKSystem.Builders
{
    public static class ChainLayoutBuilder
    {
        private const float epsilon = 1e-12f;
        private const float default_tolerance = 0.001f;

        /* Build chain data from a validated path using the selected topology.
         * @param settings build settings
         * @param path validated hierarchy path
         * @param result built data
         * @param error_message failure reason
         */
        public static bool TryBuild(
            in IkSubChainBuildSettings settings,
            Transform[] path,
            out IkSubChainBuildResult result,
            out string error_message)
        {
            if (settings.alternating_topology)
            {
                return TryBuildAlternating(in settings, path, out result, out error_message);
            }

            return TryBuildNonAlternating(in settings, path, out result, out error_message);
        }

        /* Build arrays from an alternating bone/joint path.
         * @param settings build settings
         * @param path validated path
         * @param result built data
         * @param error_message failure reason
         */
        public static bool TryBuildAlternating(
            in IkSubChainBuildSettings settings,
            Transform[] path,
            out IkSubChainBuildResult result,
            out string error_message)
        {
            result = new IkSubChainBuildResult();
            error_message = null;

            if (path == null || path.Length < 2)
            {
                error_message = "Invalid chain path.";
                return false;
            }

            int joint_count = CountJointsAlternating(in settings, path);
            int bone_count = CountBonesAlternating(in settings, path);

            if (joint_count < 1)
            {
                error_message = "Alternating chain required at least one joint.";
                return false;
            }

            if (bone_count < 1)
            {
                error_message = "Alternating chain required at least one bone.";
                return false;
            }

            IkJoint[] joints = new IkJoint[joint_count];
            Vector3[] joint_world = new Vector3[joint_count];
            BoneConstraint[] bones = new BoneConstraint[bone_count];
            BoneEndpoints[] bone_world = new BoneEndpoints[bone_count];

            IkSubChain sub_chain = BuildChainHeader(in settings, joint_count);

            float final_bone_tolerance = ResolveFinalBoneTolerance(in settings, in sub_chain);
            float final_bone_weight = Mathf.Clamp01(settings.bone_weight);

            Vector3 safe_primary_axis = IkBuildSafety.SafeNormalize(settings.primary_axis, Vector3.right, epsilon);
            Vector3 safe_bend_normal = ResolveBendNormal(in settings, safe_primary_axis);

            JointKind joint_kind = ResolveJointKind(in settings);

            int joint_write_index = 0;
            int bone_write_index = 0;

            for (int path_index = 0; path_index < path.Length; path_index++)
            {
                Transform node = path[path_index];
                if (node == null)
                {
                    error_message = "Chain path contained a missing transform.";
                    return false;
                }

                if (IsBoneNodeAlternating(in settings, path_index))
                {
                    BoneEndpoints endpoints;
                    if (!TryGetBoneEndpointsAlternating(in settings, path, path_index, out endpoints))
                    {
                        error_message = "Failed to resolve bone endpoints.";
                        return false;
                    }

                    bone_world[bone_write_index] = endpoints;

                    float bone_length = ComputeBoneLength(in settings, endpoints.Parent, endpoints.Child);

                    BoneConstraint bone = new BoneConstraint();
                    bone.Length = bone_length;
                    bone.Tolerance = final_bone_tolerance;
                    bone.Weight = final_bone_weight;
                    bones[bone_write_index] = bone;

                    bone_write_index++;
                }
                else
                {
                    joint_world[joint_write_index] = node.position;

                    IkJoint joint = new IkJoint();
                    joint.JointIndex = joint_write_index;
                    joint.Kind = joint_kind;

                    if (settings.joints_virtual)
                    {
                        joint.PivotTransform = null;
                    }
                    else
                    {
                        joint.PivotTransform = node;
                    }

                    joint.Model = settings.joint_model;
                    joint.PrimaryAxisLocal = safe_primary_axis;
                    joint.PreferredBendNormalLocal = safe_bend_normal;
                    joint.BranchPreference = settings.branch_pref;
                    joint.BranchSwitchDeadbandDegrees = IkBuildSafety.ClampNonNegativeFinite(settings.branch_deadband);
                    joint.MaxDeltaDegrees = IkBuildSafety.ClampNonNegativeFinite(settings.max_delta);

                    joints[joint_write_index] = joint;
                    joint_write_index++;
                }
            }

            if (joint_write_index != joint_count)
            {
                error_message = "Joint indexing failed.";
                return false;
            }

            if (bone_write_index != bone_count)
            {
                error_message = "Bone indexing failed.";
                return false;
            }

            result.sub_chain = sub_chain;
            result.joints = joints;
            result.joint_world = joint_world;
            result.bones = bones;
            result.bone_world = bone_world;

            return true;
        }

        /* Build arrays from a non-alternating path (every node is a joint; bones are between adjacent joints).
         * @param settings build settings
         * @param path validated path
         * @param result built data
         * @param error_message failure reason
         */
        public static bool TryBuildNonAlternating(
            in IkSubChainBuildSettings settings,
            Transform[] path,
            out IkSubChainBuildResult result,
            out string error_message)
        {
            result = new IkSubChainBuildResult();
            error_message = null;

            if (path == null || path.Length < 2)
            {
                error_message = "Invalid chain path.";
                return false;
            }
            int joint_count = path.Length;
            int bone_count = joint_count - 1;

            if (joint_count < 2)
            {
                error_message = "Non-alternating chain required at least two joints.";
                return false;
            }

            IkJoint[] joints = new IkJoint[joint_count];
            Vector3[] joint_world = new Vector3[joint_count];
            BoneConstraint[] bones = new BoneConstraint[bone_count];
            BoneEndpoints[] bone_world = new BoneEndpoints[bone_count];

            IkSubChain sub_chain = BuildChainHeader(in settings, joint_count);

            float final_bone_tolerance = ResolveFinalBoneTolerance(in settings, in sub_chain);
            float final_bone_weight = Mathf.Clamp01(settings.bone_weight);

            Vector3 safe_primary_axis = IkBuildSafety.SafeNormalize(settings.primary_axis, Vector3.right, epsilon);
            Vector3 safe_bend_normal = ResolveBendNormal(in settings, safe_primary_axis);

            JointKind joint_kind = ResolveJointKind(in settings);

            for (int i = 0; i < joint_count; i++)
            {
                Transform node = path[i];
                if (node == null)
                {
                    error_message = "Chain path contained a missing transform.";
                    return false;
                }

                joint_world[i] = node.position;

                IkJoint joint = new IkJoint();
                joint.JointIndex = i;
                joint.Kind = joint_kind;

                if (settings.joints_virtual)
                {
                    joint.PivotTransform = null;
                }
                else
                {
                    joint.PivotTransform = node;
                }

                joint.Model = settings.joint_model;
                joint.PrimaryAxisLocal = safe_primary_axis;
                joint.PreferredBendNormalLocal = safe_bend_normal;
                joint.BranchPreference = settings.branch_pref;
                joint.BranchSwitchDeadbandDegrees = IkBuildSafety.ClampNonNegativeFinite(settings.branch_deadband);
                joint.MaxDeltaDegrees = IkBuildSafety.ClampNonNegativeFinite(settings.max_delta);

                joints[i] = joint;
            }

            for (int b = 0; b < bone_count; b++)
            {
                Vector3 parent_world = joint_world[b];
                Vector3 child_world = joint_world[b + 1];

                BoneEndpoints endpoints = new BoneEndpoints();
                endpoints.Parent = parent_world;
                endpoints.Child = child_world;
                bone_world[b] = endpoints;

                float bone_length = ComputeBoneLength(in settings, parent_world, child_world);

                BoneConstraint bone = new BoneConstraint();
                bone.Length = bone_length;
                bone.Tolerance = final_bone_tolerance;
                bone.Weight = final_bone_weight;
                bones[b] = bone;
            }

            result.sub_chain = sub_chain;
            result.joints = joints;
            result.joint_world = joint_world;
            result.bones = bones;
            result.bone_world = bone_world;

            return true;
        }

        private static JointKind ResolveJointKind(in IkSubChainBuildSettings settings)
        {
            if (settings.joints_virtual)
            {
                return JointKind.Virtual;
            }

            return JointKind.Transform;
        }

        private static IkSubChain BuildChainHeader(in IkSubChainBuildSettings settings, int joint_count)
        {
            IkSubChain chain = new IkSubChain();
            chain.StartJointIndex = 0;
            chain.EndJointIndex = joint_count - 1;
            chain.Phase = settings.phase;
            chain.RootPolicy = settings.root_policy;
            chain.AnchorMode = settings.anchor_mode;
            chain.PassOrder = settings.pass_order;
            chain.MaxIterations = settings.max_iterations;
            chain.Tolerance = IkBuildSafety.ClampNonNegativeFinite(settings.tolerance);
            return chain;
        }

        private static float ResolveFinalBoneTolerance(in IkSubChainBuildSettings settings, in IkSubChain sub_chain)
        {
            float overridden = IkBuildSafety.ClampNonNegativeFinite(settings.bone_tolerance);
            if (overridden > 0f)
            {
                return overridden;
            }

            float chain_tolerance = sub_chain.SafeTolerance(0f);
            chain_tolerance = IkBuildSafety.ClampNonNegativeFinite(chain_tolerance);
            if (chain_tolerance > 0f)
            {
                return chain_tolerance;
            }

            return default_tolerance;
        }

        private static Vector3 ResolveBendNormal(in IkSubChainBuildSettings settings, Vector3 safe_primary_axis)
        {
            Vector3 safe_bend_normal = IkBuildSafety.SafeNormalize(settings.bend_normal, Vector3.forward, epsilon);

            if (settings.joint_model == JointModel.Hinge2D)
            {
                safe_bend_normal = IkBuildSafety.EnsureNotParallel(safe_bend_normal, safe_primary_axis, Vector3.forward);
                safe_bend_normal = IkBuildSafety.SafeNormalize(safe_bend_normal, Vector3.forward, epsilon);
            }

            return safe_bend_normal;
        }

        private static float ComputeBoneLength(in IkSubChainBuildSettings settings, Vector3 parent_world_position, Vector3 child_world_position)
        {
            if (!settings.compute_length)
            {
                return 0f;
            }

            Vector3 delta = child_world_position - parent_world_position;
            float distance = delta.magnitude;

            if (!IkBuildSafety.IsFinite(distance))
            {
                return 0f;
            }

            if (distance <= epsilon)
            {
                return 0f;
            }

            return distance;
        }

        private static int CountJointsAlternating(in IkSubChainBuildSettings settings, Transform[] path)
        {
            int count = 0;

            for (int i = 0; i < path.Length; i++)
            {
                if (!IsBoneNodeAlternating(in settings, i))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountBonesAlternating(in IkSubChainBuildSettings settings, Transform[] path)
        {
            int count = 0;

            for (int i = 0; i < path.Length; i++)
            {
                if (IsBoneNodeAlternating(in settings, i))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsBoneNodeAlternating(in IkSubChainBuildSettings settings, int path_index)
        {
            if (settings.first_is_joint)
            {
                return (path_index % 2) == 1;
            }

            return (path_index % 2) == 0;
        }

        private static bool TryGetBoneEndpointsAlternating(
            in IkSubChainBuildSettings settings,
            Transform[] path,
            int path_index,
            out BoneEndpoints endpoints)
        {
            endpoints = new BoneEndpoints();

            if (path == null)
            {
                return false;
            }

            if (path_index < 0 || path_index >= path.Length)
            {
                return false;
            }

            if (!IsBoneNodeAlternating(in settings, path_index))
            {
                return false;
            }

            Transform bone_node = path[path_index];
            if (bone_node == null)
            {
                return false;
            }

            bool has_prev = path_index > 0;
            bool has_next = (path_index + 1) < path.Length;

            Transform prev_node = null;
            Transform next_node = null;

            if (has_prev)
            {
                prev_node = path[path_index - 1];
            }

            if (has_next)
            {
                next_node = path[path_index + 1];
            }

            bool prev_is_joint = false;
            bool next_is_joint = false;

            if (prev_node != null)
            {
                if (!IsBoneNodeAlternating(in settings, path_index - 1))
                {
                    prev_is_joint = true;
                }
            }

            if (next_node != null)
            {
                if (!IsBoneNodeAlternating(in settings, path_index + 1))
                {
                    next_is_joint = true;
                }
            }

            if (prev_is_joint && next_is_joint)
            {
                endpoints.Parent = prev_node.position;
                endpoints.Child = next_node.position;
                return true;
            }

            if (!prev_is_joint && next_is_joint)
            {
                endpoints.Parent = bone_node.position;
                endpoints.Child = next_node.position;
                return true;
            }

            if (prev_is_joint && !next_is_joint)
            {
                endpoints.Parent = prev_node.position;
                endpoints.Child = bone_node.position;
                return true;
            }

            return false;
        }
    }
}
