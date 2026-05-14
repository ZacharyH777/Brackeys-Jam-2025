using System;
using Unity.Mathematics;
using Unity.Collections;

namespace RunstarSystems.IKSystem.Data
{
    public enum JointKind
    {
        Virtual = 0,
        Transform = 1
    }

    public enum JointModel
    {
        Free = 0,
        Hinge2D = 1,
        BallSocket = 2
    }

    public enum JointBranchPreference
    {
        None = 0,
        BranchA = 1,
        BranchB = 2
    }

    [Serializable]
    public struct JointState
    {
        public float3 world_position;
        public quaternion world_rotation;

        public float3 previous_world_position;
        public quaternion previous_world_rotation;

        public int count;
    }

    [Serializable]
    public struct JointStaticData
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FixedString64Bytes name;
#endif

        public JointKind kind;
        public JointModel model;

        public float3 primary_axis_local;
        public float3 preferred_bend_normal_local;

        public JointBranchPreference branch_preference;
        public float branch_deadband;
        public float max_delta_degrees;

        public JointConstraintData constraint;

        public int sub_chain_index;
    }

    [Serializable]
    public struct JointConstraintData
    {
        public Hinge2DJointData hinge_2d;
        public BallSocketJointData ball_socket;
    }

    [Serializable]
    public struct Hinge2DJointData
    {
        public float3 hinge_axis_local;
        public float3 reference_direction_local;

        public float min_angle_degrees;
        public float max_angle_degrees;

        public byte use_limits;
    }

    [Serializable]
    public struct BallSocketJointData
    {
        public float3 swing_axis_local;
        public float3 reference_direction_local;

        public float max_swing_degrees;

        public float min_twist_degrees;
        public float max_twist_degrees;

        public byte use_swing_limit;
        public byte use_twist_limit;
    }
}
