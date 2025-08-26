using UnityEngine;
using UnityEngine.InputSystem;

/*
 * 2D ping-pong movement with local circular clamp and floor.
 * Uses Input System callbacks (Vector2 Move).
 */
[RequireComponent(typeof(Rigidbody2D))]
public class PingPongMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Max speed.")]
    public float max_speed = 12f;
    [Tooltip("Accel.")]
    public float acceleration = 60f;
    [Tooltip("Decel.")]
    public float deceleration = 30f;

    [Header("Bounds (local)")]
    [Tooltip("Radius^2 in local.")]
    public float clamp_radius_sq = 3.4f;
    [Tooltip("Min local Y.")]
    public float min_local_y = 0.4f;

    [Header("Input")]
    [Tooltip("Drag Move (Vector2).")]
    public InputActionReference move_action_ref;
    [Tooltip("Fallback name on PlayerInput.")]
    public string move_action_name = "Move";
    [Tooltip("Auto-bind from PlayerInput.")]
    public bool fetch_from_player_input_if_null = true;

    private Rigidbody2D rb;
    private InputAction move_action;
    private Vector2 move_input;

    /* Unity */
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
#else
        rb.drag = 0f;
        rb.angularDrag = 0f;
#endif
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /* Unity */
    void OnEnable()
    {
        bind_input();
    }

    /* Unity */
    void OnDisable()
    {
        unbind_input();
    }

    /* Input */
    private void bind_input()
    {
        if (move_action_ref != null)
        {
            if (move_action_ref.action != null)
            {
                move_action = move_action_ref.action;
            }
        }

        if (move_action == null)
        {
            if (fetch_from_player_input_if_null)
            {
                var player_input = GetComponent<PlayerInput>();
                if (player_input != null)
                {
                    var asset = player_input.actions;
                    if (asset != null)
                    {
                        try
                        {
                            move_action = asset.FindAction(move_action_name, true);
                        }
                        catch
                        {
                            move_action = null;
                        }
                    }
                }
            }
        }

        if (move_action != null)
        {
            move_action.performed += on_move_performed;
            move_action.canceled += on_move_canceled;
            if (!move_action.enabled)
            {
                move_action.Enable();
            }
        }
        else
        {
            Debug.LogWarning("[PingPongMovement] Move action not bound.");
        }
    }

    /* Input */
    private void unbind_input()
    {
        if (move_action != null)
        {
            move_action.performed -= on_move_performed;
            move_action.canceled -= on_move_canceled;
            move_action = null;
        }
        move_input = Vector2.zero;
    }

    /* Input */
    private void on_move_performed(InputAction.CallbackContext ctx)
    {
        move_input = ctx.ReadValue<Vector2>();
    }

    /* Input */
    private void on_move_canceled(InputAction.CallbackContext ctx)
    {
        move_input = Vector2.zero;
    }

    /* Unity */
    void FixedUpdate()
    {
        Vector2 input = move_input;
        if (input.sqrMagnitude > 1f)
        {
            input = input.normalized;
        }

        float dt = Time.fixedDeltaTime;

#if UNITY_6000_0_OR_NEWER
        Vector2 v = rb.linearVelocity;
#else
        Vector2 v = rb.velocity;
#endif

        if (input.sqrMagnitude > 1e-6f)
        {
            Vector2 target = input * max_speed;
            Vector2 delta_v = target - v;

            float max_step = acceleration * dt;
            float mag = delta_v.magnitude;
            if (mag > max_step)
            {
                float denom = mag;
                if (denom < 1e-6f) denom = 1e-6f;
                delta_v = delta_v * (max_step / denom);
            }

            rb.AddForce(rb.mass * delta_v, ForceMode2D.Impulse);
        }
        else
        {
            float speed = v.magnitude;
            if (speed > 0f)
            {
                float drop = deceleration * dt;
                float new_speed = speed - drop;
                if (new_speed < 0f) new_speed = 0f;
                if (!Mathf.Approximately(new_speed, speed))
                {
                    float denom = speed;
                    if (denom < 1e-6f) denom = 1e-6f;
                    Vector2 delta_v = (new_speed - speed) * (v / denom);
                    rb.AddForce(rb.mass * delta_v, ForceMode2D.Impulse);
                }
            }
        }

#if UNITY_6000_0_OR_NEWER
        Vector2 capped = rb.linearVelocity;
#else
        Vector2 capped = rb.velocity;
#endif
        float s = capped.magnitude;
        if (s > max_speed)
        {
            capped = capped * (max_speed / s);
        }

        Transform origin = transform.parent;
        Vector2 p_world = rb.position;
        Vector2 p_local;

        if (origin != null)
        {
            p_local = origin.InverseTransformPoint(p_world);
        }
        else
        {
            p_local = p_world;
        }

        bool changed = false;
        Vector2 clamped_local = p_local;

        float r2 = clamp_radius_sq;
        if (r2 < 0f) r2 = 0f;
        float r = 0f;
        if (r2 > 0f) r = Mathf.Sqrt(r2);

        if (clamped_local.y < min_local_y)
        {
            clamped_local.y = min_local_y;
            changed = true;
        }

        if (r2 > 0f)
        {
            float d2 = clamped_local.sqrMagnitude;
            if (d2 > r2)
            {
                if (clamped_local.y <= min_local_y + 1e-6f)
                {
                    float x_max = Mathf.Sqrt(Mathf.Max(0f, r2 - min_local_y * min_local_y));
                    if (clamped_local.x < -x_max) clamped_local.x = -x_max;
                    if (clamped_local.x > x_max) clamped_local.x = x_max;
                    clamped_local.y = min_local_y;
                }
                else
                {
                    if (clamped_local.sqrMagnitude > 1e-12f)
                    {
                        clamped_local = clamped_local.normalized * r;
                    }
                    if (clamped_local.y < min_local_y)
                    {
                        float x_max = Mathf.Sqrt(Mathf.Max(0f, r2 - min_local_y * min_local_y));
                        if (clamped_local.x < -x_max) clamped_local.x = -x_max;
                        if (clamped_local.x > x_max) clamped_local.x = x_max;
                        clamped_local.y = min_local_y;
                    }
                }
                changed = true;
            }
        }

        if (changed)
        {
            Vector2 clamped_world;
            if (origin != null)
            {
                clamped_world = origin.TransformPoint(clamped_local);
            }
            else
            {
                clamped_world = clamped_local;
            }

            rb.position = clamped_world;

            Vector2 v_world = capped;
            Vector2 v_local;

            if (origin != null)
            {
                v_local = origin.InverseTransformVector(v_world);
            }
            else
            {
                v_local = v_world;
            }

            bool on_circle = false;
            float diff = clamped_local.sqrMagnitude - r2;
            if (r2 > 0f)
            {
                if (Mathf.Abs(diff) <= 1e-4f) on_circle = true;
            }

            if (on_circle)
            {
                if (clamped_local.sqrMagnitude > 1e-12f)
                {
                    Vector2 n_local = clamped_local.normalized;
                    float radial = Vector2.Dot(v_local, n_local);
                    if (radial > 0f)
                    {
                        v_local = v_local - radial * n_local;
                    }
                }
            }

            if (clamped_local.y <= min_local_y + 1e-6f)
            {
                if (v_local.y < 0f) v_local.y = 0f;
            }

            if (origin != null)
            {
                capped = origin.TransformVector(v_local);
            }
            else
            {
                capped = v_local;
            }
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = capped;
#else
        rb.velocity = capped;
#endif
    }
}
