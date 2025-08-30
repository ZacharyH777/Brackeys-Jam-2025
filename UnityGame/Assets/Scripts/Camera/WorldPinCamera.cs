using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Camera/World Pinned Camera")]
public sealed class WorldPinCamera : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("Global position set every frame")]
    public Vector3 fixed_world_position = new Vector3(0f, -1000f, 10f);

    [Header("Look At")]
    [Tooltip("Optional target to face")]
    public Transform ui_target;
    [Tooltip("Up direction for look")]
    public Vector3 up_direction = Vector3.up;

    [Header("Viewport Rect")]
    [Tooltip("Place on right side")]
    public bool place_on_right_side = true;

    private Camera camera_component;

    /*
    * Cache camera component and set viewport side.
    * Ensures a camera reference is available and viewport is pinned.
    * @param none
    */
    void Awake()
    {
        camera_component = GetComponent<Camera>();
        if (camera_component == null)
        {
            Debug.LogWarning("Camera component was not found");
            return;
        }

        ApplyViewportSide();
    }

    /*
    * Apply pose after parent updates.
    * Runs every frame.
    * @param none
    */
    void LateUpdate()
    {
        ApplyPose();
    }

    /*
    * Apply pose before culling and render.
    * Ensures correctness at render time.
    * @param none
    */
    void OnPreCull()
    {
        ApplyPose();
    }

    /*
    * Sets world position and optional look at.
    * @param none
    */
    private void ApplyPose()
    {
        transform.position = fixed_world_position;

        if (ui_target != null)
        {
            Vector3 dir = ui_target.position - transform.position;
            if (dir.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.LookRotation(dir, up_direction);
            }
        }
    }

    /*
    * Sets camera rect to left or right half.
    * Uses place_on_right_side to choose side.
    * @param none
    */
    private void ApplyViewportSide()
    {
        Rect viewport_rect = camera_component.rect;
        viewport_rect.y = 0f;
        viewport_rect.width = 0.5f;
        viewport_rect.height = 1f;

        if (place_on_right_side)
        {
            viewport_rect.x = 0.5f;
        }
        else
        {
            viewport_rect.x = 0f;
        }

        camera_component.rect = viewport_rect;
    }
}
