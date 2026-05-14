using UnityEngine;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;
using RunstarSystems.IKSystem.Safety;

namespace RunstarSystems.IKSystem.Builders
{
    public static class ChainLayoutBuilder
    {
        private const float epsilon = 1e-12f;
        private const float default_tolerance = 0.001f;

        public static bool TryBuild(
            in IkSubChainBuildSettings settings,
            int subChainIndex,
            Transform[] path,
            ref IkSubChainData result,
            out string error_message)
        {
            if (settings.alternating_topology)
            {
                return TryBuildAlternating(in settings, subChainIndex, path, ref result, out error_message);
            }

            return TryBuildNonAlternating(in settings, subChainIndex, path, ref result, out error_message);
        }

        public static bool TryBuildAlternating(
            in IkSubChainBuildSettings settings,
            int subChainIndex,
            Transform[] path,
            ref IkSubChainData result,
            out string error_message)
        {
            error_message = null;

            if (path == null || path.Length < 2)
            {
                error_message = "Invalid chain path.";
                return false;
            }

            float final_bone_tolerance = ResolveFinalBoneTolerance(in settings);
            float final_bone_weight = Mathf.Clamp01(settings.bone_weight);

            Vector3 safe_primary_axis = IkBuildSafety.SafeNormalize(settings.primary_axis, Vector3.right, epsilon);
            Vector3 safe_bend_normal = ResolveBendNormal(in settings, safe_primary_axis);

            JointKind joint_kind = ResolveJointKind(in settings);

            int joint_write_index = 0;

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
                    bool prev_is_joint = path_index > 0 && !IsBoneNodeAlternating(in settings, path_index - 1);
                    bool next_is_joint = (path_index + 1) < path.Length && !IsBoneNodeAlternating(in settings, path_index + 1);

                    int parent_idx = prev_is_joint ? (joint_write_index - 1) : -1;
                    int child_idx = next_is_joint ? joint_write_index : -1;

                    Vector3 parent_pos = prev_is_joint ? path[path_index - 1].position : node.position;
                    Vector3 child_pos = next_is_joint ? path[path_index + 1].position : node.position;

                    float bone_length = ComputeBoneLength(in settings, parent_pos, child_pos);

                    result.boneStates.Add(new BoneState
                    {
                        Position = node.position,
                        Rotation = node.rotation,
                        LocalScale = node.localScale
                    });

                    result.boneStatics.Add(new BoneStaticData
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Name = node.name,
#endif
                        ParentJointIndex = parent_idx,
                        ChildJointIndex = child_idx,
                        RestLength = bone_length,
                        Weight = final_bone_weight,
                        Tolerance = final_bone_tolerance
                    });
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    string jointName = node.name;
                    if (settings.joints_virtual)
                    {
                        string parentName = (path_index > 0) ? path[path_index - 1].name : "Start";
                        string childName = (path_index + 1 < path.Length) ? path[path_index + 1].name : "End";
                        jointName = $"{parentName}-{childName} Joint";
                    }
#endif

                    result.jointStates.Add(new JointState
                    {
                        world_position = node.position,
                        world_rotation = node.rotation,
                        previous_world_position = node.position,
                        previous_world_rotation = node.rotation,
                        count = 0
                    });

                    result.jointStatics.Add(new JointStaticData
                    {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                        name = jointName,
                    #endif
                        kind = joint_kind,
                        model = settings.joint_model,
                        primary_axis_local = safe_primary_axis,
                        preferred_bend_normal_local = safe_bend_normal,
                        branch_preference = settings.branch_pref,
                        branch_deadband = IkBuildSafety.ClampNonNegativeFinite(settings.branch_deadband),
                        max_delta_degrees = IkBuildSafety.ClampNonNegativeFinite(settings.max_delta),
                        constraint = BuildJointConstraintData(in settings, safe_primary_axis, safe_bend_normal),
                        sub_chain_index = subChainIndex
                    });
                    joint_write_index++;
                }
            }

            BuildChainState(in settings, ref result);
            return true;
        }

        /*
        * Builds model specific joint constraint data from chain build settings.
        *
        * @param settings Source chain build settings.
        * @param safe_primary_axis Safe primary local axis.
        * @param safe_bend_normal Safe bend normal local axis.
        */
        private static JointConstraintData BuildJointConstraintData(
            in IkSubChainBuildSettings settings,
            Vector3 safe_primary_axis,
            Vector3 safe_bend_normal)
        {
            JointConstraintData constraint = default(JointConstraintData);

            constraint.hinge_2d = new Hinge2DJointData
            {
                hinge_axis_local = safe_bend_normal,
                reference_direction_local = safe_primary_axis,
                min_angle_degrees = -180f,
                max_angle_degrees = 180f,
                use_limits = 0
            };

            constraint.ball_socket = new BallSocketJointData
            {
                swing_axis_local = safe_primary_axis,
                reference_direction_local = safe_bend_normal,
                max_swing_degrees = 180f,
                min_twist_degrees = -180f,
                max_twist_degrees = 180f,
                use_swing_limit = 0,
                use_twist_limit = 0
            };

            return constraint;
        }

        public static bool TryBuildNonAlternating(
            in IkSubChainBuildSettings settings,
            int subChainIndex,
            Transform[] path,
            ref IkSubChainData result,
            out string error_message)
        {
            error_message = null;

            if (path == null || path.Length < 2)
            {
                error_message = "Invalid chain path.";
                return false;
            }

            int joint_count = path.Length;
            int bone_count = joint_count - 1;

            float final_bone_tolerance = ResolveFinalBoneTolerance(in settings);
            float final_bone_weight = Mathf.Clamp01(settings.bone_weight);

            Vector3 safe_primary_axis = IkBuildSafety.SafeNormalize(settings.primary_axis, Vector3.right, epsilon);
            Vector3 safe_bend_normal = ResolveBendNormal(in settings, safe_primary_axis);

            JointKind joint_kind = ResolveJointKind(in settings);

            // 1. Build Joints
            for (int i = 0; i < joint_count; i++)
            {
                Transform node = path[i];
                if (node == null)
                {
                    error_message = "Chain path contained a missing transform.";
                    return false;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                string jointName = settings.joints_virtual ? $"{node.name} (Virtual)" : node.name;
#endif

                result.jointStates.Add(new JointState
                {
                    world_position = node.position,
                    world_rotation = node.rotation,
                    previous_world_position = node.position,
                    previous_world_rotation = node.rotation,
                    count = 0
                });

                result.jointStatics.Add(new JointStaticData
                {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    name = jointName,
                #endif
                    kind = joint_kind,
                    model = settings.joint_model,
                    primary_axis_local = safe_primary_axis,
                    preferred_bend_normal_local = safe_bend_normal,
                    branch_preference = settings.branch_pref,
                    branch_deadband = IkBuildSafety.ClampNonNegativeFinite(settings.branch_deadband),
                    max_delta_degrees = IkBuildSafety.ClampNonNegativeFinite(settings.max_delta),
                    constraint = BuildJointConstraintData(in settings, safe_primary_axis, safe_bend_normal),
                    sub_chain_index = subChainIndex
                });
            }

            // 2. Build Connecting Bones
            for (int b = 0; b < bone_count; b++)
            {
                Transform current = path[b];
                Transform next = path[b + 1];

                float bone_length = ComputeBoneLength(in settings, current.position, next.position);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                string boneName = $"{current.name}-{next.name} Bone";
#endif

                result.boneStates.Add(new BoneState
                {
                    Position = current.position,
                    Rotation = current.rotation,
                    LocalScale = current.localScale
                });

                result.boneStatics.Add(new BoneStaticData
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Name = boneName, 
#endif
                    ParentJointIndex = b,
                    ChildJointIndex = b + 1,
                    RestLength = bone_length,
                    Weight = final_bone_weight,
                    Tolerance = final_bone_tolerance
                });
            }

            BuildChainState(in settings, ref result);
            return true;
        }

        private static JointKind ResolveJointKind(in IkSubChainBuildSettings settings)
        {
            return settings.joints_virtual ? JointKind.Virtual : JointKind.Transform;
        }

        private static void BuildChainState(in IkSubChainBuildSettings settings, ref IkSubChainData result)
        {
            result.chainState = new IKChainState
            {
                StartTargetPos = settings.start.position,
                StartTargetRot = settings.start.rotation,
                EndTargetPos = settings.end.position,
                EndTargetRot = settings.end.rotation,

                ElementStartIndex = 0,
                ElementCount = result.jointStates.Count + result.boneStates.Count,
                
                SolveWeight = 1.0f,
                ConstraintsEnabled = true,

                Phase = settings.phase,
                RootPolicy = settings.root_policy,
                AnchorMode = settings.anchor_mode,
                PassOrder = settings.pass_order,
                MaxIterations = settings.max_iterations,
                Tolerance = IkBuildSafety.ClampNonNegativeFinite(settings.tolerance)
            };
        }

        private static float ResolveFinalBoneTolerance(in IkSubChainBuildSettings settings)
        {
            float overridden = IkBuildSafety.ClampNonNegativeFinite(settings.bone_tolerance);
            if (overridden > 0f) return overridden;

            float chain_tolerance = IkBuildSafety.ClampNonNegativeFinite(settings.tolerance);
            if (chain_tolerance > 0f) return chain_tolerance;

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
            if (!settings.compute_length) return 0f;

            Vector3 delta = child_world_position - parent_world_position;
            float distance = delta.magnitude;

            if (!IkBuildSafety.IsFinite(distance) || distance <= epsilon) return 0f;

            return distance;
        }

        private static bool IsBoneNodeAlternating(in IkSubChainBuildSettings settings, int path_index)
        {
            if (settings.first_is_joint) return (path_index % 2) == 1;
            return (path_index % 2) == 0;
        }
    }
}
