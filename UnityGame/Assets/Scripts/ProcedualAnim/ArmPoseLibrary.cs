using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ArmPoseLibrary : ScriptableObject
{
    [Serializable]
    public struct ArmPoseKey
    {
        [Header("Name")]
        [Tooltip("Optional label for this pose")]
        public string pose_name;

        [Header("Sprites")]
        [Tooltip("Sprite for upper arm")]
        public Sprite upper_arm_sprite;
        [Tooltip("Sprite for forearm")]
        public Sprite forearm_sprite;
        [Tooltip("Sprite for hand")]
        public Sprite hand_sprite;

        [Header("Angles")]
        [Tooltip("Local Z degrees of upper arm")]
        public float upper_arm_z;
        [Tooltip("Local Z degrees of forearm")]
        public float forearm_z;
        [Tooltip("Local Z degrees of hand")]
        public float hand_z;

        [Header("Match")]
        [Tooltip("Degrees used for nearest match per joint")]
        public float per_joint_tolerance;
    }

    [Header("Keys")]
    [Tooltip("List of saved poses")]
    public List<ArmPoseKey> keys = new List<ArmPoseKey>();
}
