using IKSystem.Data;
using UnityEngine;

namespace IKSystem.Contracts
{
    public interface ISkeletalConstraints
    {
        void Bone_Length(
            ref BoneEndpoints joints,
            in BoneConstraint constraint,
            Vector3 fallback_direction
        );
    }
}
