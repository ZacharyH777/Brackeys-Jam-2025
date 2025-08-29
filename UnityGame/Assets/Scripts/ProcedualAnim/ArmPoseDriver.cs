using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class ArmPoseDriver : MonoBehaviour
{
    public enum Mode { SequenceByDriver, NearestByTolerance }
    public enum DriverJoint { UpperArm, Forearm, Hand }

    [Header("Renderers")]
    [Tooltip("Renderer for upper arm")]
    public SpriteRenderer upper_arm_renderer;
    [Tooltip("Renderer for forearm")]
    public SpriteRenderer forearm_renderer;
    [Tooltip("Renderer for hand")]
    public SpriteRenderer hand_renderer;

    [Header("Joints")]
    [Tooltip("Transform of upper arm joint")]
    public Transform upper_arm_joint;
    [Tooltip("Transform of forearm joint")]
    public Transform forearm_joint;
    [Tooltip("Transform of hand joint")]
    public Transform hand_joint;

    [Header("Collider")]
    [Tooltip("Capsule collider to drive")]
    public CapsuleCollider2D collider_bounds;

    [Header("Library")]
    [Tooltip("Pose library asset")]
    public ArmPoseLibrary library;

    [Header("Mode")]
    [Tooltip("Selection mode")]
    public Mode mode = Mode.SequenceByDriver;
    [Tooltip("Driver joint for sequence mode")]
    public DriverJoint driver_joint = DriverJoint.Forearm;

    [Header("Sequence")]
    [Tooltip("Enable circular wrap at ends")]
    public bool wrap_sequence = false;
    [Tooltip("Extra degrees to avoid flicker")]
    public float hysteresis_degrees = 3f;

    [Header("Nearest")]
    [Tooltip("Fallback tolerance if key is zero")]
    public float default_per_joint_tolerance = 12f;

    [Header("Runtime")]
    [Tooltip("Apply on Start")]
    public bool apply_first_on_start = true;
    [Tooltip("Update every frame")]
    public bool update_continuously = true;

    private int current_index = -1;
    private float last_driver_deg = 0f;
    private bool last_driver_valid = false;

    /* Unity */
    void Start()
    {
        if (apply_first_on_start)
        {
            int count = GetKeyCount();
            if (count > 0)
            {
                ApplyKeySprites(0);
                current_index = 0;
            }
        }
    }

    void Update()
    {
        if (!update_continuously)
        {
            return;
        }

        if (!IsConfigured())
        {
            Debug.LogWarning("Arm pose driver is not fully configured");
            return;
        }

        if (mode == Mode.SequenceByDriver)
        {
            StepSequence();
        }
        else
        {
            StepNearest();
        }
    }

    /* StepSequence
     * Advance current_index based on driver joint angle passing midpoints.
     */
    private void StepSequence()
    {
        int count = GetKeyCount();
        if (count <= 0)
        {
            return;
        }

        if (current_index < 0)
        {
            current_index = 0;
            ApplyKeySprites(current_index);
        }
        if (current_index >= count)
        {
            current_index = count - 1;
            ApplyKeySprites(current_index);
        }

        float driver_now = ReadDriverAngleZ();
        if (!last_driver_valid)
        {
            last_driver_deg = driver_now;
            last_driver_valid = true;
            return;
        }

        float move = Mathf.DeltaAngle(last_driver_deg, driver_now);
        last_driver_deg = driver_now;

        if (Mathf.Abs(move) < 0.0001f)
        {
            return;
        }

        bool increasing = move > 0f;

        if (increasing)
        {
            TryMoveToNext(driver_now);
        }
        else
        {
            TryMoveToPrev(driver_now);
        }
    }

    /* StepNearest
     * Choose key with smallest angular distance if within tolerance.
     */
    private void StepNearest()
    {
        int count = GetKeyCount();
        if (count <= 0)
        {
            return;
        }

        float ua = GetLocalZ(upper_arm_joint);
        float fa = GetLocalZ(forearm_joint);
        float ha = GetLocalZ(hand_joint);

        int best_index = -1;
        float best_score = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            ArmPoseLibrary.ArmPoseKey key = library.keys[i];

            float tol = key.per_joint_tolerance;
            if (tol <= 0f)
            {
                tol = default_per_joint_tolerance;
            }

            float d0 = Mathf.Abs(Mathf.DeltaAngle(ua, key.upper_arm_z));
            float d1 = Mathf.Abs(Mathf.DeltaAngle(fa, key.forearm_z));
            float d2 = Mathf.Abs(Mathf.DeltaAngle(ha, key.hand_z));

            bool within = d0 <= tol && d1 <= tol && d2 <= tol;
            if (!within)
            {
                continue;
            }

            float score = d0 + d1 + d2;
            if (score < best_score)
            {
                best_score = score;
                best_index = i;
            }
        }

        if (best_index >= 0 && best_index != current_index)
        {
            ApplyKeySprites(best_index);
            current_index = best_index;
        }
    }

    /* TryMoveToNext
     * Switch to next key when driver passes midpoint plus hysteresis.
     * @param driver_now Current driver angle in degrees
     */
    private void TryMoveToNext(float driver_now)
    {
        int count = GetKeyCount();
        if (count <= 1)
        {
            return;
        }

        int next = current_index + 1;
        if (next >= count)
        {
            if (!wrap_sequence)
            {
                return;
            }
            next = 0;
        }

        float a = GetDriverAngleFromKey(current_index);
        float b = GetDriverAngleFromKey(next);

        float mid = MidAngle(a, b);
        float trip = AddAngle(mid, hysteresis_degrees);

        bool passed = AngleIsBeyond(a, trip, driver_now);
        if (passed)
        {
            ApplyKeySprites(next);
            current_index = next;
        }
    }

    /* TryMoveToPrev
     * Switch to previous key when driver passes midpoint minus hysteresis.
     * @param driver_now Current driver angle in degrees
     */
    private void TryMoveToPrev(float driver_now)
    {
        int count = GetKeyCount();
        if (count <= 1)
        {
            return;
        }

        int prev = current_index - 1;
        if (prev < 0)
        {
            if (!wrap_sequence)
            {
                return;
            }
            prev = count - 1;
        }

        float a = GetDriverAngleFromKey(prev);
        float b = GetDriverAngleFromKey(current_index);

        float mid = MidAngle(a, b);
        float trip = AddAngle(mid, -hysteresis_degrees);

        bool passed = AngleIsBeyond(b, trip, driver_now);
        if (passed)
        {
            ApplyKeySprites(prev);
            current_index = prev;
        }
    }

    /* ApplyKeySprites
     * Swap sprites and collider to match a key index.
     * @param index Key index to apply
     */
    public void ApplyKeySprites(int index)
    {
        int count = GetKeyCount();
        if (index < 0)
        {
            return;
        }
        if (index >= count)
        {
            return;
        }

        ArmPoseLibrary.ArmPoseKey k = library.keys[index];

        if (upper_arm_renderer != null)
        {
            upper_arm_renderer.sprite = k.upper_arm_sprite;
        }
        if (forearm_renderer != null)
        {
            forearm_renderer.sprite = k.forearm_sprite;
        }
        if (hand_renderer != null)
        {
            hand_renderer.sprite = k.hand_sprite;
        }

        if (collider_bounds != null)
        {
            collider_bounds.offset = k.collider_offset;
            collider_bounds.size = k.collider_size;
            collider_bounds.direction = k.collider_direction;
        }
    }

    /* CaptureCurrentPose
     * Read current sprites, joint angles, and collider and append a key.
     */
    [ContextMenu("Capture Current Pose")]
    public void CaptureCurrentPose()
    {
        if (!IsConfigured())
        {
            Debug.LogWarning("Arm pose driver is not fully configured");
            return;
        }
        if (library == null)
        {
            Debug.LogWarning("Pose library is missing");
            return;
        }

        ArmPoseLibrary.ArmPoseKey key = new ArmPoseLibrary.ArmPoseKey();
        key.pose_name = "Pose " + library.keys.Count;

        if (upper_arm_renderer != null)
        {
            key.upper_arm_sprite = upper_arm_renderer.sprite;
        }
        else
        {
            key.upper_arm_sprite = null;
        }

        if (forearm_renderer != null)
        {
            key.forearm_sprite = forearm_renderer.sprite;
        }
        else
        {
            key.forearm_sprite = null;
        }

        if (hand_renderer != null)
        {
            key.hand_sprite = hand_renderer.sprite;
        }
        else
        {
            key.hand_sprite = null;
        }

        key.upper_arm_z = GetLocalZ(upper_arm_joint);
        key.forearm_z = GetLocalZ(forearm_joint);
        key.hand_z = GetLocalZ(hand_joint);

        if (collider_bounds != null)
        {
            key.collider_offset = collider_bounds.offset;
            key.collider_size = collider_bounds.size;
            key.collider_direction = collider_bounds.direction;
        }
        else
        {
            key.collider_offset = Vector2.zero;
            key.collider_size = Vector2.zero;
            key.collider_direction = CapsuleDirection2D.Vertical;
        }

        key.per_joint_tolerance = default_per_joint_tolerance;

        library.keys.Add(key);
        MarkAssetDirty(library);
    }

    /* SortByDriver
     * Sort keys by the driver joint angle ascending for sequence mode.
     */
    [ContextMenu("Sort Keys By Driver")]
    public void SortKeysByDriver()
    {
        int count = GetKeyCount();
        if (count <= 1)
        {
            return;
        }

        library.keys.Sort((a, b) =>
        {
            float av = GetDriverAngleFromKey(a);
            float bv = GetDriverAngleFromKey(b);
            float da = Mathf.DeltaAngle(0f, av);
            float db = Mathf.DeltaAngle(0f, bv);
            if (da < db) return -1;
            if (da > db) return 1;
            return 0;
        });

        MarkAssetDirty(library);
    }

    /* Utility */
    private bool IsConfigured()
    {
        if (upper_arm_renderer == null) return false;
        if (forearm_renderer == null) return false;
        if (hand_renderer == null) return false;
        if (upper_arm_joint == null) return false;
        if (forearm_joint == null) return false;
        if (hand_joint == null) return false;
        if (library == null) return false;
        return true;
    }

    private int GetKeyCount()
    {
        if (library == null)
        {
            return 0;
        }
        if (library.keys == null)
        {
            return 0;
        }
        return library.keys.Count;
    }

    private float GetLocalZ(Transform t)
    {
        if (t == null)
        {
            return 0f;
        }
        Vector3 e = t.localEulerAngles;
        return e.z;
    }

    private float ReadDriverAngleZ()
    {
        if (driver_joint == DriverJoint.UpperArm)
        {
            return GetLocalZ(upper_arm_joint);
        }
        if (driver_joint == DriverJoint.Hand)
        {
            return GetLocalZ(hand_joint);
        }
        return GetLocalZ(forearm_joint);
    }

    private float GetDriverAngleFromKey(int index)
    {
        ArmPoseLibrary.ArmPoseKey k = library.keys[index];
        return GetDriverAngleFromKey(k);
    }

    private float GetDriverAngleFromKey(ArmPoseLibrary.ArmPoseKey k)
    {
        if (driver_joint == DriverJoint.UpperArm)
        {
            return k.upper_arm_z;
        }
        if (driver_joint == DriverJoint.Hand)
        {
            return k.hand_z;
        }
        return k.forearm_z;
    }

    private static float MidAngle(float a, float b)
    {
        float d = Mathf.DeltaAngle(a, b);
        float m = a + d * 0.5f;
        return Normalize360(m);
    }

    private static float Normalize360(float a)
    {
        float x = a % 360f;
        if (x < 0f)
        {
            x += 360f;
        }
        return x;
    }

    private static float AddAngle(float a, float delta)
    {
        return Normalize360(a + delta);
    }

    /* AngleIsBeyond
     * Return true if moving from start toward target has passed sample.
     * @param start Start angle in degrees
     * @param sample Sample gate angle in degrees
     * @param current Current angle in degrees
     */
    private static bool AngleIsBeyond(float start, float sample, float current)
    {
        float to_sample = Mathf.DeltaAngle(start, sample);
        float to_current = Mathf.DeltaAngle(start, current);
        if (to_sample >= 0f)
        {
            return to_current >= to_sample;
        }
        return to_current <= to_sample;
    }

    /* MarkAssetDirty
     * Editor only utility to save asset changes from Play Mode captures.
     * @param obj Asset to mark dirty
     */
    private static void MarkAssetDirty(Object obj)
    {
#if UNITY_EDITOR
        if (obj == null)
        {
            return;
        }
        EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets();
#endif
    }
}
