using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PingPongMovement : MonoBehaviour
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

    [Header("Input Map")]
    public InputActionReference move_action_ref;
    public string move_action_name = "Move";

    [Header("Reliability")]
    [Tooltip("Poll value each fixed update.")]
    public bool also_poll_each_fixed_update = true;

    [Header("Mirror")]
    [Tooltip("Auto read negative axis from scale.")]
    public bool auto_mirror_from_scale = true;
    [Tooltip("Manual X when auto is off.")]
    public bool mirror_x = false;
    [Tooltip("Manual Y when auto is off.")]
    public bool mirror_y = false;
    [Tooltip("Mirror input vector.")]
    public bool mirror_inputs = true;
    [Tooltip("Mirror bounds and floor.")]
    public bool mirror_bounds = true;

    private Rigidbody2D rigidbody2d;
    private InputAction move_action;
    private Vector2 move_input;
    private bool is_subscribed;

    /*
    Set or change owner and rebind input.
    @param player_input PlayerInput that provides the actions.
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
    Cache rigidbody and configure baseline physics.
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
            Debug.LogWarning("Rigidbody is not dynamic.");
        }

        if (!rigidbody2d.simulated)
        {
            Debug.LogWarning("Rigidbody is not simulated.");
        }
    }

    /*
    Subscribe and bind on enable.
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
    Unsubscribe and unbind on disable.
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
    Rebind after control scheme or device changes.
    @param player_input Source PlayerInput that changed.
    */
    private void OnControlsChanged(PlayerInput player_input)
    {
        BindInput();
    }

    /*
    Bind the move action from the owner asset.
    */
    private void BindInput()
    {
        UnbindInput();

        if (owner == null || owner.actions == null)
        {
            return;
        }

        InputActionAsset action_asset = owner.actions;

        if (move_action_ref != null && move_action_ref.action != null)
        {
            move_action = action_asset.FindAction(move_action_ref.action.id);
        }
        else if (!string.IsNullOrEmpty(move_action_name))
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
            Debug.LogWarning("Move action was not found.");
        }
    }

    /*
    Unsubscribe input callbacks and clear cached state.
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
    Cache input vector when performed.
    @param context Callback context from the action.
    */
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        move_input = context.ReadValue<Vector2>();
    }

    /*
    Clear input vector on cancel.
    @param context Callback context from the action.
    */
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        move_input = Vector2.zero;
    }

    /*
    Compute mirror vector from scale or manual toggles.
    @return Vector with +1 or -1 per axis.
    */
    private Vector2 GetMirrorVector()
    {
        Vector2 m = Vector2.one;

        if (auto_mirror_from_scale)
        {
            Vector3 s = transform.lossyScale;
            if (s.x < 0f) m.x = -1f;
            if (s.y < 0f) m.y = -1f;
        }
        else
        {
            if (mirror_x) m.x = -1f;
            if (mirror_y) m.y = -1f;
        }

        return m;
    }

    /*
    Multiply by mirror vector.
    @param v Input vector.
    @param m Mirror vector.
    @return Mirrored vector.
    */
    private static Vector2 ApplyMirror(Vector2 v, Vector2 m)
    {
        return new Vector2(v.x * m.x, v.y * m.y);
    }

    /*
    Physics update with optional input mirroring and bounds mirroring.
    */
    void FixedUpdate()
    {
        if (move_action == null)
        {
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
            input_vector.Normalize();
        }

        if (mirror_inputs)
        {
            input_vector = ApplyMirror(input_vector, mirror);
        }

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
                delta_velocity *= max_step / delta_magnitude;
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
                    Vector2 normalized_velocity = (speed < 1e-6f) ? velocity / 1e-6f : velocity / speed;
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
            capped_velocity *= max_speed / speed_magnitude;
        }

        Transform origin_transform = transform.parent;
        Vector2 position_world = rigidbody2d.position;
        Vector2 position_local = (origin_transform != null) ? origin_transform.InverseTransformPoint(position_world) : position_world;

        Vector2 clamp_space_pos = mirror_bounds ? ApplyMirror(position_local, mirror) : position_local;

        bool position_changed = false;
        Vector2 clamped_clamp_space_pos = clamp_space_pos;

        float radius_squared = Mathf.Max(0f, clamp_radius_sq);

        if (clamped_clamp_space_pos.y < min_local_y)
        {
            clamped_clamp_space_pos.y = min_local_y;
            position_changed = true;
        }

        if (radius_squared > 0f)
        {
            if (clamped_clamp_space_pos.sqrMagnitude > radius_squared)
            {
                float radius = Mathf.Sqrt(radius_squared);
                if (clamped_clamp_space_pos.y <= min_local_y + 1e-6f)
                {
                    float x_limit = Mathf.Sqrt(Mathf.Max(0f, radius_squared - min_local_y * min_local_y));
                    clamped_clamp_space_pos.x = Mathf.Clamp(clamped_clamp_space_pos.x, -x_limit, x_limit);
                    clamped_clamp_space_pos.y = min_local_y;
                }
                else
                {
                    if (clamped_clamp_space_pos.sqrMagnitude > 1e-12f)
                    {
                        clamped_clamp_space_pos = clamped_clamp_space_pos.normalized * radius;
                    }

                    if (clamped_clamp_space_pos.y < min_local_y)
                    {
                        float x_limit = Mathf.Sqrt(Mathf.Max(0f, radius_squared - min_local_y * min_local_y));
                        clamped_clamp_space_pos.x = Mathf.Clamp(clamped_clamp_space_pos.x, -x_limit, x_limit);
                        clamped_clamp_space_pos.y = min_local_y;
                    }
                }
                position_changed = true;
            }
        }

        if (position_changed)
        {
            Vector2 clamped_local = mirror_bounds ? ApplyMirror(clamped_clamp_space_pos, mirror) : clamped_clamp_space_pos;
            Vector2 clamped_world_position = (origin_transform != null) ? origin_transform.TransformPoint(clamped_local) : clamped_local;
            rigidbody2d.position = clamped_world_position;

            Vector2 velocity_world = capped_velocity;
            Vector2 velocity_local = (origin_transform != null) ? origin_transform.InverseTransformVector(velocity_world) : velocity_world;
            Vector2 clamp_space_vel = mirror_bounds ? ApplyMirror(velocity_local, mirror) : velocity_local;

            bool on_circle = radius_squared > 0f && Mathf.Abs(clamped_clamp_space_pos.sqrMagnitude - radius_squared) <= 1e-4f;
            if (on_circle && clamped_clamp_space_pos.sqrMagnitude > 1e-12f)
            {
                Vector2 normal_local = clamped_clamp_space_pos.normalized;
                float radial = Vector2.Dot(clamp_space_vel, normal_local);
                if (radial > 0f) clamp_space_vel -= radial * normal_local;
            }

            if (clamped_clamp_space_pos.y <= min_local_y + 1e-6f && clamp_space_vel.y < 0f)
            {
                clamp_space_vel.y = 0f;
            }

            velocity_local = mirror_bounds ? ApplyMirror(clamp_space_vel, mirror) : clamp_space_vel;
            capped_velocity = (origin_transform != null) ? origin_transform.TransformVector(velocity_local) : velocity_local;

            float speed_post = capped_velocity.magnitude;
            if (speed_post > max_speed)
            {
                capped_velocity *= max_speed / speed_post;
            }
        }

#if UNITY_6000_0_OR_NEWER
        rigidbody2d.linearVelocity = capped_velocity;
#else
        rigidbody2d.velocity = capped_velocity;
#endif
    }
}
