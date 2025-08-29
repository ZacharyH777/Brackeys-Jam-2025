using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ik_data;

[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("IK/Builder/IK Chain Builder")]
public sealed class IkChainBuilder : MonoBehaviour
{
    public enum JointSource
    {
        VirtualFromBonesOnly,
        FromHierarchyAlternating
    }

    // Constants
    private const int DEFAULT_PATH_CAPACITY = 32;
    private const int STRINGBUILDER_CAPACITY = 128;

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
    [SerializeField] private EndEffector chain_effector;
    [SerializeField] private IkChain ik_chain;
    [SerializeField, HideInInspector] private List<Transform> old_path;

    // Cached objects to reduce allocations
    private static readonly StringBuilder s_jointNameBuilder = new StringBuilder(STRINGBUILDER_CAPACITY);
    private static readonly List<Transform> s_tempPath = new List<Transform>(DEFAULT_PATH_CAPACITY);
    private static readonly Stack<(Transform node, int childIndex)> s_depthStack = new Stack<(Transform, int)>(DEFAULT_PATH_CAPACITY);
    
    // Property accessors
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
                BuildVirtualOptimized(path);
                break;
            case JointSource.FromHierarchyAlternating:
                BuildAlternatingOptimized(path);
                break;
            default:
                Debug.LogError("Failed to find path!", this);
                return;
        }

        // Reuse old_path list
        if (old_path == null) old_path = new List<Transform>(path.Count);
        else old_path.Clear();
        old_path.AddRange(path);

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void BuildVirtualOptimized(List<Transform> bonesPath)
    {
        int bonesCount = bonesPath.Count;
        if (bonesCount < 2)
        {
            Debug.LogWarning($"A chain requires at least 2 bones. Found {bonesCount} on '{name}'.", this);
            return;
        }

        // Reuse existing lists or create new ones
        var bones = ik_chain.bone_chain ?? new List<IkBone>(bonesCount);
        var joints = ik_chain.joint_chain ?? new List<IkJoint>(bonesCount - 1);
        
        bones.Clear();
        joints.Clear();

        // Ensure capacity to avoid reallocation
        if (bones.Capacity < bonesCount) bones.Capacity = bonesCount;
        if (joints.Capacity < bonesCount - 1) joints.Capacity = bonesCount - 1;

        // Process bones and joints in single loop
        for (int i = 0; i < bonesCount - 1; i++)
        {
            Transform currentBoneT = bonesPath[i];
            Transform childBoneT = bonesPath[i + 1];

            float length = computeBoneLengths ? 
                Vector3.Distance(currentBoneT.position, childBoneT.position) : 0f;

            // Use shared StringBuilder for joint names
            lock (s_jointNameBuilder)
            {
                s_jointNameBuilder.Clear();
                s_jointNameBuilder.Append("Joint ").Append(i)
                    .Append(" (").Append(currentBoneT.name)
                    .Append("->").Append(childBoneT.name).Append(")");
                string jointName = s_jointNameBuilder.ToString();

                bones.Add(new IkBone
                {
                    length = length,
                    transform = currentBoneT,
                    child_joint_index = i,
                    bone_name = currentBoneT.name,
                    child_bone_name = childBoneT.name,
                    child_joint_name = jointName
                });

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
        }

        // Add tip bone
        Transform tipTransform = bonesPath[bonesCount - 1];
        bones.Add(new IkBone
        {
            length = 0f,
            transform = tipTransform,
            child_joint_index = -1,
            bone_name = tipTransform.name,
            child_bone_name = string.Empty,
            child_joint_name = string.Empty
        });

        chain_effector = new EndEffector(0, tipTransform.position, tipTransform.rotation);

        ref var chainRef = ref ik_chain;
        chainRef.length = bones.Count;
        chainRef.index = 0;
        chainRef.bone_chain = bones;
        chainRef.joint_chain = joints;

        ValidateChainIntegrity();
    }

    private void BuildAlternatingOptimized(List<Transform> altPath)
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

        var bones = ik_chain.bone_chain ?? new List<IkBone>(bonesCount);
        var joints = ik_chain.joint_chain ?? new List<IkJoint>(jointsCount);
        
        bones.Clear();
        joints.Clear();
        
        if (bones.Capacity < bonesCount) bones.Capacity = bonesCount;
        if (joints.Capacity < jointsCount) joints.Capacity = jointsCount;

        // Build bones
        for (int i = 0; i < bonesCount; i++)
        {
            int pathIndex = i * 2;
            bool hasChild = i < jointsCount;
            float length = 0f;
            
            if (computeBoneLengths && hasChild && pathIndex + 2 < altPath.Count)
                length = Vector3.Distance(altPath[pathIndex].position, altPath[pathIndex + 2].position);

            bones.Add(new IkBone
            {
                length = length,
                child_joint_index = hasChild ? i : -1,
                bone_name = altPath[pathIndex].name,
                child_bone_name = hasChild && pathIndex + 2 < altPath.Count ? altPath[pathIndex + 2].name : string.Empty,
                child_joint_name = hasChild ? CreateJointName(i, altPath, pathIndex) : string.Empty
            });
        }

        // Build joints
        for (int j = 0; j < jointsCount; j++)
        {
            int pathIndex = j * 2;
            joints.Add(new IkJoint
            {
                rotation = Quaternion.identity,
                position = Vector3.zero,
                optional_transform = pathIndex + 1 < altPath.Count ? altPath[pathIndex + 1] : null,
                child_bone_index = j + 1,
                parent_bone_name = altPath[pathIndex].name,
                child_bone_name = pathIndex + 2 < altPath.Count ? altPath[pathIndex + 2].name : string.Empty,
                joint_name = CreateJointName(j, altPath, pathIndex)
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

    private string CreateJointName(int joint, List<Transform> altPath, int pathIndex)
    {
        lock (s_jointNameBuilder)
        {
            s_jointNameBuilder.Clear();
            s_jointNameBuilder.Append("Joint ").Append(joint).Append(" (");
            
            if (pathIndex < altPath.Count)
                s_jointNameBuilder.Append(altPath[pathIndex].name);
            else
                s_jointNameBuilder.Append("Bone").Append(joint);
                
            s_jointNameBuilder.Append("->");
            
            if (pathIndex + 2 < altPath.Count)
                s_jointNameBuilder.Append(altPath[pathIndex + 2].name);
            else
                s_jointNameBuilder.Append("Bone").Append(joint + 1);
                
            s_jointNameBuilder.Append(")");
            return s_jointNameBuilder.ToString();
        }
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
        s_tempPath.Clear();
        
        for (var t = end; t != null; t = t.parent)
        {
            s_tempPath.Add(t);
            if (t == start)
            {
                s_tempPath.Reverse();
                return new List<Transform>(s_tempPath); // Return copy
            }
        }

        return null;
    }

    private static List<Transform> BuildDeepestPath(Transform start)
    {
        if (start == null) return new List<Transform>();

        var bestPath = new List<Transform>(DEFAULT_PATH_CAPACITY);
        var currentPath = new List<Transform>(DEFAULT_PATH_CAPACITY);

        s_depthStack.Clear();
        s_depthStack.Push((start, 0));

        while (s_depthStack.Count > 0)
        {
            var (node, childIndex) = s_depthStack.Pop();

            if (childIndex == 0)
            {
                currentPath.Add(node);
            }

            if (childIndex < node.childCount)
            {
                s_depthStack.Push((node, childIndex + 1));
                s_depthStack.Push((node.GetChild(childIndex), 0));
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

        // Batch validation to reduce loop overhead
        for (int j = 0; j < nj; j++)
        {
            var b = ik_chain.bone_chain[j];
            var joint = ik_chain.joint_chain[j];
            
            if (b.child_joint_index != j)
                Debug.LogWarning($"[{j}] child_joint_index={b.child_joint_index} expected {j}.", this);

            if (joint.child_bone_index != j + 1)
                Debug.LogWarning($"Joint[{j}] child_bone_index={joint.child_bone_index} expected {j + 1}.", this);
        }

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