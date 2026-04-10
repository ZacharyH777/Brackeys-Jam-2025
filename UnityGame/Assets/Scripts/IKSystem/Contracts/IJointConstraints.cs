using IKSystem.Data;
using UnityEngine;
using System.Diagnostics;

namespace IKSystem.Contracts
{
    public enum ChainSolveDirection
    {
        RootToTip = 0,
        TipToRoot = 1
    }

    public enum JointConstraintResult
    {
        Applied = 0,
        Skipped = 1,
        InvalidInput = 2
    }

    public interface IJointConstraints
    {

        JointConstraintResult Constrain_Joint_Step(
            in IkJoint joint,
            ref IkJointConstraintState joint_state,
            ChainSolveDirection solve_direction,
            ChainAnchorMode anchor_mode,
            Vector3 parent_world,
            Vector3 joint_world,
            ref Vector3 proposed_joint_world,
            Vector3 child_world,
            float epsilon);
    }
    public struct IkJointConstraintState
    {
        public int last_branch;
        public Vector3 last_direction_world;
        public bool has_last_direction;

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Debug_Assert_Finite(string label)
        {
            if (!float.IsFinite(last_direction_world.x) ||
                !float.IsFinite(last_direction_world.y) ||
                !float.IsFinite(last_direction_world.z))
            {
                UnityEngine.Debug.LogWarning(label + " joint state direction was not finite.");
            }
        }
    }
}
