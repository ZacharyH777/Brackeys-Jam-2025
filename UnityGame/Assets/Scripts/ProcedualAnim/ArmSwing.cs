using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/*
Fluid path playback with a single global speed ramp across the whole path.
Records local waypoints for pully and target, then moves along both paths
with one normalized progress value. Acceleration and an optional speed
curve modulate motion. No threshold hopping. Progress flows across
segments. Saves and loads waypoints to an asset so play mode edits persist.
*/
public class ArmSwing : MonoBehaviour
{
    [Header("References")]
    public Transform root;
    public Transform end_effector;
    public Transform target;
    public Transform root_target;
    public Transform pully;

    [Header("Waypoints Local")]
    public List<Vector3> pully_positions = new List<Vector3>();
    public List<Vector3> target_positions = new List<Vector3>();

    [Header("Speed Global")]
    [Tooltip("Units per second against longer path")]
    public float target_speed = 2.0f;
    [Tooltip("Acceleration units per second squared")]
    public float acceleration = 4.0f;
    [Tooltip("Speed over path progress")]
    public AnimationCurve speed_over_path = AnimationCurve.Linear(0, 1, 1, 1);

    [Header("Persistence")]
    [Tooltip("Shared asset for path")]
    public ArmSwingPath path_asset;
    [Tooltip("Load asset on awake")]
    public bool auto_load_on_awake = false;
    [Tooltip("Save to asset on stop")]
    public bool auto_save_on_stop = true;

    // Playback state
    private bool is_playing;
    private float normalized_progress;     // 0..1 across the whole path
    private float current_speed;           // units per second
    private int waypoint_count;            // min(list counts)

    // Precomputed arc lengths
    private readonly List<float> pully_cumulative_distances = new List<float>();
    private readonly List<float> target_cumulative_distances = new List<float>();
    private float pully_length;
    private float target_length;
    private float master_length;           // max of both lengths

    /*
    Set default local positions, optionally load the asset, then precompute lengths.
    */
    void Awake()
    {
        if (target != null)
        {
            target.localPosition = new Vector3(3.09f, -0.12f, 0f);
        }
        if (pully != null)
        {
            pully.localPosition = new Vector3(-3.07f, 1.22f, 0f);
        }
        if (root_target != null)
        {
            root_target.localPosition = Vector3.zero;
        }

        if (auto_load_on_awake)
        {
            if (path_asset != null)
            {
                LoadFromAsset();
            }
        }
        SyncCountsAndLengths();
    }

#if UNITY_EDITOR
    /*
    Auto save on leaving play mode when enabled.
    */
    void OnDisable()
    {
        if (Application.isPlaying)
        {
            if (auto_save_on_stop)
            {
                if (path_asset != null)
                {
                    SaveToAsset();
                }
            }
        }
    }
#endif

    /*
    Start playback on input. Advance progress using global speed with acceleration.
    */
    void Update()
    {
        if (GetPlayPressed())
        {
            StartPlayback();
        }

        if (!is_playing)
        {
            return;
        }
        if (pully == null)
        {
            is_playing = false;
            return;
        }
        if (target == null)
        {
            is_playing = false;
            return;
        }
        if (waypoint_count < 2)
        {
            is_playing = false;
            return;
        }
        if (master_length <= 1e-6f)
        {
            // Tiny floor prevents division by zero when path length is almost zero
            is_playing = false;
            return;
        }

        // Desired speed comes from a flat target plus a curve over progress
        float desired_speed = Mathf.Max(0f, target_speed) * SafeCurve(speed_over_path, normalized_progress);

        // Accelerate toward desired speed using a per frame bound
        current_speed = MoveToward(current_speed, desired_speed, acceleration * Time.deltaTime);

        // Convert speed to normalized progress by dividing by the full path length
        float delta_u = (current_speed / master_length) * Time.deltaTime;
        normalized_progress = normalized_progress + delta_u;

        if (normalized_progress >= 1f)
        {
            normalized_progress = 1f;
            target.localPosition = target_positions[waypoint_count - 1];
            pully.localPosition = pully_positions[waypoint_count - 1];
            is_playing = false;
            return;
        }

        float pully_distance = normalized_progress * pully_length;
        float target_distance = normalized_progress * target_length;

        // Sample both polylines at their respective arc distances
        target.localPosition = SampleAtDistance(target_positions, target_cumulative_distances, target_distance);
        pully.localPosition = SampleAtDistance(pully_positions, pully_cumulative_distances, pully_distance);
    }

    /*
    Add current local positions to the waypoint lists.
    */
    [ContextMenu("Save Position")]
    public void SavePosition()
    {
        if (pully == null)
        {
            Debug.LogWarning("Assign pully and target before saving positions");
            return;
        }
        if (target == null)
        {
            Debug.LogWarning("Assign pully and target before saving positions");
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
    Clear all waypoints and reset playback state.
    */
    public void ClearPositions()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Clear ArmSwing Positions");
#endif
        pully_positions.Clear();
        target_positions.Clear();
        is_playing = false;
        normalized_progress = 0f;
        current_speed = 0f;

        SyncCountsAndLengths();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /*
    Begin playback from the start with a fresh speed ramp.
    */
    public void StartPlayback()
    {
        SyncCountsAndLengths();
        if (waypoint_count < 2)
        {
            Debug.LogWarning("Need at least two saved positions");
            is_playing = false;
            return;
        }
        if (master_length <= 1e-6f)
        {
            Debug.LogWarning("Path length is too small");
            is_playing = false;
            return;
        }

        normalized_progress = 0f;
        current_speed = 0f;
        is_playing = true;

        target.localPosition = target_positions[0];
        pully.localPosition = pully_positions[0];
    }

    /*
    Snap transforms to the first saved pose.
    */
    public void SnapToFirst()
    {
        if (pully == null)
        {
            return;
        }
        if (target == null)
        {
            return;
        }
        if (waypoint_count == 0)
        {
            return;
        }
        target.localPosition = target_positions[0];
        pully.localPosition = pully_positions[0];
    }

    /*
    Save the waypoint lists to the asset. Editor only.
    */
    public void SaveToAsset()
    {
#if UNITY_EDITOR
        if (!EnsureAsset())
        {
            return;
        }

        Undo.RecordObject(path_asset, "Save ArmSwing Path Asset");
        path_asset.pully_positions = new List<Vector3>(pully_positions);
        path_asset.target_positions = new List<Vector3>(target_positions);

        EditorUtility.SetDirty(path_asset);
        AssetDatabase.SaveAssets();
#else
        Debug.LogWarning("Save to asset is editor only");
#endif
    }

    /*
    Load the waypoint lists from the asset and reset playback.
    */
    public void LoadFromAsset()
    {
        if (path_asset == null)
        {
            Debug.LogWarning("Assign a path asset first");
            return;
        }
#if UNITY_EDITOR
        Undo.RecordObject(this, "Load ArmSwing Path From Asset");
#endif
        pully_positions = new List<Vector3>(path_asset.pully_positions != null ? path_asset.pully_positions : new List<Vector3>());
        target_positions = new List<Vector3>(path_asset.target_positions != null ? path_asset.target_positions : new List<Vector3>());

        is_playing = false;
        normalized_progress = 0f;
        current_speed = 0f;

        SyncCountsAndLengths();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /*
    Precompute cumulative arc lengths and the master length used for time scaling.
    */
    private void SyncCountsAndLengths()
    {
        int pully_count = 0;
        if (pully_positions != null)
        {
            pully_count = pully_positions.Count;
        }

        int target_count = 0;
        if (target_positions != null)
        {
            target_count = target_positions.Count;
        }

        waypoint_count = Mathf.Min(pully_count, target_count);

        BuildCumulative(pully_positions, pully_cumulative_distances, out pully_length);
        BuildCumulative(target_positions, target_cumulative_distances, out target_length);
        master_length = Mathf.Max(pully_length, target_length);
    }

    /*
    Build a cumulative distance table for a polyline.
    @param points List of local points defining the path.
    @param cumulative Output list of cumulative distances.
    @param total_length Output total arc length of the path.
    */
    private static void BuildCumulative(List<Vector3> points, List<float> cumulative, out float total_length)
    {
        cumulative.Clear();
        total_length = 0f;

        if (points == null)
        {
            return;
        }
        if (points.Count == 0)
        {
            return;
        }

        cumulative.Add(0f);
        for (int i = 1; i < points.Count; i++)
        {
            total_length = total_length + Vector3.Distance(points[i - 1], points[i]);
            cumulative.Add(total_length);
        }
    }

    /*
    Sample a polyline at a given arc distance using the cumulative table.
    Uses small floors to avoid divide by zero when a segment is extremely short.
    @param points List of local points defining the path.
    @param cumulative Cumulative distance table matching the points.
    @param s Arc distance along the path.
    @return Interpolated local point at the requested distance.
    */
    private static Vector3 SampleAtDistance(List<Vector3> points, List<float> cumulative, float s)
    {
        if (points == null)
        {
            return Vector3.zero;
        }
        if (points.Count == 0)
        {
            return Vector3.zero;
        }
        if (points.Count == 1)
        {
            return points[0];
        }
        if (cumulative == null)
        {
            return points[points.Count - 1];
        }
        if (cumulative.Count != points.Count)
        {
            return points[points.Count - 1];
        }

        float total = cumulative[cumulative.Count - 1];
        if (total <= 1e-6f)
        {
            // Avoid division by zero when the whole path is almost a point
            return points[points.Count - 1];
        }

        if (s <= 0f)
        {
            return points[0];
        }
        if (s >= total)
        {
            return points[points.Count - 1];
        }

        // Find the segment that contains the target distance s
        int hi = 1;
        while (hi < cumulative.Count && cumulative[hi] < s)
        {
            hi = hi + 1;
        }
        int lo = hi - 1;

        float segment_start_s = cumulative[lo];
        float segment_length = cumulative[hi] - segment_start_s;
        if (segment_length < 1e-6f)
        {
            // Use a tiny floor so interpolation stays stable on degenerate segments
            segment_length = 1e-6f;
        }
        float t = (s - segment_start_s) / segment_length;
        return Vector3.LerpUnclamped(points[lo], points[hi], t);
    }

    /*
    Evaluate the speed curve safely. Returns non negative values only.
    @param curve The speed modulation curve.
    @param x Normalized progress from zero to one.
    @return A non negative multiplier applied to base speed.
    */
    private static float SafeCurve(AnimationCurve curve, float x)
    {
        if (curve == null)
        {
            return 1f;
        }
        float clamped = Mathf.Clamp01(x);
        float evaluated = curve.Evaluate(clamped);
        if (evaluated < 0f)
        {
            return 0f;
        }
        return evaluated;
    }

    /*
    Move a value toward a target by a maximum step per call.
    @param current Current value.
    @param target Target value.
    @param max_delta Maximum change this call.
    @return The moved value clamped so it does not overshoot.
    */
    private static float MoveToward(float current, float target, float max_delta)
    {
        if (current < target)
        {
            float next = current + max_delta;
            if (next > target)
            {
                return target;
            }
            return next;
        }
        if (current > target)
        {
            float next = current - max_delta;
            if (next < target)
            {
                return target;
            }
            return next;
        }
        return current;
    }

    /*
    Check for play input from keyboard or gamepad.
    @return True when play input was pressed this frame.
    */
    private bool GetPlayPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard_pressed = false;
        if (Keyboard.current != null)
        {
            keyboard_pressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        bool gamepad_pressed = false;
        if (Gamepad.current != null)
        {
            if (Gamepad.current.startButton.wasPressedThisFrame)
            {
                gamepad_pressed = true;
            }
            else
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    gamepad_pressed = true;
                }
            }
        }
        return keyboard_pressed || gamepad_pressed;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

#if UNITY_EDITOR
    /*
    Create a new asset when none is assigned.
    @return True when an asset is present or created.
    */
    private bool EnsureAsset()
    {
        if (path_asset != null)
        {
            return true;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Create ArmSwing Path Asset",
            "ArmSwingPath",
            "asset",
            "Choose where to save the path asset");

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        ArmSwingPath asset = ScriptableObject.CreateInstance<ArmSwingPath>();
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
Custom inspector with buttons for saving, clearing, playback, and asset io.
*/
[CustomEditor(typeof(ArmSwing))]
public class ArmSwingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        ArmSwing arm_swing = (ArmSwing)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save position", GUILayout.Height(22)))
            {
                arm_swing.SavePosition();
            }
            if (GUILayout.Button("Clear positions", GUILayout.Height(22)))
            {
                arm_swing.ClearPositions();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Play SPACE", GUILayout.Height(22)))
            {
                arm_swing.StartPlayback();
            }
            if (GUILayout.Button("Snap to first", GUILayout.Height(22)))
            {
                arm_swing.SnapToFirst();
            }
        }

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load from asset", GUILayout.Height(22)))
            {
                arm_swing.LoadFromAsset();
            }
            if (GUILayout.Button("Save to asset", GUILayout.Height(22)))
            {
                arm_swing.SaveToAsset();
            }
        }
    }
}
#endif
