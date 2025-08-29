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
    // Constants
    private const float EPSILON = 1e-6f;
    private const float MIN_SEGMENT_LENGTH = 1e-6f;
    
    public Vector3 target_positon = new Vector3(3.09f, -0.12f, 0f);
    public Vector3 pully_position = new Vector3(-3.07f, 1.22f, 0f);
    
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
    private float normalized_progress;
    private float current_speed;
    private int waypoint_count;
    
    // Cached values to reduce repeated calculations
    private bool has_valid_setup;
    private float delta_time_cache;
    private float target_speed_cache;
    private float master_length_reciprocal;

    // Precomputed arc lengths
    private readonly List<float> pully_cumulative_distances = new List<float>();
    private readonly List<Vector3> target_positions_cache = new List<Vector3>();
    private readonly List<float> target_cumulative_distances = new List<float>();
    private float pully_length;
    private float target_length;
    private float master_length;

    // Input caching
#if ENABLE_INPUT_SYSTEM
    private Keyboard keyboard_cache;
    private Gamepad gamepad_cache;
#endif

    void Awake()
    {
        InitializeDefaults();
        CacheInputDevices();
        
        if (auto_load_on_awake && path_asset != null)
        {
            LoadFromAsset();
        }
        SyncCountsAndLengths();
    }

    private void InitializeDefaults()
    {
        if (target != null)
            target.localPosition = target_positon;
        if (pully != null)
            pully.localPosition = pully_position;
        if (root_target != null)
            root_target.localPosition = Vector3.zero;
    }

    private void CacheInputDevices()
    {
#if ENABLE_INPUT_SYSTEM
        keyboard_cache = Keyboard.current;
        gamepad_cache = Gamepad.current;
#endif
    }

#if UNITY_EDITOR
    void OnDisable()
    {
        if (Application.isPlaying && auto_save_on_stop && path_asset != null)
        {
            SaveToAsset();
        }
    }
#endif

    void Update()
    {
        if (GetPlayPressed())
        {
            StartPlayback();
        }

        // Early exit with cached validity check
        if (!has_valid_setup || !is_playing)
        {
            is_playing = false;
            return;
        }

        // Cache delta time to avoid multiple property access
        delta_time_cache = Time.deltaTime;
        
        // Calculate desired speed with cached curve evaluation
        float curve_multiplier = SafeCurve(speed_over_path, normalized_progress);
        float desired_speed = target_speed_cache * curve_multiplier;

        // Update current speed
        current_speed = Mathf.MoveTowards(current_speed, desired_speed, acceleration * delta_time_cache);

        // Update progress using cached reciprocal to avoid division
        float delta_u = current_speed * master_length_reciprocal * delta_time_cache;
        normalized_progress += delta_u;

        // Check completion
        if (normalized_progress >= 1f)
        {
            CompletePlayback();
            return;
        }

        // Update positions
        UpdateTransformPositions();
    }

    private void CompletePlayback()
    {
        normalized_progress = 1f;
        int lastIndex = waypoint_count - 1;
        target.localPosition = target_positions[lastIndex];
        pully.localPosition = pully_positions[lastIndex];
        is_playing = false;
    }

    private void UpdateTransformPositions()
    {
        float pully_distance = normalized_progress * pully_length;
        float target_distance = normalized_progress * target_length;

        target.localPosition = SampleAtDistanceOptimized(target_positions, target_cumulative_distances, target_distance);
        pully.localPosition = SampleAtDistanceOptimized(pully_positions, pully_cumulative_distances, pully_distance);
    }

    [ContextMenu("Save Position")]
    public void SavePosition()
    {
        if (pully == null || target == null)
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

    public void ClearPositions()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Clear ArmSwing Positions");
#endif
        pully_positions.Clear();
        target_positions.Clear();
        ResetPlaybackState();

        SyncCountsAndLengths();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void StartPlayback()
    {
        SyncCountsAndLengths();
        if (!ValidatePlaybackConditions())
        {
            is_playing = false;
            return;
        }

        ResetPlaybackState();
        is_playing = true;

        target.localPosition = target_positions[0];
        pully.localPosition = pully_positions[0];
    }

    private void ResetPlaybackState()
    {
        normalized_progress = 0f;
        current_speed = 0f;
    }

    private bool ValidatePlaybackConditions()
    {
        return waypoint_count >= 2 && master_length > EPSILON;
    }

    public void SnapToFirst()
    {
        if (pully == null || target == null || waypoint_count == 0) return;
        target.localPosition = target_positions[0];
        pully.localPosition = pully_positions[0];
    }

    public void SaveToAsset()
    {
#if UNITY_EDITOR
        if (!EnsureAsset()) return;

        Undo.RecordObject(path_asset, "Save ArmSwing Path Asset");
        
        // Reuse existing lists instead of creating new ones
        if (path_asset.pully_positions == null)
            path_asset.pully_positions = new List<Vector3>();
        if (path_asset.target_positions == null)
            path_asset.target_positions = new List<Vector3>();
            
        path_asset.pully_positions.Clear();
        path_asset.target_positions.Clear();
        path_asset.pully_positions.AddRange(pully_positions);
        path_asset.target_positions.AddRange(target_positions);

        EditorUtility.SetDirty(path_asset);
        AssetDatabase.SaveAssets();
#else
        Debug.LogWarning("Save to asset is editor only");
#endif
    }

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
        
        // Clear and reuse existing lists
        pully_positions.Clear();
        target_positions.Clear();
        
        if (path_asset.pully_positions != null)
            pully_positions.AddRange(path_asset.pully_positions);
        if (path_asset.target_positions != null)
            target_positions.AddRange(path_asset.target_positions);

        ResetPlaybackState();
        SyncCountsAndLengths();
        
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private void SyncCountsAndLengths()
    {
        waypoint_count = Mathf.Min(pully_positions?.Count ?? 0, target_positions?.Count ?? 0);

        BuildCumulativeOptimized(pully_positions, pully_cumulative_distances, out pully_length);
        BuildCumulativeOptimized(target_positions, target_cumulative_distances, out target_length);
        
        master_length = Mathf.Max(pully_length, target_length);
        master_length_reciprocal = master_length > EPSILON ? 1f / master_length : 0f;
        target_speed_cache = target_speed;
        
        // Update validity check
        has_valid_setup = pully != null && target != null && waypoint_count >= 2 && master_length > EPSILON;
    }

    private static void BuildCumulativeOptimized(List<Vector3> points, List<float> cumulative, out float total_length)
    {
        cumulative.Clear();
        total_length = 0f;

        if (points == null || points.Count == 0) return;

        cumulative.Add(0f);
        
        // Use squared distances for comparison when possible
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 delta = points[i] - points[i - 1];
            float distance = delta.magnitude;
            total_length += distance;
            cumulative.Add(total_length);
        }
    }

    // Optimized sampling using binary search instead of linear search
    private static Vector3 SampleAtDistanceOptimized(List<Vector3> points, List<float> cumulative, float s)
    {
        if (points == null || points.Count == 0) return Vector3.zero;
        if (points.Count == 1) return points[0];
        if (cumulative == null || cumulative.Count != points.Count) 
            return points[points.Count - 1];

        float total = cumulative[cumulative.Count - 1];
        if (total <= EPSILON) return points[points.Count - 1];
        if (s <= 0f) return points[0];
        if (s >= total) return points[points.Count - 1];

        // Binary search for the segment
        int hi = BinarySearchCumulative(cumulative, s);
        int lo = hi - 1;

        float segment_start_s = cumulative[lo];
        float segment_length = cumulative[hi] - segment_start_s;
        
        if (segment_length < MIN_SEGMENT_LENGTH) 
            segment_length = MIN_SEGMENT_LENGTH;
            
        float t = (s - segment_start_s) / segment_length;
        return Vector3.LerpUnclamped(points[lo], points[hi], t);
    }

    private static int BinarySearchCumulative(List<float> cumulative, float target)
    {
        int left = 1;
        int right = cumulative.Count - 1;
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            if (cumulative[mid] < target)
                left = mid + 1;
            else
                right = mid - 1;
        }
        
        return left;
    }

    private static float SafeCurve(AnimationCurve curve, float x)
    {
        if (curve == null) return 1f;
        return Mathf.Max(0f, curve.Evaluate(Mathf.Clamp01(x)));
    }

    private bool GetPlayPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyboard_pressed = keyboard_cache?.spaceKey.wasPressedThisFrame ?? false;
        bool gamepad_pressed = (gamepad_cache?.startButton.wasPressedThisFrame ?? false) || 
                               (gamepad_cache?.buttonSouth.wasPressedThisFrame ?? false);
        return keyboard_pressed || gamepad_pressed;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

#if UNITY_EDITOR
    private bool EnsureAsset()
    {
        if (path_asset != null) return true;

        string path = EditorUtility.SaveFilePanelInProject(
            "Create ArmSwing Path Asset",
            "ArmSwingPath",
            "asset",
            "Choose where to save the path asset");

        if (string.IsNullOrEmpty(path)) return false;

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