using System;
using System.Collections.Generic;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Data
{
    [Serializable]
    public class IkSubChainData
    {
        public string name = "New Sub Chain";
        
        // Settings for this specific sub-chain
        public IKChainState chainState;

        // Lists for the Job System (one subchain per job)
        public List<JointState> jointStates = new();
        public List<JointStaticData> jointStatics = new();
        
        // same as joints, one per job
        public List<BoneState> boneStates = new();
        public List<BoneStaticData> boneStatics = new();
    }
}
