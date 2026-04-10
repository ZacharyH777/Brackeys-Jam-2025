using UnityEngine;
using IKSystem.Data;

namespace IKSystem.Builders
{
    public readonly struct IkSubChainBuildSettings
    {
        public readonly Transform start;
        public readonly Transform end;

        public readonly bool joints_virtual;
        public readonly bool first_is_joint;

        public readonly JointModel joint_model;
        public readonly Vector3 primary_axis;
        public readonly Vector3 bend_normal;
        public readonly JointBranchPreference branch_pref;
        public readonly float branch_deadband;
        public readonly float max_delta;
        public readonly bool alternating_topology;

        public readonly bool compute_length;
        public readonly float bone_tolerance;
        public readonly float bone_weight;

        public readonly ChainPhase phase;
        public readonly ChainRootPolicy root_policy;
        public readonly ChainAnchorMode anchor_mode;
        public readonly ChainPassOrder pass_order;
        public readonly int max_iterations;
        public readonly float tolerance;

        public IkSubChainBuildSettings(IkSubChainBuilder builder)
        {
            start = builder.start;
            end = builder.end;

            joints_virtual = builder.joints_virtual;
            first_is_joint = builder.first_is_joint;
            alternating_topology = builder.alternating_topology;

            joint_model = builder.joint_model;
            primary_axis = builder.primary_axis;
            bend_normal = builder.bend_normal;
            branch_pref = builder.branch_pref;
            branch_deadband = builder.branch_deadband;
            max_delta = builder.max_delta;

            compute_length = builder.compute_length;
            bone_tolerance = builder.bone_tolerance;
            bone_weight = builder.bone_weight;

            phase = builder.phase;
            root_policy = builder.root_policy;
            anchor_mode = builder.anchor_mode;
            pass_order = builder.pass_order;
            max_iterations = builder.max_iterations;
            tolerance = builder.tolerance;
        }

    }
}
