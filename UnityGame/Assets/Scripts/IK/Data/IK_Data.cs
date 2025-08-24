using System;
using System.Collections.Generic;
using Vec3 = UnityEngine.Vector3;
using Quat = UnityEngine.Quaternion;
using UnityEngine;

namespace ik_data
{
    [Serializable]
    public struct EndEffector
    {
        public long id;
        public Vec3 location;
        public Quat rotation;

        public EndEffector(long p_id, Vec3 p_location, Quat p_rotation)
        { id = p_id; location = p_location; rotation = p_rotation; }
    }

    [Serializable]
    public struct IkJoint
    {
        public Quat rotation;
        public Vec3 position;

        public Transform optional_transform;

        public int child_bone_index;

        public string parent_bone_name; 
        public string child_bone_name;
        public string joint_name;
    }

    [Serializable]
    public struct IkBone
    {
        public float length;
        public int child_joint_index;
        public Transform transform;
        public string bone_name;        
        public string child_bone_name;  
        public string child_joint_name;
    }

    [Serializable]
    public struct IkChain
    {
        public int length;               
        public int index;              
        public List<IkBone>  bone_chain; 
        public List<IkJoint> joint_chain;
    }
}
