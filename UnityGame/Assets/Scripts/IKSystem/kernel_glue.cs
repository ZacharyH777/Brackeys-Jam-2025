using Unity.Collections;
using UnityEngine;
using IKSystem.Data;

namespace IKSystem.Core

{

    public struct IkRigMemory : System.IDisposable

    {

        public NativeArray<Vector3> Positions;
        public NativeArray<Quaternion> Rotations;
        public NativeArray<IkJoint> Joints;
        public NativeArray<BoneConstraint> Bones;
        public Transform[] SyncTransforms;

        public void Dispose()
        {

            if (Positions.IsCreated)
                Positions.Dispose();

            if (Rotations.IsCreated)
                Rotations.Dispose();

            if (Joints.IsCreated)
                Joints.Dispose();

            if (Bones.IsCreated)
                Bones.Dispose();
        }

    }

}
