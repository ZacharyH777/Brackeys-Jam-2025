using System;
using System.Collections.Generic;
using UnityEngine;
using ik_data;

/*
* We want to execute this from the Editor not runtime.
* Then Serialize it.
*/
[ExecuteAlways]

/*
* We will be storing the data in a seperate chain component
*/
[DisallowMultipleComponent]

[AddComponentMenu("IK/Builder/IK Chain Builder")]
public sealed class IkChainBuilder : MonoBehaviour
{
    // This tells us whether we are animating the joints or just bones
    public enum JointSource
    {
        VirtualFromBonesOnly,
        FromHierarchyAlternating
    }

    private const int DEFAULT_PATH_CAPACITY = 32;

    [Header("Hierarchy")]
    [Tooltip("This is the start of the ik chain. If null, this GameObject's transform is used.")]
    public Transform root;

    [Tooltip("The is the leaf or end of the ik chain. If null, the builder finds the deepest child.")]
    public Transform endEffector;

    [Header("Build Options")]
    [Tooltip("Are bones only being animated or are joints?")]
    public JointSource jointSource = JointSource.VirtualFromBonesOnly;

    [Tooltip("If you want to generate virtual joints at midpoint.")]
    public bool virtualJointAtMidpoint = true;

    [Tooltip("Generate lengths of bones for you. You have to do the setup")]
    public bool computeBoneLengths = true;

    [Header("Built Data")]
    [SerializeField]
    private EndEffector chain_effector;

    [SerializeField]
    private IkChain ik_chain;

    [SerializeField, HideInInspector] private List<Transform> old_path;

    public EndEffector EffectorRO => chain_effector;
    public IkChain ChainRO => ik_chain;

    public ref EndEffector GetEffectorRef() => ref chain_effector;
    public ref IkChain GetChainRef() => ref ik_chain;

    [ContextMenu("Build Chain")]
    private void BuildChainEditor() => BuildChain();

    public void BuildChain()
    {
        var path = BuildPathFromHierarchy();
        if (path == null || path.Count == 0)
        {
            Debug.LogError($"Could not build path on: {name}. Check Root and End Effector.", this);
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Build Chain");
#endif

        switch (jointSource)
        {
            case JointSource.VirtualFromBonesOnly:
                BuildVirtual(path);
                break;

            case JointSource.FromHierarchyAlternating:
                BuildAlternating(path);
                break;

            default:
                // This case should ideally not be hit if the enum is handled completely.
                Debug.LogError("Failed to find path!", this);
                return;
        }

        old_path = path;

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /*
    * Virtual chain without joints
    */
    private void BuildVirtual(List<Transform> bonesPath)
    {
        int bonesCount = bonesPath.Count;
        if (bonesCount < 2)
        {
            Debug.LogWarning($"A chain requires at least 2 bones. Found {bonesCount} on '{name}'.", this);
            return;
        }

        // Pre-allocate lists with the exact size needed to avoid resizing.
        var bones = new List<IkBone>(bonesCount);
        var joints = new List<IkJoint>(bonesCount - 1);

        // Loop up to the second-to-last bone, as each one will have a child and a joint.
        for (int i = 0; i < bonesCount - 1; i++)
        {
            Transform currentBoneT = bonesPath[i];
            Transform childBoneT = bonesPath[i + 1];

            float length = computeBoneLengths ? Vector3.Distance(currentBoneT.position, childBoneT.position) : 0f;
            string jointName = $"Joint {i} ({currentBoneT.name}->{childBoneT.name})";

            bones.Add(new IkBone
            {
                length = length,
                transform = currentBoneT,
                child_joint_index = i,
                bone_name = currentBoneT.name,
                child_bone_name = childBoneT.name,
                child_joint_name = jointName
            });

            // The virtual joint's position and rotation are managed by the solver at runtime.
            // We just need to define its connectivity here.
            joints.Add(new IkJoint
            {
                rotation = Quaternion.identity,
                position = Vector3.zero,
                child_bone_index = i + 1,
                parent_bone_name = currentBoneT.name,
                child_bone_name = childBoneT.name,
                joint_name = jointName,
            });
        }

        // Add the final bone (the tip), which has no child joint.
        Transform tipTransform = bonesPath[bonesCount - 1];
        bones.Add(new IkBone
        {
            length = 0f,
            transform = tipTransform,
            child_joint_index = -1, // -1 indicates no child joint.
            bone_name = tipTransform.name,
            child_bone_name = string.Empty,
            child_joint_name = string.Empty
        });

        // The end effector is at the tip of the last bone.
        chain_effector = new EndEffector(0, tipTransform.position, tipTransform.rotation);

        ref var chainRef = ref ik_chain;
        chainRef.length = bones.Count;
        chainRef.index = 0;
        chainRef.bone_chain = bones;
        chainRef.joint_chain = joints;

        ValidateChainIntegrity();
    }

    // Not used right now but if we wanted to animate joints this is how.
    private void BuildAlternating(List<Transform> altPath)
    {
        if ((altPath.Count % 2) == 0)
            Debug.LogWarning($"Alternating: expected odd-length path, got {altPath.Count} on '{name}'.", this);

        int bonesCount = (altPath.Count + 1) / 2;
        if (bonesCount < 2)
        {
            Debug.LogWarning($"An alternating chain requires at least 2 bones. Found {bonesCount} on '{name}'.", this);
            return;
        }
        int jointsCount = bonesCount - 1;

        var bones = new List<IkBone>(bonesCount);
        var joints = new List<IkJoint>(jointsCount);

        // Local function for consistent naming.
        string JointName(int joint)
        {
            int aIdx = 2 * joint;
            int bIdx = 2 * (joint + 1);
            string a = (aIdx < altPath.Count) ? altPath[aIdx].name : $"Bone{joint}";
            string b = (bIdx < altPath.Count) ? altPath[bIdx].name : $"Bone{joint + 1}";
            return $"Joint {joint} ({a}->{b})";
        };

        for (int i = 0; i < bonesCount; i++)
        {
            int pathIndex = i * 2;
            bool hasChild = i < jointsCount;
            float length = 0f;
            if (computeBoneLengths && hasChild)
                length = Vector3.Distance(altPath[pathIndex].position, altPath[pathIndex + 2].position);

            bones.Add(new IkBone
            {
                length = length,
                child_joint_index = hasChild ? i : -1,
                bone_name = altPath[pathIndex].name,
                child_bone_name = hasChild ? altPath[pathIndex + 2].name : string.Empty,
                child_joint_name = hasChild ? JointName(i) : string.Empty
            });
        }

        for (int j = 0; j < jointsCount; j++)
        {
            int pathIndex = j * 2;
            joints.Add(new IkJoint
            {
                rotation = Quaternion.identity,
                position = Vector3.zero,
                optional_transform = altPath[pathIndex + 1],
                child_bone_index = j + 1,
                parent_bone_name = altPath[pathIndex].name,
                child_bone_name = altPath[pathIndex + 2].name,
                joint_name = JointName(j)
            });
        }

        Transform tip = altPath[altPath.Count - 1];
        chain_effector = new EndEffector(0, tip.position, tip.rotation);

        ref var chainRef = ref ik_chain;
        chainRef.length = bones.Count;
        chainRef.index = 0;
        chainRef.bone_chain = bones;
        chainRef.joint_chain = joints;

        ValidateChainIntegrity();
    }

    private List<Transform> BuildPathFromHierarchy()
    {
        Transform start = root != null ? root : transform;

        if (endEffector != null)
        {
            if (!IsDescendantOf(endEffector, start))
            {
                Debug.LogWarning($"[IkChainBuilder] End effector '{endEffector.name}' is not a descendant of '{start.name}'.", this);
                return null;
            }
            return BuildPathRootToEnd(start, endEffector);
        }

        return BuildDeepestPath(start);
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        for (var t = child; t != null; t = t.parent)
            if (t == ancestor) return true;
        return false;
    }

    private static List<Transform> BuildPathRootToEnd(Transform start, Transform end)
    {
        var path = new List<Transform>(DEFAULT_PATH_CAPACITY);
        for (var t = end; t != null; t = t.parent)
        {
            path.Add(t);
            if (t == start)
            {
                path.Reverse();
                return path;
            }
        }

        return null;
    }

    /*
    * Breath First Search for DeepestPath Finding
    */
    private static List<Transform> BuildDeepestPath(Transform start)
    {
        if (start == null) return new List<Transform>();

        var bestPath = new List<Transform>(DEFAULT_PATH_CAPACITY);
        var currentPath = new List<Transform>(DEFAULT_PATH_CAPACITY);

        var stack = new Stack<(Transform node, int childIndex)>(DEFAULT_PATH_CAPACITY);
        stack.Push((start, 0));

        while (stack.Count > 0)
        {
            var (node, childIndex) = stack.Pop();

            if (childIndex == 0)
            {
                currentPath.Add(node);
            }

            if (childIndex < node.childCount)
            {
                stack.Push((node, childIndex + 1));
        
                stack.Push((node.GetChild(childIndex), 0));
            }
            else
            {
                if (currentPath.Count > bestPath.Count)
                {
                    bestPath.Clear();
                    bestPath.AddRange(currentPath);
                }

                if (currentPath.Count > 0)
                {
                    currentPath.RemoveAt(currentPath.Count - 1);
                }
            }
        }
        return bestPath;
    }

    private void ValidateChainIntegrity()
    {
        if (ik_chain.bone_chain == null || ik_chain.joint_chain == null) return;

        int nb = ik_chain.bone_chain.Count;
        int nj = ik_chain.joint_chain.Count;

        if (nb < 1 || nj != nb - 1)
        {
            Debug.LogWarning($"Index mismatch on '{name}': bones={nb}, joints={nj} (expected joints=bones-1).", this);
            return;
        }

        for (int j = 0; j < nj; j++)
        {
            var b = ik_chain.bone_chain[j];
            if (b.child_joint_index != j)
                Debug.LogWarning($"[{j}] child_joint_index={b.child_joint_index} expected {j}.", this);

            var joint = ik_chain.joint_chain[j];
            if (joint.child_bone_index != j + 1)
                Debug.LogWarning($"Joint[{j}] child_bone_index={joint.child_bone_index} expected {j + 1}.", this);
        }

        // Final tip bone must not have a child joint.
        // That can change, to connectable joint.
        if (ik_chain.bone_chain[nb - 1].child_joint_index != -1)
        {
            Debug.LogWarning($" Tip Bone[{nb - 1}] should have child_joint_index=-1.", this);
        }
    }

    private void OnValidate()
    {
        if (ik_chain.bone_chain == null) ik_chain.bone_chain = new List<IkBone>();
        if (ik_chain.joint_chain == null) ik_chain.joint_chain = new List<IkJoint>();
    }

    private void Reset()
    {
        ik_chain.bone_chain = new List<IkBone>();
        ik_chain.joint_chain = new List<IkJoint>();
        root = transform;
    }
}