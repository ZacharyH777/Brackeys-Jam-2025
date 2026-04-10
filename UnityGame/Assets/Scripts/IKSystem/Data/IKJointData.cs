using System;
using UnityEngine;

namespace IKSystem.Data
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
    public struct IkJoint
    {
        private const float axis_epsilon = 1e-8f;
        private const float degrees_max_reasonable = 360f;

        [SerializeField]
        [Min(0)]
        [Tooltip("Global joint index.")]
        private int joint_index;

        [SerializeField]
        [Tooltip("Joint source kind.")]
        private JointKind kind;

        [SerializeField]
        [Tooltip("Optional pivot transform.")]
        private Transform pivot_transform;

        [SerializeField]
        [Tooltip("Constraint model.")]
        private JointModel model;

        [SerializeField]
        [Tooltip("Primary axis in local space.")]
        private Vector3 primary_axis_local;

        [SerializeField]
        [Tooltip("Preferred bend normal in local space.")]
        private Vector3 preferred_bend_normal_local;

        [SerializeField]
        [Tooltip("Branch preference when solutions are ambiguous.")]
        private JointBranchPreference branch_preference;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Branch switch deadband in degrees.")]
        private float branch_switch_deadband_degrees;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Max delta in degrees per iteration.")]
        private float max_delta_degrees;

        public int JointIndex
        {
            get { return joint_index; }
            set { joint_index = clamp_min_int(value); }
        }

        public JointKind Kind
        {
            get { return kind; }
            set { kind = clamp_joint_kind(value); }
        }

        public Transform PivotTransform
        {
            get { return pivot_transform; }
            set { pivot_transform = value; }
        }

        public JointModel Model
        {
            get { return model; }
            set { model = clamp_joint_model(value); }
        }

        public Vector3 PrimaryAxisLocal
        {
            get { return primary_axis_local; }
            set { primary_axis_local = clamp_axis(value, Vector3.right); }
        }

        public Vector3 PreferredBendNormalLocal
        {
            get { return preferred_bend_normal_local; }
            set { preferred_bend_normal_local = clamp_axis(value, Vector3.forward); }
        }

        public JointBranchPreference BranchPreference
        {
            get { return branch_preference; }
            set { branch_preference = clamp_branch_preference(value); }
        }

        public float BranchSwitchDeadbandDegrees
        {
            get { return branch_switch_deadband_degrees; }
            set { branch_switch_deadband_degrees = clamp_degrees(value); }
        }

        public float MaxDeltaDegrees
        {
            get { return max_delta_degrees; }
            set { max_delta_degrees = clamp_degrees(value); }
        }

        public bool HasPivot
        {
            get { return pivot_transform != null; }
        }

        public bool HasPreferredBendNormal
        {
            get { return preferred_bend_normal_local.sqrMagnitude > axis_epsilon * axis_epsilon; }
        }

        private static int clamp_min_int(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value;
        }

        private static float clamp_degrees(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            if (value < 0f)
            {
                return 0f;
            }

            if (value > degrees_max_reasonable)
            {
                return degrees_max_reasonable;
            }

            return value;
        }

        private static Vector3 clamp_axis(Vector3 value, Vector3 fallback_axis)
        {
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.z))
            {
                return fallback_axis;
            }

            float m = value.magnitude;
            if (m < axis_epsilon)
            {
                return fallback_axis;
            }

            return value / m;
        }

        private static JointKind clamp_joint_kind(JointKind value)
        {
            int v = (int)value;
            if (v < (int)JointKind.Virtual)
            {
                return JointKind.Virtual;
            }
            if (v > (int)JointKind.Transform)
            {
                return JointKind.Transform;
            }
            return value;
        }

        private static JointModel clamp_joint_model(JointModel value)
        {
            int v = (int)value;
            if (v < (int)JointModel.Free)
            {
                return JointModel.Free;
            }
            if (v > (int)JointModel.BallSocket)
            {
                return JointModel.BallSocket;
            }
            return value;
        }

        private static JointBranchPreference clamp_branch_preference(JointBranchPreference value)
        {
            int v = (int)value;
            if (v < (int)JointBranchPreference.None)
            {
                return JointBranchPreference.None;
            }
            if (v > (int)JointBranchPreference.BranchB)
            {
                return JointBranchPreference.BranchB;
            }
            return value;
        }
    }
}
