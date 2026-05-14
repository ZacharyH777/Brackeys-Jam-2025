using System;
using Unity.Mathematics;

namespace RunstarSystems.IKSystem.Data
{
    public enum ChainPhase { Limbs = 0, Spine = 1, Tail = 2 }
    public enum ChainRootPolicy { FollowParent = 0, Pinned = 1, Free = 2 }
    public enum ChainAnchorMode { None = 0, Start = 1, End = 2, Dual = 3 }
    public enum ChainPassOrder { Forward = 0, Backward = 1, Alternate = 2 }

    [Serializable]
    public struct IKChainState
    {
        // Transforms
        public float3 StartTargetPos;
        public quaternion StartTargetRot;
        
        public float3 EndTargetPos;
        public quaternion EndTargetRot;

        // Array Mapping
        public int ElementStartIndex;
        public int ElementCount;
        
        // General Controls
        public float SolveWeight;
        public bool ConstraintsEnabled;

        // Solver specific settings mapped from the Builder
        public ChainPhase Phase;
        public ChainRootPolicy RootPolicy;
        public ChainAnchorMode AnchorMode;
        public ChainPassOrder PassOrder;

        public int MaxIterations;
        public float Tolerance;

        public bool alternating_topology;
        public bool first_is_joint;
    }
}
