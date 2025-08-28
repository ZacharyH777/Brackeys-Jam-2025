using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Owner")]
    public PlayerInput player_input;

    [Header("Targets")]
    [Tooltip("Target uses same PlayerInput clone")]
    public PingPongMovement ping_pong_target;

    [Header("Movement")]
    public float max_speed = 12f;
    public float acceleration = 60f;
    public float deceleration = 80f;

    [Header("Input")]
    [Tooltip("Action reference used for mapping by id")]
    public InputActionReference move_action_ref;
    public string move_action_name = "Move";
    public bool fetch_from_player_input_if_null = true;
    [Tooltip("Invert controls")]
    public bool invert_controls = false;

    private Rigidbody2D rigidbody2d;
    private InputAction move_action;
    private Vector2 move_input;

    /*
    Ensure rigidbody2d is configured and a PlayerInput is available.
    */
    void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        rigidbody2d.gravityScale = 0f;
        rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
#if UNITY_6000_0_OR_NEWER
        rigidbody2d.linearDamping = 0f;
        rigidbody2d.angularDamping = 0f;
#else
        rigidbody2d.drag = 0f;
        rigidbody2d.angularDrag = 0f;
#endif
        rigidbody2d.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (player_input == null)
        {
            player_input = GetComponent<PlayerInput>();
            if (player_input == null)
            {
                player_input = GetComponentInParent<PlayerInput>();
            }
        }
    }

    /*
    Subscribe to control changes and bind actions.
    */
    void OnEnable()
    {
        if (player_input != null)
        {
            player_input.onControlsChanged += OnControlsChanged;
        }
        BindInput();
    }

    /*
    Unsubscribe and unbind when disabled.
    */
    void OnDisable()
    {
        if (player_input != null)
        {
            player_input.onControlsChanged -= OnControlsChanged;
        }
        UnbindInput();
    }

    /*
    Rebind when the control scheme or devices change.
    @param changed_input The PlayerInput that changed.
    */
    private void OnControlsChanged(PlayerInput changed_input)
    {
        BindInput();
    }

    /*
    Resolve and enable the move action from the PlayerInput or the reference.
    */
    private void BindInput()
    {
        UnbindInput();

        InputActionAsset action_asset = null;
        if (player_input != null)
        {
            action_asset = player_input.actions;
        }

        if (action_asset != null)
        {
            if (move_action_ref != null && move_action_ref.action != null)
            {
                move_action = action_asset.FindAction(move_action_ref.action.id);
            }

            if (move_action == null && !string.IsNullOrEmpty(move_action_name))
            {
                move_action = action_asset.FindAction(move_action_name, throwIfNotFound: false);
            }
        }
        else
        {
            if (!fetch_from_player_input_if_null && move_action_ref != null)
            {
                move_action = move_action_ref.action;
            }
        }

        if (move_action != null)
        {
            move_action.performed += OnMovePerformed;
            move_action.canceled += OnMoveCanceled;

            if (!move_action.enabled)
            {
                Debug.Log("Enable player movement");

                if (ping_pong_target != null && player_input != null)
                {
                    ping_pong_target.SetOwner(player_input);
                }

                move_action.Enable();
            }
        }
        else
        {
            Debug.LogWarning("Move action not bound. Ensure PlayerInput and matching action");
        }
    }

    /*
    Remove callbacks and clear state.
    */
    private void UnbindInput()
    {
        if (move_action != null)
        {
            move_action.performed -= OnMovePerformed;
            move_action.canceled -= OnMoveCanceled;
            move_action = null;
        }
        move_input = Vector2.zero;
    }

    /*
    Cache the latest input vector from the action.
    @param context Callback context from the Input System.
    */
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        move_input = context.ReadValue<Vector2>();
    }

    /*
    Clear the input vector when the action is canceled.
    @param context Callback context from the Input System.
    */
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        move_input = Vector2.zero;
    }

    /*
    Apply acceleration or deceleration and cap speed while keeping motion stable.
    */
    void FixedUpdate()
    {
        Vector2 input_vector = move_input;

        if (input_vector.sqrMagnitude > 1f)
        {
            input_vector = input_vector.normalized;
        }

        float delta_time = Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
        Vector2 current_velocity = rigidbody2d.linearVelocity;
#else
        Vector2 current_velocity = rigidbody2d.velocity;
#endif

        if (invert_controls)
        {
            ApplyAccelerationFromInputInverted(input_vector, delta_time, current_velocity);
        }
        else
        {
            ApplyAccelerationFromInput(input_vector, delta_time, current_velocity);
        }

        if (input_vector.sqrMagnitude <= 1e-6f)
        {
#if UNITY_6000_0_OR_NEWER
            Vector2 velocity_after_accel = rigidbody2d.linearVelocity;
#else
            Vector2 velocity_after_accel = rigidbody2d.velocity;
#endif
            float speed = velocity_after_accel.magnitude;
            if (speed > 0f)
            {
                float new_speed = Mathf.Max(0f, speed - deceleration * delta_time);
                if (!Mathf.Approximately(new_speed, speed))
                {
                    Vector2 normalized_velocity;
                    if (speed < 1e-6f)
                    {
                        normalized_velocity = velocity_after_accel / 1e-6f;
                    }
                    else
                    {
                        normalized_velocity = velocity_after_accel / speed;
                    }

                    Vector2 delta_velocity = (new_speed - speed) * normalized_velocity;
                    rigidbody2d.AddForce(rigidbody2d.mass * delta_velocity, ForceMode2D.Impulse);
                }
            }
        }

#if UNITY_6000_0_OR_NEWER
        Vector2 capped_velocity = rigidbody2d.linearVelocity;
#else
        Vector2 capped_velocity = rigidbody2d.velocity;
#endif
        float speed_magnitude = capped_velocity.magnitude;

        if (speed_magnitude > max_speed)
        {
            float denom_speed = speed_magnitude;
            if (denom_speed < 1e-6f)
            {
                denom_speed = 1e-6f;
            }
            capped_velocity *= (max_speed / denom_speed);
#if UNITY_6000_0_OR_NEWER
            rigidbody2d.linearVelocity = capped_velocity;
#else
            rigidbody2d.velocity = capped_velocity;
#endif
        }
    }

    /*
    Apply acceleration from input using normal controls.
    @param input_vector Input from user in XY.
    @param delta_time Fixed delta time.
    @param current_velocity Current rigidbody velocity.
    */
    private void ApplyAccelerationFromInput(Vector2 input_vector, float delta_time, Vector2 current_velocity)
    {
        if (input_vector.sqrMagnitude > 1e-6f)
        {
            Vector2 target_velocity = input_vector * max_speed;
            Vector2 delta_velocity = target_velocity - current_velocity;
            float max_step = acceleration * delta_time;
            float delta_magnitude = delta_velocity.magnitude;

            if (delta_magnitude > max_step)
            {
                float denom = delta_magnitude;
                if (denom < 1e-6f)
                {
                    denom = 1e-6f;
                }
                delta_velocity *= (max_step / denom);
            }

            rigidbody2d.AddForce(rigidbody2d.mass * delta_velocity, ForceMode2D.Impulse);
        }
    }

    /*
    Apply acceleration from input using inverted controls.
    @param input_vector Input from user in XY.
    @param delta_time Fixed delta time.
    @param current_velocity Current rigidbody velocity.
    */
    private void ApplyAccelerationFromInputInverted(Vector2 input_vector, float delta_time, Vector2 current_velocity)
    {
        Vector2 inverted = -input_vector;
        ApplyAccelerationFromInput(inverted, delta_time, current_velocity);
    }
}
