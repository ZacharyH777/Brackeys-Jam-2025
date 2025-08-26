using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "IK/ArmSwing Path", fileName = "ArmSwingPath")]
public class ArmSwingPath : ScriptableObject
{
    public List<Vector3> pully_positions  = new List<Vector3>();
    public List<Vector3> target_positions = new List<Vector3>();
}