using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Simple 2D player movement using Input System callbacks (Vector2 Move).
 */
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Max speed.")]
    public float max_speed = 12f;
    [Tooltip("Accel.")]
    public float acceleration = 60f;
    [Tooltip("Decel.")]
    public float deceleration = 80f;

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
        rb.angularDamping = 0f;
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
            Debug.LogWarning("[PlayerMovement2D] Move action not bound.");
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
        Vector2 in2 = move_input;
        if (in2.sqrMagnitude > 1f)
        {
            in2 = in2.normalized;
        }

        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;

        if (in2.sqrMagnitude > 1e-6f)
        {
            Vector2 target = in2 * max_speed;
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

        Vector2 vp = rb.linearVelocity;
        if (vp.magnitude > max_speed)
        {
            float denom = vp.magnitude;
            if (denom < 1e-6f) denom = 1e-6f;
            rb.linearVelocity = vp * (max_speed / denom);
        }
    }
}
