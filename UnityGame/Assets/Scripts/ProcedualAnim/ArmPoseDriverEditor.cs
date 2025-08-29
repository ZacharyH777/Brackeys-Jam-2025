#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArmPoseDriver))]
public sealed class ArmPoseDriverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);

        ArmPoseDriver driver = (ArmPoseDriver)target;

        if (GUILayout.Button("Capture Current Pose"))
        {
            driver.CaptureCurrentPose();
        }

        if (GUILayout.Button("Sort Keys By Driver"))
        {
            driver.SortKeysByDriver();
        }

        GUILayout.Space(4);

        EditorGUILayout.HelpBox("Enter Play Mode and pose the arm then press Capture Current Pose. Use Sort Keys By Driver for ordered thresholds.", MessageType.Info);
    }
}
#endif
