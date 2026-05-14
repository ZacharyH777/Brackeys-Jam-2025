using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using RunstarSystems.IKSystem.Data;

namespace RunstarSystems.IKSystem.Builders
{
    [ExecuteAlways]
    [AddComponentMenu("Runstar/IK/IK Chain Builder")]
    public sealed class IkChainBuilder : MonoBehaviour
    {
        private const float default_tolerance = 0.001f;
        private const float position_error_tolerance = 0.001f;
        private const float length_error_tolerance = 0.001f;

        [Header("Definitions")]
        public List<SubChainDefinition> definitions = new List<SubChainDefinition>();

        [Header("Baked Data")]
        public List<IkSubChainData> subChains = new List<IkSubChainData>();

        [Header("Transform Registry")]
        [Tooltip("A single deduplicated list of every transform used in the IK rig")]
        public List<Transform> uniqueTransforms = new List<Transform>();

        [Tooltip("Maps each sub chain element one to one with uniqueTransforms")]
        public List<SubChainTransformMap> subChainTransformMaps = new List<SubChainTransformMap>();

        [Header("Debug View")]
        public bool showDebug = true;
        public bool use3DMode = true;

        [Range(0.01f, 0.5f)]
        public float nodeSize = 0.05f;

        private readonly Dictionary<Transform, int> transform_lookup = new Dictionary<Transform, int>();
        private readonly HashSet<Transform> chain_transform_check = new HashSet<Transform>();

        /*
         * Builds every IK sub chain and validates the generated data.
         */
        [ContextMenu("Build All Chains")]
        public void Build()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RecordObject(this, "Build IK Sub Chains");
            }
#endif

            subChains.Clear();
            uniqueTransforms.Clear();
            subChainTransformMaps.Clear();
            transform_lookup.Clear();

            if (definitions == null)
            {
                Debug.LogError("IK Build Failed: Definitions list is null.", this);
                return;
            }

            for (int definition_index = 0; definition_index < definitions.Count; definition_index++)
            {
                SubChainDefinition definition = definitions[definition_index];

                if (!ValidateDefinition(definition, definition_index))
                {
                    continue;
                }

                IkSubChainBuildSettings settings = new IkSubChainBuildSettings(definition);

                IkSubChainData data;
                Transform[] path;
                string error_message;

                bool built = IkSubChainBuildPipeline.TryBuild(
                    definition.name,
                    definition_index,
                    in settings,
                    out data,
                    out path,
                    out error_message);

                if (!built)
                {
                    Debug.LogError("IK Build Failed for '" + definition.name + "': " + error_message, this);
                    continue;
                }

                if (!ValidateBuiltData(definition, data, path))
                {
                    continue;
                }

                IKChainState state = data.chainState;
                state.alternating_topology = definition.alternatingTopology;
                state.first_is_joint = definition.firstIsJoint;
                data.chainState = state;

                if (!ValidateTopology(definition, data, path))
                {
                    continue;
                }

                if (!ValidateMathematics(definition, data, path))
                {
                    continue;
                }

                SubChainTransformMap map = BuildTransformMap(definition, path);

                if (!ValidateTransformMap(definition, data, map))
                {
                    continue;
                }

                subChains.Add(data);
                subChainTransformMaps.Add(map);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif

            Debug.Log("IK Build Complete: " + subChains.Count + " chains, " + uniqueTransforms.Count + " unique transforms.", this);
        }

        /*
         * Validates a user authored chain definition.
         *
         * @param definition Chain definition to validate.
         * @param definition_index Index inside the definitions list.
         */
        private bool ValidateDefinition(SubChainDefinition definition, int definition_index)
        {
            if (definition == null)
            {
                Debug.LogError("IK Build Failed: Definition at index " + definition_index + " is null.", this);
                return false;
            }

            if (definition.start == null)
            {
                Debug.LogWarning("IK Build Warning: Sub chain '" + definition.name + "' is missing Start.", this);
                return false;
            }

            if (definition.end == null)
            {
                Debug.LogWarning("IK Build Warning: Sub chain '" + definition.name + "' is missing End.", this);
                return false;
            }

            if (definition.start == definition.end)
            {
                Debug.LogError("IK Build Failed: Sub chain '" + definition.name + "' has the same Start and End transform.", this);
                return false;
            }

            if (definition.maxIterations < 1)
            {
                Debug.LogError("IK Build Failed: Sub chain '" + definition.name + "' must have at least one iteration.", this);
                return false;
            }

            if (definition.tolerance < 0f)
            {
                Debug.LogError("IK Build Failed: Sub chain '" + definition.name + "' has negative tolerance.", this);
                return false;
            }

            return true;
        }

        /*
         * Counts joint elements in a chain path.
         *
         * @param element_count Number of transforms in the path.
         * @param alternating_topology Whether topology alternates.
         * @param first_is_joint Whether element zero is a joint.
         */
        private int CountJointElements(int element_count, bool alternating_topology, bool first_is_joint)
        {
            int joint_count = 0;

            for (int element_index = 0; element_index < element_count; element_index++)
            {
                bool is_joint = IsJointElement(
                    element_index,
                    alternating_topology,
                    first_is_joint);

                if (is_joint)
                {
                    joint_count++;
                }
            }

            return joint_count;
        }

        /*
         * Validates the basic shape of generated chain data.
         *
         * @param definition Source chain definition.
         * @param data Generated chain data.
         * @param path Generated transform path.
         */
        private bool ValidateBuiltData(SubChainDefinition definition, IkSubChainData data, Transform[] path)
        {
            if (path == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' returned a null path.", this);
                return false;
            }

            if (path.Length < 2)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' path must have at least two transforms.", this);
                return false;
            }

            if (data.jointStates == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' returned null jointStates.", this);
                return false;
            }

            if (data.jointStatics == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' returned null jointStatics.", this);
                return false;
            }

            if (data.boneStatics == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' returned null boneStatics.", this);
                return false;
            }

            int expected_joint_count = CountJointElements(
                path.Length,
                definition.alternatingTopology,
                definition.firstIsJoint);

            int expected_bone_count = path.Length - expected_joint_count;

            if (data.jointStates.Count != expected_joint_count)
            {
                Debug.LogError(
                    "IK Build Mismatch: '" + definition.name + "' jointStates (" + data.jointStates.Count +
                    ") must equal joint elements in path (" + expected_joint_count +
                    "), not full path length (" + path.Length + ").",
                    this);

                return false;
            }

            if (data.jointStatics.Count != expected_joint_count)
            {
                Debug.LogError(
                    "IK Build Mismatch: '" + definition.name + "' jointStatics (" + data.jointStatics.Count +
                    ") must equal joint elements in path (" + expected_joint_count + ").",
                    this);

                return false;
            }

            if (data.boneStatics.Count < expected_bone_count)
            {
                Debug.LogError(
                    "IK Build Mismatch: '" + definition.name + "' boneStatics (" + data.boneStatics.Count +
                    ") must be at least bone elements in path (" + expected_bone_count + ").",
                    this);

                return false;
            }

            chain_transform_check.Clear();

            for (int path_index = 0; path_index < path.Length; path_index++)
            {
                Transform path_transform = path[path_index];

                if (path_transform == null)
                {
                    Debug.LogError("IK Build Failed: '" + definition.name + "' has a null transform at path index " + path_index + ".", this);
                    return false;
                }

                if (chain_transform_check.Contains(path_transform))
                {
                    Debug.LogError(
                        "IK Build Failed: '" + definition.name + "' contains duplicate transform '" + path_transform.name + "' inside the same path.",
                        this);

                    return false;
                }

                chain_transform_check.Add(path_transform);
            }

            return true;
        }

        /*
         * Validates topology settings against the generated element path.
         *
         * @param definition Source chain definition.
         * @param data Generated chain data.
         * @param path Generated transform path.
         */
        private bool ValidateTopology(SubChainDefinition definition, IkSubChainData data, Transform[] path)
        {
            int joint_count = 0;
            int bone_count = 0;

            for (int element_index = 0; element_index < path.Length; element_index++)
            {
                bool is_joint = IsJointElement(
                    element_index,
                    definition.alternatingTopology,
                    definition.firstIsJoint);

                if (is_joint)
                {
                    joint_count++;
                }
                else
                {
                    bone_count++;
                }
            }

            if (joint_count == 0)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' topology produced zero joint elements.", this);
                return false;
            }

            if (definition.alternatingTopology && bone_count == 0)
            {
                Debug.LogWarning("IK Build Warning: '" + definition.name + "' uses alternating topology but produced zero bone elements.", this);
            }

            if (definition.alternatingTopology && path.Length < 3)
            {
                Debug.LogWarning("IK Build Warning: '" + definition.name + "' alternating topology works best with at least three elements.", this);
            }

            return true;
        }

        /*
         * Validates generated positions and segment lengths against the transform path.
         *
         * @param definition Source chain definition.
         * @param data Generated chain data.
         * @param path Generated transform path.
         */
        private bool ValidateMathematics(SubChainDefinition definition, IkSubChainData data, Transform[] path)
        {
            int joint_state_index = 0;
            int bone_static_index = 0;

            for (int element_index = 0; element_index < path.Length; element_index++)
            {
                bool is_joint = IsJointElement(
                    element_index,
                    definition.alternatingTopology,
                    definition.firstIsJoint);

                if (is_joint)
                {
                    if (joint_state_index >= data.jointStates.Count)
                    {
                        Debug.LogError(
                            "IK Build Failed: '" + definition.name + "' expected jointState for joint element " +
                            element_index + ", but jointState index " + joint_state_index +
                            " is outside jointStates count " + data.jointStates.Count + ".",
                            this);

                        return false;
                    }

                    float3 baked_position = data.jointStates[joint_state_index].world_position;
                    float3 transform_position = path[element_index].position;

                    float position_error = math.distance(baked_position, transform_position);

                    if (position_error > position_error_tolerance)
                    {
                        Debug.LogWarning(
                            "IK Build Warning: '" + definition.name +
                            "' baked joint position differs from transform position at element " +
                            element_index + ". Error " + position_error + ".",
                            this);
                    }

                    joint_state_index++;
                }
            }

            for (int element_index = 0; element_index < path.Length - 1; element_index++)
            {
                bool current_is_joint = IsJointElement(
                    element_index,
                    definition.alternatingTopology,
                    definition.firstIsJoint);

                bool current_is_bone = !current_is_joint;

                float3 current_position = path[element_index].position;
                float3 child_position = path[element_index + 1].position;

                float measured_length = math.distance(current_position, child_position);

                if (measured_length <= 0.000001f)
                {
                    Debug.Log(
                        "IK Build Info: '" + definition.name +
                        "' has zero length hierarchy segment at element " + element_index +
                        ". This is allowed for pivots or control transforms.",
                        this);
                }

                if (!current_is_bone)
                {
                    continue;
                }

                if (bone_static_index >= data.boneStatics.Count)
                {
                    Debug.LogError(
                        "IK Build Failed: '" + definition.name +
                        "' expected boneStatic for bone element " + element_index +
                        ", but boneStatic index " + bone_static_index +
                        " is outside boneStatics count " + data.boneStatics.Count + ".",
                        this);

                    return false;
                }

                BoneStaticData static_data = data.boneStatics[bone_static_index];
                float baked_length = static_data.RestLength;

                if (baked_length < 0f)
                {
                    Debug.LogError(
                        "IK Build Failed: '" + definition.name +
                        "' has negative RestLength at bone element " + element_index + ".",
                        this);

                    return false;
                }

                if (baked_length <= 0.000001f)
                {
                    Debug.Log(
                        "IK Build Info: '" + definition.name +
                        "' has zero RestLength at bone element " + element_index +
                        ". This is allowed for pivots or non-distance control bones.",
                        this);
                }
                else
                {
                    float length_error = math.abs(measured_length - baked_length);

                    if (length_error > length_error_tolerance)
                    {
                        Debug.LogWarning(
                            "IK Build Warning: '" + definition.name +
                            "' RestLength differs from transform distance at bone element " +
                            element_index + ". Baked " + baked_length +
                            ", measured " + measured_length + ".",
                            this);
                    }
                }

                bool has_parent_joint = static_data.ParentJointIndex >= 0;
                bool has_child_joint = static_data.ChildJointIndex >= 0;

                if (!has_parent_joint || !has_child_joint)
                {
                    Debug.Log(
                        "IK Build Info: '" + definition.name +
                        "' bone static at bone element " + element_index +
                        " is not a joint-to-joint segment. ParentJointIndex " +
                        static_data.ParentJointIndex + ", ChildJointIndex " +
                        static_data.ChildJointIndex + ".",
                        this);
                }

                bone_static_index++;
            }

            if (joint_state_index != data.jointStates.Count)
            {
                Debug.LogWarning(
                    "IK Build Warning: '" + definition.name +
                    "' consumed " + joint_state_index +
                    " jointStates, but data contains " + data.jointStates.Count + ".",
                    this);
            }

            if (bone_static_index > data.boneStatics.Count)
            {
                Debug.LogError(
                    "IK Build Failed: '" + definition.name +
                    "' consumed more boneStatics than available.",
                    this);

                return false;
            }

            return true;
        }

        /*
         * Builds one to one element transform mapping and data lookup mapping.
         *
         * @param definition Source chain definition.
         * @param path Generated transform path.
         */
        private SubChainTransformMap BuildTransformMap(SubChainDefinition definition, Transform[] path)
        {
            SubChainTransformMap map = new SubChainTransformMap();

            map.transformIndices.Capacity = path.Length;
            map.jointStateIndices.Capacity = path.Length;
            map.boneStaticIndices.Capacity = path.Length;

            int joint_state_index = 0;
            int bone_static_index = 0;

            for (int path_index = 0; path_index < path.Length; path_index++)
            {
                Transform path_transform = path[path_index];

                int unique_index;

                if (!transform_lookup.TryGetValue(path_transform, out unique_index))
                {
                    unique_index = uniqueTransforms.Count;
                    uniqueTransforms.Add(path_transform);
                    transform_lookup.Add(path_transform, unique_index);
                }

                map.transformIndices.Add(unique_index);

                bool is_joint = IsJointElement(
                    path_index,
                    definition.alternatingTopology,
                    definition.firstIsJoint);

                if (is_joint)
                {
                    map.jointStateIndices.Add(joint_state_index);
                    map.boneStaticIndices.Add(-1);
                    joint_state_index++;
                }
                else
                {
                    map.jointStateIndices.Add(-1);
                    map.boneStaticIndices.Add(bone_static_index);
                    bone_static_index++;
                }
            }

            return map;
        }

        /*
         * Validates the final map produced for a chain.
         *
         * @param definition Source chain definition.
         * @param data Generated chain data.
         * @param map Generated transform map.
         */
        private bool ValidateTransformMap(SubChainDefinition definition, IkSubChainData data, SubChainTransformMap map)
        {
            if (map == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' produced null transform map.", this);
                return false;
            }

            if (map.transformIndices == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' produced null transform index list.", this);
                return false;
            }

            if (map.jointStateIndices == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' produced null joint state index list.", this);
                return false;
            }

            if (map.boneStaticIndices == null)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' produced null bone static index list.", this);
                return false;
            }

            int element_count = map.transformIndices.Count;

            if (map.jointStateIndices.Count != element_count)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' joint state map count does not match transform map count.", this);
                return false;
            }

            if (map.boneStaticIndices.Count != element_count)
            {
                Debug.LogError("IK Build Failed: '" + definition.name + "' bone static map count does not match transform map count.", this);
                return false;
            }

            for (int element_index = 0; element_index < element_count; element_index++)
            {
                int unique_index = map.transformIndices[element_index];

                if (unique_index < 0)
                {
                    Debug.LogError("IK Build Failed: '" + definition.name + "' has negative transform index at element " + element_index + ".", this);
                    return false;
                }

                if (unique_index >= uniqueTransforms.Count)
                {
                    Debug.LogError("IK Build Failed: '" + definition.name + "' transform index is outside uniqueTransforms at element " + element_index + ".", this);
                    return false;
                }

                int joint_state_index = map.jointStateIndices[element_index];

                if (joint_state_index >= data.jointStates.Count)
                {
                    Debug.LogError("IK Build Failed: '" + definition.name + "' joint state index is outside jointStates at element " + element_index + ".", this);
                    return false;
                }

                int bone_static_index = map.boneStaticIndices[element_index];

                if (bone_static_index >= data.boneStatics.Count)
                {
                    Debug.LogError("IK Build Failed: '" + definition.name + "' bone static index is outside boneStatics at element " + element_index + ".", this);
                    return false;
                }
            }

            return true;
        }

        /*
         * Returns whether an element index is a joint according to topology settings.
         *
         * @param element_index Index inside the chain path.
         * @param alternating_topology Whether topology alternates.
         * @param first_is_joint Whether element zero is a joint.
         */
        private bool IsJointElement(int element_index, bool alternating_topology, bool first_is_joint)
        {
            if (!alternating_topology)
            {
                return true;
            }

            bool even_index = element_index % 2 == 0;
            bool is_joint = even_index == first_is_joint;

            return is_joint;
        }

        /*
         * Draws chain gizmos in the scene view.
         */
        private void OnDrawGizmos()
        {
            if (!showDebug)
            {
                return;
            }

            if (subChains == null)
            {
                return;
            }

            if (subChains.Count == 0)
            {
                return;
            }

            int color_segments = subChains.Count + 1;

            for (int chain_index = 1; chain_index < color_segments; chain_index++)
            {
                float hue = (float)chain_index / color_segments;
                Gizmos.color = Color.HSVToRGB(hue, 0.8f, 1.0f);

                DrawChainVisuals(subChains[chain_index - 1]);
            }
        }

        /*
         * Draws debug nodes and segments for one chain.
         *
         * @param chain Chain data to draw.
         */
        private void DrawChainVisuals(IkSubChainData chain)
        {
            if (chain.jointStates == null)
            {
                return;
            }

            if (chain.jointStates.Count == 0)
            {
                return;
            }

            for (int joint_index = 0; joint_index < chain.jointStates.Count; joint_index++)
            {
                float3 position = chain.jointStates[joint_index].world_position;

                if (use3DMode)
                {
                    if (joint_index == chain.jointStates.Count - 1)
                    {
                        Gizmos.DrawWireCube(position, Vector3.one * nodeSize);
                    }
                    else
                    {
                        Gizmos.DrawWireSphere(position, nodeSize);
                    }
                }
#if UNITY_EDITOR
                else
                {
                    UnityEditor.Handles.color = Gizmos.color;
                    UnityEditor.Handles.DrawWireDisc(position, Vector3.forward, nodeSize);
                }
#endif
            }

            for (int joint_index = 0; joint_index < chain.jointStates.Count - 1; joint_index++)
            {
                Vector3 start_position = chain.jointStates[joint_index].world_position;
                Vector3 end_position = chain.jointStates[joint_index + 1].world_position;

                Gizmos.DrawLine(start_position, end_position);
            }
        }

        [System.Serializable]
        public class SubChainDefinition
        {
            public string name = "New Sub Chain";

            [Header("Hierarchy")]
            public Transform start;
            public Transform end;

            [Header("Topology")]
            public bool jointsVirtual = true;
            public bool alternatingTopology = true;
            public bool firstIsJoint = false;

            [Header("Joint Defaults")]
            public JointModel jointModel = JointModel.Hinge2D;
            public Vector3 primaryAxis = Vector3.right;
            public Vector3 bendNormal = Vector3.forward;
            public JointBranchPreference branchPref = JointBranchPreference.None;
            public float branchDeadband = 0f;
            public float maxDelta = 0f;

            [Header("Hinge Limits")]
            public bool hingeUseLimits = false;
            public Vector3 hingeAxisLocal = Vector3.forward;
            public Vector3 hingeReferenceDirectionLocal = Vector3.right;
            public float hingeMinAngleDegrees = -180f;
            public float hingeMaxAngleDegrees = 180f;

            [Header("Ball Socket Limits")]
            public bool ballSocketUseSwingLimit = false;
            public bool ballSocketUseTwistLimit = false;
            public Vector3 ballSocketSwingAxisLocal = Vector3.right;
            public Vector3 ballSocketReferenceDirectionLocal = Vector3.forward;
            public float ballSocketMaxSwingDegrees = 180f;
            public float ballSocketMinTwistDegrees = -180f;
            public float ballSocketMaxTwistDegrees = 180f;

            [Header("Bone Defaults")]
            public bool computeLength = true;
            public float boneTolerance = 0f;

            [Range(0f, 1f)]
            public float boneWeight = 0.5f;

            [Header("Chain Defaults")]
            public ChainPhase phase = ChainPhase.Limbs;
            public ChainRootPolicy rootPolicy = ChainRootPolicy.FollowParent;
            public ChainAnchorMode anchorMode = ChainAnchorMode.Dual;
            public ChainPassOrder passOrder = ChainPassOrder.Alternate;

            [Min(1)]
            public int maxIterations = 12;

            [Min(0f)]
            public float tolerance = default_tolerance;
        }
    }

    [System.Serializable]
    public class SubChainTransformMap
    {
        [Tooltip("One to one mapping with chain path elements")]
        public List<int> transformIndices = new List<int>();

        [Tooltip("One to one with chain path elements. Bone elements use -1")]
        public List<int> jointStateIndices = new List<int>();

        [Tooltip("One to one with chain path elements. Joint elements use -1")]
        public List<int> boneStaticIndices = new List<int>();
    }
}
