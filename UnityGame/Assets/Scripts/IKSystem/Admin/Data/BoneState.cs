using System;
using Unity.Mathematics;
using Unity.Collections;

namespace RunstarSystems.IKSystem.Data
{
    // Keeps track of changing variables
    [Serializable]
    public struct BoneState
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 LocalScale; 
    }

    // Should only be changed during build/restart/etc
    [Serializable]
    public struct BoneStaticData
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public FixedString64Bytes Name;
#endif
        public int ParentJointIndex;
        public int ChildJointIndex;
        public float RestLength;
        public float Weight;
        public float Tolerance;
    }
}
