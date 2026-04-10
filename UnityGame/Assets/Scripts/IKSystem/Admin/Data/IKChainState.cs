using Unity.Mathematics;

namespace RunstarSystems.IKSystem.Data
{
    // This is the data that transfers between unity and the ik system
    public struct IKChainState
    {
        public float3 RootAnchor;     
        public float3 EndEffectorTarget; 
        
        public int BoneStartIndex;
        public int BoneCount;
        
        public float ChainLength;    
        public float Weight; 
    }
}
