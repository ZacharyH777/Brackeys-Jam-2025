using UnityEngine;

[DisallowMultipleComponent]
public class rotation_target : MonoBehaviour
{
    public Transform target; 
    public Transform torso;
    [Tooltip("If your sprite's forward isn't +Z/UP isn't toward torso, add an extra Z-rotation (in degrees).")]
    public float angleOffsetDeg = 0f;

    void LateUpdate()
    {
        if (!torso) return;

        Transform t = target ? target : transform;
        Vector3 toTorso = torso.localPosition - t.localPosition;
        if (toTorso.sqrMagnitude < 1e-8f) return;

        t.LookAt(t.position + Vector3.forward, toTorso);
        t.rotation *= Quaternion.AngleAxis(angleOffsetDeg, Vector3.forward);
    }
}
