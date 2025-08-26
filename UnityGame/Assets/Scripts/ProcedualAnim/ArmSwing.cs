using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/*
* Fluid path playback with global speed ramp (not per-segment).
* - Records LOCAL waypoints for pully + target.
* - Plays along the whole path using ONE normalized progress (u ∈ [0..1]).
* - Speed ramps with acceleration and an optional speed curve over u.
* - No threshold hopping; progress carries across segments seamlessly.
* - Save/load to ScriptableObject so Play Mode edits persist.
*
*/
public class ArmSwing : MonoBehaviour
{
    [Header("Refs")]
    public Transform root;
    public Transform end_effector;
    public Transform target;
    public Transform root_target;
    public Transform pully;

    [Header("Waypoints (local)")]
    public List<Vector3> pully_positions = new List<Vector3>();
    public List<Vector3> target_positions = new List<Vector3>();

    [Header("Speed (global)")]
    [Tooltip("World units/sec measured against the longer of the two paths")]
    public float target_speed = 2.0f;
    [Tooltip("Acceleration in units/sec^2")]
    public float acceleration = 4.0f;
    [Tooltip("Modulate speed across path progress u∈[0..1]")]
    public AnimationCurve speed_over_path = AnimationCurve.Linear(0, 1, 1, 1);

    [Header("Persistence")]
    [Tooltip("Shared asset to load/save path")]
    public ArmSwingPath path_asset;
    [Tooltip("Load asset on Awake")]
    public bool auto_load_on_awake = false;
    [Tooltip("Auto-save to asset when exiting Play Mode")]
    public bool auto_save_on_stop = true;

    // playback state
    private bool is_playing;
    private float u;                 // normalized progress 0..1 across the WHOLE path
    private float current_speed;     // units/sec
    private int count;               // min(list counts)

    // precomputed arc-lengths
    private readonly List<float> pully_cum  = new List<float>();
    private readonly List<float> target_cum = new List<float>();
    private float pully_len;
    private float target_len;
    private float master_len;        // max(pully_len, target_len)

    /*
    * Defaults + optional asset load + precompute lengths
    *
    */
    void Awake()
    {
        if (target) target.localPosition = new Vector3(3.09f, -0.12f, 0f);
        if (pully)  pully.localPosition  = new Vector3(-3.07f, 1.22f, 0f);
        if (root_target) root_target.localPosition = Vector3.zero;

        if (auto_load_on_awake && path_asset) LoadFromAsset();
        SyncCountsAndLengths();
    }

#if UNITY_EDITOR
    /*
    * Auto-save on leaving Play Mode
    *
    */
    void OnDisable()
    {
        if (Application.isPlaying && auto_save_on_stop && path_asset) SaveToAsset();
    }
#endif

    /*
    * Space (or inspector) starts playback; motion uses global speed with acceleration
    *
    */
    void Update()
    {
        if (GetPlayPressed()) StartPlayback();

        if (!is_playing) return;
        if (!pully || !target) { is_playing = false; return; }
        if (count < 2 || master_len <= 1e-6f) { is_playing = false; return; }

        float desired_speed = Mathf.Max(0f, target_speed) * SafeCurve(speed_over_path, u);
        current_speed = MoveToward(current_speed, desired_speed, acceleration * Time.deltaTime);

        float du = (current_speed / master_len) * Time.deltaTime;
        u += du;

        if (u >= 1f)
        {
            u = 1f;
            target.localPosition = target_positions[count - 1];
            pully .localPosition = pully_positions[count - 1];
            is_playing = false;
            return;
        }

        float s_pully  = u * pully_len;
        float s_target = u * target_len;

        target.localPosition = SampleAtDistance(target_positions, target_cum, s_target);
        pully .localPosition = SampleAtDistance(pully_positions,  pully_cum,  s_pully);
    }

    /*
    * Add current LOCAL positions to lists
    *
    */
    [ContextMenu("Save Position")]
    public void SavePosition()
    {
        if (!pully || !target)
        {
            Debug.LogWarning("[ArmSwing] Assign both 'pully' and 'target' before saving.");
            return;
        }
#if UNITY_EDITOR
        Undo.RecordObject(this, "Save ArmSwing Position");
#endif
        pully_positions.Add(pully.localPosition);
        target_positions.Add(target.localPosition);

        SyncCountsAndLengths();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /*
    * Clear all waypoints
    *
    */
    public void ClearPositions()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Clear ArmSwing Positions");
#endif
        pully_positions.Clear();
        target_positions.Clear();
        is_playing = false;
        u = 0f;
        current_speed = 0f;

        SyncCountsAndLengths();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /*
    * Begin from u=0, with speed ramp starting from 0
    *
    */
    public void StartPlayback()
    {
        SyncCountsAndLengths();
        if (count < 2 || master_len <= 1e-6f)
        {
            Debug.LogWarning("[ArmSwing] Need at least 2 saved positions.");
            is_playing = false;
            return;
        }

        u = 0f;
        current_speed = 0f;
        is_playing = true;

        target.localPosition = target_positions[0];
        pully .localPosition = pully_positions[0];
    }

    /*
    * Snap to first saved pose
    *
    */
    public void SnapToFirst()
    {
        if (!pully || !target) return;
        if (count == 0) return;
        target.localPosition = target_positions[0];
        pully .localPosition = pully_positions[0];
    }

    /*
    * Save lists -> asset (Editor)
    *
    */
    public void SaveToAsset()
    {
#if UNITY_EDITOR
        if (!EnsureAsset()) return;

        Undo.RecordObject(path_asset, "Save ArmSwing Path Asset");
        path_asset.pully_positions  = new List<Vector3>(pully_positions);
        path_asset.target_positions = new List<Vector3>(target_positions);

        EditorUtility.SetDirty(path_asset);
        AssetDatabase.SaveAssets();
#else
        Debug.LogWarning("[ArmSwing] SaveToAsset is Editor-only.");
#endif
    }

    /*
    * Load lists <- asset
    *
    */
    public void LoadFromAsset()
    {
        if (!path_asset)
        {
            Debug.LogWarning("[ArmSwing] Assign a Path Asset first.");
            return;
        }
#if UNITY_EDITOR
        Undo.RecordObject(this, "Load ArmSwing Path From Asset");
#endif
        pully_positions  = new List<Vector3>(path_asset.pully_positions  ?? new List<Vector3>());
        target_positions = new List<Vector3>(path_asset.target_positions ?? new List<Vector3>());

        is_playing = false;
        u = 0f;
        current_speed = 0f;

        SyncCountsAndLengths();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /*
    * Precompute cumulative lengths and master length
    *
    */
    private void SyncCountsAndLengths()
    {
        count = Mathf.Min(
            pully_positions  != null ? pully_positions.Count  : 0,
            target_positions != null ? target_positions.Count : 0
        );

        BuildCumulative(pully_positions,  pully_cum,  out pully_len);
        BuildCumulative(target_positions, target_cum, out target_len);
        master_len = Mathf.Max(pully_len, target_len);
    }

    private static void BuildCumulative(List<Vector3> pts, List<float> cum, out float total)
    {
        cum.Clear();
        total = 0f;

        if (pts == null || pts.Count == 0) return;

        cum.Add(0f);
        for (int i = 1; i < pts.Count; i++)
        {
            total += Vector3.Distance(pts[i - 1], pts[i]);
            cum.Add(total);
        }
    }

    /*
    * Sample a polyline at arc-length s (clamped)
    *
    */
    private static Vector3 SampleAtDistance(List<Vector3> pts, List<float> cum, float s)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0];
        if (cum == null || cum.Count != pts.Count) return pts[pts.Count - 1];

        float total = cum[cum.Count - 1];
        if (total <= 1e-6f) return pts[pts.Count - 1];

        if (s <= 0f) return pts[0];
        if (s >= total) return pts[pts.Count - 1];

        int hi = 1;
        while (hi < cum.Count && cum[hi] < s) hi++;
        int lo = hi - 1;

        float seg_start_s = cum[lo];
        float seg_len = Mathf.Max(1e-6f, cum[hi] - seg_start_s);
        float t = (s - seg_start_s) / seg_len;
        return Vector3.LerpUnclamped(pts[lo], pts[hi], t);
    }

    /*
    * Helpers
    *
    */
    private static float SafeCurve(AnimationCurve c, float x)
    {
        if (c == null) return 1f;
        return Mathf.Max(0f, c.Evaluate(Mathf.Clamp01(x)));
    }

    private static float MoveToward(float current, float target, float max_delta)
    {
        if (current < target) return Mathf.Min(current + max_delta, target);
        if (current > target) return Mathf.Max(current - max_delta, target);
        return current;
    }

    private bool GetPlayPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool kb = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool gp = Gamepad.current  != null && (
                    Gamepad.current.startButton.wasPressedThisFrame ||
                    Gamepad.current.buttonSouth.wasPressedThisFrame
                  );
        return kb || gp;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

#if UNITY_EDITOR
    /*
    * Create a new asset if none assigned
    *
    */
    private bool EnsureAsset()
    {
        if (path_asset) return true;

        string path = EditorUtility.SaveFilePanelInProject(
            "Create ArmSwing Path Asset",
            "ArmSwingPath",
            "asset",
            "Choose where to save the path asset");

        if (string.IsNullOrEmpty(path)) return false;

        var asset = ScriptableObject.CreateInstance<ArmSwingPath>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        path_asset = asset;
        EditorUtility.SetDirty(this);
        return true;
    }
#endif
}

#if UNITY_EDITOR
/*
* Custom Inspector: save/load + playback controls
*
*/
[CustomEditor(typeof(ArmSwing))]
public class ArmSwingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        var s = (ArmSwing)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save position", GUILayout.Height(22))) s.SavePosition();
            if (GUILayout.Button("Clear positions", GUILayout.Height(22))) s.ClearPositions();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Play (SPACE)", GUILayout.Height(22))) s.StartPlayback();
            if (GUILayout.Button("Snap to first", GUILayout.Height(22))) s.SnapToFirst();
        }

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load from Asset", GUILayout.Height(22))) s.LoadFromAsset();
            if (GUILayout.Button("Save to Asset",  GUILayout.Height(22))) s.SaveToAsset();
        }
    }
}
#endif
