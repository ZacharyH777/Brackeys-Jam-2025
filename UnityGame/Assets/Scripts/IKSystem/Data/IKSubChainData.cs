using System;
using UnityEngine;

namespace IKSystem.Data
{
    public enum ChainPhase
    {
        Core = 0, Limbs = 1, Fingers = 2
    }

    public enum ChainRootPolicy
    {
        Pinned = 0, FollowParent = 1, Weighted = 2
    }

    public enum ChainAnchorMode
    {
        RootOnly = 0, TipOnly = 1, Dual = 2
    }

    public enum ChainPassOrder
    {
        RootFirst = 0, TipFirst = 1, Alternate = 2, Adaptive = 3
    }

    [Serializable]
    public struct IkSubChain
    {
        private const float zero_float = 0f;

        [SerializeField]
        [Min(0)]
        [Tooltip("Sub chain start joint index.")]
        private int start_joint_index;

        [SerializeField]
        [Min(0)]
        [Tooltip("Sub chain end joint index.")]
        private int end_joint_index;

        [SerializeField]
        [Tooltip("Solve phase.")]
        private ChainPhase phase;

        [SerializeField]
        [Tooltip("Root handling policy.")]
        private ChainRootPolicy root_policy;

        [SerializeField]
        [Tooltip("Anchored ends.")]
        private ChainAnchorMode anchor_mode;

        [SerializeField]
        [Tooltip("Pass order policy.")]
        private ChainPassOrder pass_order;

        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum iterations.")]
        private int max_iterations;

        [SerializeField]
        [Min(0)]
        [Tooltip("Stop tolerance.")]
        private float tolerance;

        public int StartJointIndex
        {
            get
            {
                return start_joint_index;
            }
            set
            {
                int clamped_value = clamp_min_int(value);
                start_joint_index = clamped_value;
            }
        }

        public int EndJointIndex
        {
            get
            {
                return end_joint_index;
            }
            set
            {
                int clamped_value = clamp_min_int(value);
                end_joint_index = clamped_value;
            }
        }

        public void SetStartKeepRange(int value)
        {
            int clamped_value = clamp_min_int(value);
            start_joint_index = clamped_value;

            if (end_joint_index < start_joint_index)
            {
                end_joint_index = start_joint_index;
            }
        }

        public void SetEndKeepRange(int value)
        {
            int clamped_value = clamp_min_int(value);
            end_joint_index = clamped_value;

            if (end_joint_index < start_joint_index)
            {
                start_joint_index = end_joint_index;
            }
        }

        public ChainPhase Phase
        {
            get
            {
                return phase;
            }
            set
            {
                phase = clamp_phase(value);
            }
        }

        public ChainRootPolicy RootPolicy
        {
            get
            {
                return root_policy;
            }
            set
            {
                root_policy = clamp_root_policy(value);
            }
        }

        public ChainAnchorMode AnchorMode
        {
            get
            {
                return anchor_mode;
            }
            set
            {
                anchor_mode = clamp_anchor_mode(value);
            }
        }

        public ChainPassOrder PassOrder
        {
            get
            {
                return pass_order;
            }
            set
            {
                pass_order = clamp_pass_order(value);
            }
        }

        public int MaxIterations
        {
            get
            {
                return max_iterations;
            }
            set
            {
                max_iterations = clamp_min_one_int(value);
            }
        }

        public float Tolerance
        {
            get
            {
                return tolerance;
            }
            set
            {
                tolerance = clamp_min_float(value);
            }
        }

        public int JointCount
        {
            get
            {
                if (end_joint_index < start_joint_index)
                {
                    return 0;
                }
                return (end_joint_index - start_joint_index) + 1;
            }
        }

        public bool IsValidRange
        {
            get
            {
                return end_joint_index >= start_joint_index;
            }
        }

        public bool HasStopTolerance
        {
            get
            {
                return tolerance > zero_float;
            }
        }

        /* Purpose: Provide a non-zero tolerance when authoring sets it to 0. */
        public float SafeTolerance(float fallback_tolerance)
        {
            float authored_tolerance = tolerance;
            if (authored_tolerance > zero_float)
            {
                return authored_tolerance;
            }

            return clamp_min_float(fallback_tolerance);
        }

        public bool HasRootAnchor
        {
            get
            {
                if (anchor_mode == ChainAnchorMode.RootOnly)
                {
                    return true;
                }
                if (anchor_mode == ChainAnchorMode.Dual)
                {
                    return true;
                }
                return false;
            }
        }

        public bool HasTipAnchor
        {
            get
            {
                if (anchor_mode == ChainAnchorMode.TipOnly)
                {
                    return true;
                }
                if (anchor_mode == ChainAnchorMode.Dual)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsDualAnchored
        {
            get
            {
                return anchor_mode == ChainAnchorMode.Dual;
            }
        }

        private static int clamp_min_int(int value)
        {
            if (value < 0)
            {
                return 0;
            }
            return value;
        }

        private static int clamp_min_one_int(int value)
        {
            if (value < 1)
            {
                return 1;
            }
            return value;
        }

        private static float clamp_min_float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return zero_float;
            }
            if (value < zero_float)
            {
                return zero_float;
            }
            return value;
        }

        private static ChainPhase clamp_phase(ChainPhase value)
        {
            int raw_value = (int)value;
            if (raw_value < (int)ChainPhase.Core)
            {
                return ChainPhase.Core;
            }
            if (raw_value > (int)ChainPhase.Fingers)
            {
                return ChainPhase.Fingers;
            }
            return value;
        }

        private static ChainRootPolicy clamp_root_policy(ChainRootPolicy value)
        {
            int raw_value = (int)value;
            if (raw_value < (int)ChainRootPolicy.Pinned)
            {
                return ChainRootPolicy.Pinned;
            }
            if (raw_value > (int)ChainRootPolicy.Weighted)
            {
                return ChainRootPolicy.Weighted;
            }
            return value;
        }

        private static ChainAnchorMode clamp_anchor_mode(ChainAnchorMode value)
        {
            int raw_value = (int)value;
            if (raw_value < (int)ChainAnchorMode.RootOnly)
            {
                return ChainAnchorMode.RootOnly;
            }
            if (raw_value > (int)ChainAnchorMode.Dual)
            {
                return ChainAnchorMode.Dual;
            }
            return value;
        }

        private static ChainPassOrder clamp_pass_order(ChainPassOrder value)
        {
            int raw_value = (int)value;
            if (raw_value < (int)ChainPassOrder.RootFirst)
            {
                return ChainPassOrder.RootFirst;
            }
            if (raw_value > (int)ChainPassOrder.Adaptive)
            {
                return ChainPassOrder.Adaptive;
            }
            return value;
        }
    }
}
