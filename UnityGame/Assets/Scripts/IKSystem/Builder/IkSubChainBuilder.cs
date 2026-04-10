using UnityEngine;
using IKSystem.Data;
using IKSystem.Builders.Path;

namespace IKSystem.Builders
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("IK/Builder/IK Sub Chain Builder")]
    public sealed class IkSubChainBuilder : MonoBehaviour
    {
        private const float default_tolerance = 0.001f;

        [Header("Hierarchy")]
        [Tooltip("Chain start transform.")]
        public Transform start;

        [Tooltip("Chain end transform.")]
        public Transform end;

        [Header("Topology")]
        [Tooltip("If true, joints are virtual and do not refer to GameObjects.")]
        public bool joints_virtual = true;

        public bool alternating_topology = true;

        [Tooltip("If true, the first transform in the path is a joint. If false, it is a bone.")]
        public bool first_is_joint = false;

        [Header("Joint Defaults")]
        [Tooltip("Default joint model.")]
        public JointModel joint_model = JointModel.Hinge2D;

        [Tooltip("Primary axis in joint local space.")]
        public Vector3 primary_axis = Vector3.right;

        [Tooltip("Preferred bend normal in joint local space.")]
        public Vector3 bend_normal = Vector3.forward;

        [Tooltip("Branch preference.")]
        public JointBranchPreference branch_pref = JointBranchPreference.None;

        [Min(0f)]
        [Tooltip("Branch switch deadband degrees.")]
        public float branch_deadband = 0f;

        [Min(0f)]
        [Tooltip("Max delta degrees per iteration.")]
        public float max_delta = 0f;

        [Header("Bone Defaults")]
        [Tooltip("Compute bone lengths from transform positions.")]
        public bool compute_length = true;

        [Min(0f)]
        [Tooltip("Bone tolerance override.")]
        public float bone_tolerance = 0f;

        [Range(0f, 1f)]
        [Tooltip("0 = parent pinned, 1 = child pinned.")]
        public float bone_weight = 0.5f;

        [Header("Chain Defaults")]
        public ChainPhase phase = ChainPhase.Limbs;
        public ChainRootPolicy root_policy = ChainRootPolicy.FollowParent;
        public ChainAnchorMode anchor_mode = ChainAnchorMode.Dual;
        public ChainPassOrder pass_order = ChainPassOrder.Alternate;

        [Min(1)]
        public int max_iterations = 12;

        [Min(0f)]
        public float tolerance = default_tolerance;

        [Header("Built Data")]
        [SerializeField] private IkSubChain sub_chain;
        [SerializeField] private IkJoint[] joints;
        [SerializeField] private Vector3[] joint_world;
        [SerializeField] private BoneConstraint[] bones;
        [SerializeField] private BoneEndpoints[] bone_world;

        [ContextMenu("Build Sub Chain")]
        private void BuildEditor()
        {
            Build();
        }

        /* Build an IK sub chain from the hierarchy path.
         * @param none
         */
        public void Build()
        {
            IkSubChainBuildSettings settings = new IkSubChainBuildSettings(this);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RecordObject(this, "Build IK Sub Chain");
            }
#endif

            IkSubChainBuildResult result;
            string error_message;
            if (!IkSubChainBuildPipeline.TryBuild(in settings, out result, out error_message))
            {
                Debug.LogWarning(error_message, this);
                return;
            }

            sub_chain = result.sub_chain;
            joints = result.joints;
            joint_world = result.joint_world;
            bones = result.bones;
            bone_world = result.bone_world;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        private void OnValidate()
        {
            if (primary_axis == Vector3.zero)
            {
                primary_axis = Vector3.right;
            }

            if (bend_normal == Vector3.zero)
            {
                bend_normal = Vector3.forward;
            }
        }
    }
}
