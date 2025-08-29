using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PingPongMove2 : MonoBehaviour
{
    [Header("Owner Source")]
    [SerializeField] private PlayerInput owner;

    [Header("Movement")]
    public float max_speed = 12f;
    public float acceleration = 60f;
    public float deceleration = 30f;

    [Header("Bounds Local")]
    public float clamp_radius_sq = 3.4f;
    public float min_local_y = 0.4f;

    [Header("Input Mapping")]
    public InputActionReference move_action_ref;
    public string move_action_name = "Move";

    [Header("Input Reliability")]
    [Tooltip("Poll each fixed update")]
    public bool also_poll_each_fixed_update = true;

    [Header("Mirror")]
    [Tooltip("Use negative scale to flip input and bounds.")]
    public bool auto_mirror_from_scale = true;
    [Tooltip("Mirror X when auto is off.")]
    public bool mirror_x = false;
    [Tooltip("Mirror Y when auto is off.")]
    public bool mirror_y = false;

    private Rigidbody2D rigidbody2d;
    private InputAction move_action;
    private Vector2 move_input;
    private bool is_subscribed;

    /*
    Set or change the owning PlayerInput and update bindings.
    @param player_input The PlayerInput that provides the action asset for this mover.
    */
    public void SetOwner(PlayerInput player_input)
    {
        if (owner == player_input)
        {
            return;
        }

        if (is_subscribed && owner != null)
        {
            owner.onControlsChanged -= OnControlsChanged;
            is_subscribed = false;
        }

        owner = player_input;

        if (owner != null && !is_subscribed)
        {
            owner.onControlsChanged += OnControlsChanged;
            is_subscribed = true;
        }

        BindInput();
    }

    /*
    Cache and configure the Rigidbody2D for controlled movement.
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

        if (rigidbody2d.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning("Rigidbody type is not dynamic. AddForce will not move it.");
        }

        if (!rigidbody2d.simulated)
        {
            Debug.LogWarning("Rigidbody is not simulated. Physics will not run.");
        }
    }

    /*
    Subscribe to owner changes and bind input when enabled.
    */
    void OnEnable()
    {
        if (owner != null && !is_subscribed)
        {
            owner.onControlsChanged += OnControlsChanged;
            is_subscribed = true;
        }
        BindInput();
    }

    /*
    Unsubscribe and unbind input when disabled.
    */
    void OnDisable()
    {
        if (is_subscribed && owner != null)
        {
            owner.onControlsChanged -= OnControlsChanged;
            is_subscribed = false;
        }
        UnbindInput();
    }

    /*
    Rebind input if the PlayerInput control scheme or devices change.
    @param player_input The PlayerInput that triggered the change.
    */
    private void OnControlsChanged(PlayerInput player_input)
    {
        BindInput();
    }

    /*
    Resolve the movement action from the owner action asset and subscribe to callbacks.
    */
    private void BindInput()
    {
        UnbindInput();

        if (owner == null)
        {
            return;
        }

        if (owner.actions == null)
        {
            return;
        }

        InputActionAsset action_asset = owner.actions;

        if (move_action_ref != null && move_action_ref.action != null)
        {
            move_action = action_asset.FindAction(move_action_ref.action.id);
        }
        else
        {
            Debug.LogWarning("Could not find action id.");
        }

        if (move_action == null && !string.IsNullOrEmpty(move_action_name))
        {
            move_action = action_asset.FindAction(move_action_name, throwIfNotFound: false);
        }

        if (move_action != null)
        {
            move_action.performed += OnMovePerformed;
            move_action.canceled += OnMoveCanceled;
            move_action.Enable();
            move_input = move_action.ReadValue<Vector2>();
        }
        else
        {
            Debug.LogWarning("Move action not found in owner asset.");
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
    Update cached input when movement is performed.
    @param context The callback context providing the input vector.
    */
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        move_input = context.ReadValue<Vector2>();
    }

    /*
    Clear cached input when movement is canceled.
    @param context The callback context for the canceled event.
    */
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        move_input = Vector2.zero;
    }

    /*
    Compute the active mirror vector.
    Returns (+1 or -1) per axis depending on settings or scale.
    */
    private Vector2 GetMirrorVector()
    {
        Vector2 m = Vector2.one;

        if (auto_mirror_from_scale)
        {
            Vector3 s = transform.lossyScale;

            if (s.x < 0f)
            {
                m.x = -1f;
            }
            if (s.y < 0f)
            {
                m.y = -1f;
            }
        }
        else
        {
            if (mirror_x)
            {
                m.x = -1f;
            }
            if (mirror_y)
            {
                m.y = -1f;
            }
        }

        return m;
    }

    /*
    Multiply a vector by the mirror vector.
    @param v Vector to mirror.
    @param m Mirror vector with components +1 or -1.
    */
    private static Vector2 ApplyMirror(Vector2 v, Vector2 m)
    {
        Vector2 r;
        r.x = v.x * m.x;
        r.y = v.y * m.y;
        return r;
    }

    /*
    Drive the Rigidbody2D using acceleration and deceleration while enforcing mirrored local bounds.
    */
    void FixedUpdate()
    {
        if (move_action == null)
        {
            Debug.LogWarning("Move action is null.");
            return;
        }

        if (also_poll_each_fixed_update)
        {
            move_input = move_action.ReadValue<Vector2>();
        }

        Vector2 mirror = GetMirrorVector();

        Vector2 input_vector = move_input;

        if (input_vector.sqrMagnitude > 1f)
        {
            input_vector = input_vector.normalized;
        }

        /* Mirror input to match facing */
        input_vector = ApplyMirror(input_vector, mirror);

        float delta_time = Time.fixedDeltaTime;

#if UNITY_6000_0_OR_NEWER
        Vector2 velocity = rigidbody2d.linearVelocity;
#else
        Vector2 velocity = rigidbody2d.velocity;
#endif

        if (input_vector.sqrMagnitude > 1e-6f)
        {
            Vector2 target_velocity = input_vector * max_speed;
            Vector2 delta_velocity = target_velocity - velocity;
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
        else
        {
            float speed = velocity.magnitude;
            if (speed > 0f)
            {
                float new_speed = Mathf.Max(0f, speed - deceleration * delta_time);
                if (!Mathf.Approximately(new_speed, speed))
                {
                    Vector2 normalized_velocity;
                    if (speed < 1e-6f)
                    {
                        normalized_velocity = velocity / 1e-6f;
                    }
                    else
                    {
                        normalized_velocity = velocity / speed;
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
        }

        Transform origin_transform = transform.parent;
        Vector2 position_world = rigidbody2d.position;
        Vector2 position_local;

        if (origin_transform != null)
        {
            position_local = origin_transform.InverseTransformPoint(position_world);
        }
        else
        {
            position_local = position_world;
        }

        /* Work in mirrored-local space so floor/circle rules stay identical */
        Vector2 pos_local_m = ApplyMirror(position_local, mirror);

        bool position_changed = false;
        Vector2 clamped_local_m = pos_local_m;

        float radius_squared = Mathf.Max(0f, clamp_radius_sq);
        float radius;

        if (radius_squared > 0f)
        {
            radius = Mathf.Sqrt(radius_squared);
        }
        else
        {
            radius = 0f;
        }

        if (clamped_local_m.y < min_local_y)
        {
            clamped_local_m.y = min_local_y;
            position_changed = true;
        }

        if (radius_squared > 0f)
        {
            float distance_squared = clamped_local_m.sqrMagnitude;
            if (distance_squared > radius_squared)
            {
                if (clamped_local_m.y <= min_local_y + 1e-6f)
                {
                    float x_limit = Mathf.Sqrt(Mathf.Max(0f, radius_squared - min_local_y * min_local_y));
                    clamped_local_m.x = Mathf.Clamp(clamped_local_m.x, -x_limit, x_limit);
                    clamped_local_m.y = min_local_y;
                }
                else
                {
                    if (clamped_local_m.sqrMagnitude > 1e-12f)
                    {
                        clamped_local_m = clamped_local_m.normalized * radius;
                    }

                    if (clamped_local_m.y < min_local_y)
                    {
                        float x_limit = Mathf.Sqrt(Mathf.Max(0f, radius_squared - min_local_y * min_local_y));
                        clamped_local_m.x = Mathf.Clamp(clamped_local_m.x, -x_limit, x_limit);
                        clamped_local_m.y = min_local_y;
                    }
                }

                position_changed = true;
            }
        }

        if (position_changed)
        {
            Vector2 clamped_local = ApplyMirror(clamped_local_m, mirror);

            Vector2 clamped_world_position;
            if (origin_transform != null)
            {
                clamped_world_position = origin_transform.TransformPoint(clamped_local);
            }
            else
            {
                clamped_world_position = clamped_local;
            }

            rigidbody2d.position = clamped_world_position;

            Vector2 velocity_world = capped_velocity;
            Vector2 velocity_local;

            if (origin_transform != null)
            {
                velocity_local = origin_transform.InverseTransformVector(velocity_world);
            }
            else
            {
                velocity_local = velocity_world;
            }

            Vector2 vel_local_m = ApplyMirror(velocity_local, mirror);

            bool on_circle = radius_squared > 0f && Mathf.Abs(clamped_local_m.sqrMagnitude - radius_squared) <= 1e-4f;
            if (on_circle && clamped_local_m.sqrMagnitude > 1e-12f)
            {
                Vector2 normal_local_m = clamped_local_m.normalized;
                float radial = Vector2.Dot(vel_local_m, normal_local_m);
                if (radial > 0f)
                {
                    vel_local_m -= radial * normal_local_m;
                }
            }

            if (clamped_local_m.y <= min_local_y + 1e-6f && vel_local_m.y < 0f)
            {
                vel_local_m.y = 0f;
            }

            velocity_local = ApplyMirror(vel_local_m, mirror);

            if (origin_transform != null)
            {
                capped_velocity = origin_transform.TransformVector(velocity_local);
            }
            else
            {
                capped_velocity = velocity_local;
            }

            float speed_post = capped_velocity.magnitude;
            if (speed_post > max_speed)
            {
                float denom_post = speed_post;
                if (denom_post < 1e-6f)
                {
                    denom_post = 1e-6f;
                }
                capped_velocity *= (max_speed / denom_post);
            }
        }

#if UNITY_6000_0_OR_NEWER
        rigidbody2d.linearVelocity = capped_velocity;
#else
        rigidbody2d.velocity = capped_velocity;
#endif
    }
}
