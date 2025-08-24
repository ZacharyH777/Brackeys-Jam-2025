using System;
using System.Collections.Generic;
using UnityEngine;
using ik_data;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("IK/Builder/IK Chain Builder")]
public sealed class IkChainBuilder : MonoBehaviour
{
    public enum JointSource
    {
        VirtualFromBonesOnly,
        FromHierarchyAlternating
    }

    [Header("Hierarchy")]
    [Tooltip("Start of this IK chain. If null, uses this GameObject.")]
    public Transform root;

    [Tooltip("End effector. If null: pick deepest leaf under root.")]
    public Transform endEffector;

    [Header("Build Options")]
    [Tooltip("Where joints come from:\n" +
             "- VirtualFromBonesOnly: hierarchy contains only bones; joints are synthesized.\n" +
             "- FromHierarchyAlternating: hierarchy alternates Bone->Joint->Bone...")]
    public JointSource jointSource = JointSource.VirtualFromBonesOnly;

    [Tooltip("If true (virtual mode only), place the joint at the midpoint between parent/child bones; otherwise use the child bone's transform.")]
    public bool virtualJointAtMidpoint = true;

    [Tooltip("If true, compute bone length from Bone[i] to Bone[i+1] (virtual mode) or from Bone[i] to Bone[i+1] skipping the joint (alternating mode).")]
    public bool computeBoneLengths = true;

    [Header("Built Data (read-only in Inspector)")]
    [SerializeField] private EndEffector chain_effector;
    [SerializeField] private IkChain ik_chain;

    [SerializeField, HideInInspector] private List<Transform> old_path;

    public EndEffector EffectorRO => chain_effector;
    public IkChain ChainRO => ik_chain;

    public ref EndEffector GetEffectorRef() => ref chain_effector;
    public ref IkChain GetChainRef() => ref ik_chain;

    [ContextMenu("Build Chain")]
    private void BuildChainEditor() => BuildChain(true);

    public void BuildChain(bool force = true)
    {
        var path = BuildPathFromHierarchy();
        if (path == null || path.Count == 0)
        {
            Debug.LogError($"[IkChainBuilder] Could not build path on '{name}'.");
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Build IK Chain");
#endif

        switch (jointSource)
        {
            case JointSource.VirtualFromBonesOnly:
                BuildVirtual(path);
                break;

            case JointSource.FromHierarchyAlternating:
                BuildAlternating(path);
                break;
        }

        old_path = path;

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void BuildVirtual(List<Transform> bonesPath)
    {
        int bonesCount = bonesPath.Count;
        if (bonesCount < 1)
        {
            Debug.LogWarning($"[IkChainBuilder] Virtual: no bones on '{name}'.");
            return;
        }

        var bones = new List<IkBone>(bonesCount);
        var joints = new List<IkJoint>(Math.Max(0, bonesCount - 1));

        for (int i = 0; i < bonesCount; i++)
        {
            bool hasChild = i < bonesCount - 1;
            float length = 0f;

            if (computeBoneLengths && hasChild)
                length = Vector3.Distance(bonesPath[i].position, bonesPath[i + 1].position);

            bones.Add(new IkBone
            {
                length = length,
                transform = bonesPath[i],
                child_joint_index = hasChild ? i : -1,
                bone_name = bonesPath[i].name,
                child_bone_name = hasChild ? bonesPath[i + 1].name : string.Empty,
                child_joint_name = hasChild ? $"Joint {i} ({bonesPath[i].name}->{bonesPath[i + 1].name})" : string.Empty
            });

            if (hasChild)
            {
                Vector3 jp;
                Quaternion jq;

                if (virtualJointAtMidpoint)
                {
                    jp = 0.5f * (bonesPath[i].position + bonesPath[i + 1].position);
                    Vector3 fwd = (bonesPath[i + 1].position - bonesPath[i].position).normalized;
                    jq = (fwd.sqrMagnitude > 1e-8f) ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
                }
                else
                {
                    jp = bonesPath[i + 1].position;
                    jq = bonesPath[i + 1].rotation;
                }

                joints.Add(new IkJoint
                {
                    rotation = Quaternion.identity,
                    position = Vector3.zero,
                    child_bone_index = i + 1,
                    parent_bone_name = bonesPath[i].name,
                    child_bone_name = bonesPath[i + 1].name,
                    joint_name = $"Joint {i} ({bonesPath[i].name}->{bonesPath[i + 1].name})",
                });
            }
        }

        Transform tip = bonesPath[bonesCount - 1];
        chain_effector = new EndEffector(0, tip.position, tip.rotation);

        ref var chainRef = ref ik_chain;
        chainRef.length = bones.Count;
        chainRef.index = 0;
        chainRef.bone_chain = bones;
        chainRef.joint_chain = joints;

        ValidateAlternation();
    }

    private void BuildAlternating(List<Transform> altPath)
    {
        if ((altPath.Count % 2) == 0)
            Debug.LogWarning($"[IkChainBuilder] Alternating: expected odd-length path, got {altPath.Count} on '{name}'.");

        int bonesCount = (altPath.Count + 1) / 2;
        int jointsCount = Math.Max(0, bonesCount - 1);

        var bones = new List<IkBone>(bonesCount);
        var joints = new List<IkJoint>(jointsCount);

        Func<int, string> JointName = j =>
        {
            int aIdx = 2 * j;
            int bIdx = 2 * (j + 1);
            string a = (aIdx < altPath.Count) ? altPath[aIdx].name : $"Bone{j}";
            string b = (bIdx < altPath.Count) ? altPath[bIdx].name : $"Bone{j + 1}";
            return $"Joint {j} ({a}->{b})";
        };

        for (int i = 0; i < altPath.Count; i += 2)
        {
            int bi = i / 2;
            bool hasChild = bi < bonesCount - 1;

            float length = 0f;
            if (computeBoneLengths && hasChild)
                length = Vector3.Distance(altPath[i].position, altPath[i + 2].position);

            bones.Add(new IkBone
            {
                length = length,
                child_joint_index = hasChild ? bi : -1,
                bone_name = altPath[i].name,
                child_bone_name = hasChild ? altPath[i + 2].name : string.Empty,
                child_joint_name = hasChild ? JointName(bi) : string.Empty
            });
        }

        for (int i = 1; i < altPath.Count; i += 2)
        {
            int j = (i - 1) / 2;
            joints.Add(new IkJoint
            {
                rotation = Quaternion.identity,
                child_bone_index = j + 1,
                parent_bone_name = altPath[2 * j].name,
                child_bone_name = altPath[2 * (j + 1)].name,
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

        ValidateAlternation();
    }

    private List<Transform> BuildPathFromHierarchy()
    {
        Transform start = root != null ? root : transform;

        if (endEffector != null)
        {
            if (!IsDescendantOf(endEffector, start))
            {
                Debug.LogWarning($"[IkChainBuilder] End effector '{endEffector.name}' is not a descendant of '{start.name}'.");
                return null;
            }
            var p = BuildPathRootToEnd(start, endEffector);
            if (jointSource == JointSource.FromHierarchyAlternating && (p.Count % 2) == 0)
                Debug.LogWarning($"[IkChainBuilder] Alternating mode: expected odd-length path {start.name}->{endEffector.name}.");
            return p;
        }

        var deepest = BuildDeepestPath(start);

        if (jointSource == JointSource.FromHierarchyAlternating && (deepest.Count % 2) == 0)
            Debug.LogWarning($"[IkChainBuilder] Alternating mode: deepest path under '{start.name}' is even-length. Check your Bone->Joint alternation.");

        return deepest;
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        for (var t = child; t != null; t = t.parent)
            if (t == ancestor) return true;
        return false;
    }

    private static List<Transform> BuildPathRootToEnd(Transform start, Transform end)
    {
        var rev = new List<Transform>(16);
        for (var t = end; t != null; t = t.parent)
        {
            rev.Add(t);
            if (t == start) break;
        }
        if (rev[rev.Count - 1] != start) return null;

        var path = new List<Transform>(rev.Count);
        for (int i = rev.Count - 1; i >= 0; i--) path.Add(rev[i]);
        return path;
    }

    private static List<Transform> BuildDeepestPath(Transform start)
    {
        var best = new List<Transform>(32);
        var cur = new List<Transform>(32);
        DFSDeepest(start, cur, best);
        return best;
    }

    private static void DFSDeepest(Transform node, List<Transform> cur, List<Transform> best)
    {
        if (!node) return;
        cur.Add(node);

        int cc = node.childCount;
        if (cc == 0)
        {
            if (cur.Count > best.Count)
            {
                best.Clear();
                best.AddRange(cur);
            }
        }
        else
        {
            for (int i = 0; i < cc; i++) DFSDeepest(node.GetChild(i), cur, best);
        }

        cur.RemoveAt(cur.Count - 1);
    }

    private void ValidateAlternation()
    {
        if (ik_chain.bone_chain == null || ik_chain.joint_chain == null) return;

        int nb = ik_chain.bone_chain.Count;
        int nj = ik_chain.joint_chain.Count;

        if (nb < 1 || nj != nb - 1)
        {
            Debug.LogWarning($"[IkChainBuilder] Index mismatch on '{name}': bones={nb}, joints={nj} (expected joints=bones-1).");
            return;
        }

        for (int j = 0; j < nb; j++)
        {
            var b = ik_chain.bone_chain[j];

            if (j < nb - 1)
            {
                if (b.child_joint_index != j)
                    Debug.LogWarning($"[IkChainBuilder] Bone[{j}] child_joint_index={b.child_joint_index} expected {j}.");

                var joint = ik_chain.joint_chain[j];
                if (joint.child_bone_index != j + 1)
                    Debug.LogWarning($"[IkChainBuilder] Joint[{j}] child_bone_index={joint.child_bone_index} expected {j + 1}.");
            }
            else if (b.child_joint_index != -1)
            {
                Debug.LogWarning($"[IkChainBuilder] Tip Bone[{j}] should have child_joint_index=-1.");
            }
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
    }
}
