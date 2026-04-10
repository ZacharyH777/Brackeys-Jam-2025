using UnityEngine;
using IKSystem.Data;

namespace IKSystem.Builders
{
    public struct IkSubChainBuildResult
    {
        public IkSubChain sub_chain;
        public IkJoint[] joints;
        public Vector3[] joint_world;
        public BoneConstraint[] bones;
        public BoneEndpoints[] bone_world;
    }
}
