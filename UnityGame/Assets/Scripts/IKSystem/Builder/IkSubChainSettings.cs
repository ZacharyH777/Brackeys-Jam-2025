using UnityEngine;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Builders
{
    public readonly struct IkSubChainBuildSettings
    {
        public readonly Transform start;
        public readonly Transform end;

        public readonly bool joints_virtual;
        public readonly bool first_is_joint;
        public readonly bool alternating_topology;

        public readonly JointModel joint_model;
        public readonly float3 primary_axis;
        public readonly float3 bend_normal;
        public readonly JointBranchPreference branch_pref;
        public readonly float branch_deadband;
        public readonly float max_delta;

        public readonly bool compute_length;
        public readonly float bone_tolerance;
        public readonly float bone_weight;

        public readonly ChainPhase phase;
        public readonly ChainRootPolicy root_policy;
        public readonly ChainAnchorMode anchor_mode;
        public readonly ChainPassOrder pass_order;
        public readonly int max_iterations;
        public readonly float tolerance;

        // Constructor maps from the Builder's Definition class
        public IkSubChainBuildSettings(IkChainBuilder.SubChainDefinition def)
        {
            start = def.start;
            end = def.end;

            joints_virtual = def.jointsVirtual;
            first_is_joint = def.firstIsJoint;
            alternating_topology = def.alternatingTopology;

            joint_model = def.jointModel;
            primary_axis = def.primaryAxis;
            bend_normal = def.bendNormal;
            branch_pref = def.branchPref;
            branch_deadband = def.branchDeadband;
            max_delta = def.maxDelta;

            compute_length = def.computeLength;
            bone_tolerance = def.boneTolerance;
            bone_weight = def.boneWeight;

            phase = def.phase;
            root_policy = def.rootPolicy;
            anchor_mode = def.anchorMode;
            pass_order = def.passOrder;
            max_iterations = def.maxIterations;
            tolerance = def.tolerance;
        }
    }
}
