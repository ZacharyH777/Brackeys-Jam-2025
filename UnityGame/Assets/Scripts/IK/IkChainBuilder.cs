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
*
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

    [Header("Hierarchy")]
    [Tooltip("This is the start of the ik chain. Sorta doubles as an end effector since this ik is two sided.")]
    public Transform root;

    [Tooltip("The is the leaf or end of the ik chain.")]
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

    // public EndEffector EffectorRO => chain_effector;
    // public IkChain ChainRO => ik_chain;

    // public ref EndEffector GetEffectorRef() => ref chain_effector;
    // public ref IkChain GetChainRef() => ref ik_chain;

    // [ContextMenu("Build Chain")]
    // private void BuildChainEditor() => BuildChainEditor(true);

//     public void BuildChain(bool force = true)
//     {
//         var path =
//     }
// #if UNITY_EDITOR
//     if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Build Chain");
// #endif

//     switch (jointSource)
//     {
//         case Joint
//     }

}