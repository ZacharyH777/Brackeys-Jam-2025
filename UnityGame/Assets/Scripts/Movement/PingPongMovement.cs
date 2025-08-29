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
    Multiply by mirror vector.
    @param v Input vector.
    @param m Mirror vector.
    @return Mirrored vector.
    */
    private static Vector2 ApplyMirror(Vector2 v, Vector2 m)
    {
        Vector2 r;
        r.x = v.x * m.x;
        r.y = v.y * m.y;
        return r;
    }

    /*
    Physics update with optional input mirroring and bounds mirroring.
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
                float denom = delta_magnitude;

                if (denom < 1e-6f)
                {
                    denom = 1e-6f;
                }

                delta_velocity *= max_step / denom;
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

            capped_velocity *= max_speed / denom_speed;
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

        Vector2 clamp_space_pos = position_local;

        if (mirror_bounds)
        {
            clamp_space_pos = ApplyMirror(position_local, mirror);
        }

        bool position_changed = false;
        Vector2 clamped_clamp_space_pos = clamp_space_pos;

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

        if (clamped_clamp_space_pos.y < min_local_y)
        {
            clamped_clamp_space_pos.y = min_local_y;
            position_changed = true;
        }

        if (radius_squared > 0f)
        {
            float distance_squared = clamped_clamp_space_pos.sqrMagnitude;

            if (distance_squared > radius_squared)
            {
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
            Vector2 clamped_local;

            if (mirror_bounds)
            {
                clamped_local = ApplyMirror(clamped_clamp_space_pos, mirror);
            }
            else
            {
                clamped_local = clamped_clamp_space_pos;
            }

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

            Vector2 clamp_space_vel = velocity_local;

            if (mirror_bounds)
            {
                clamp_space_vel = ApplyMirror(velocity_local, mirror);
            }

            bool on_circle = radius_squared > 0f && Mathf.Abs(clamped_clamp_space_pos.sqrMagnitude - radius_squared) <= 1e-4f;

            if (on_circle && clamped_clamp_space_pos.sqrMagnitude > 1e-12f)
            {
                Vector2 normal_local = clamped_clamp_space_pos.normalized;
                float radial = Vector2.Dot(clamp_space_vel, normal_local);

                if (radial > 0f)
                {
                    clamp_space_vel -= radial * normal_local;
                }
            }

            if (clamped_clamp_space_pos.y <= min_local_y + 1e-6f && clamp_space_vel.y < 0f)
            {
                clamp_space_vel.y = 0f;
            }

            if (mirror_bounds)
            {
                velocity_local = ApplyMirror(clamp_space_vel, mirror);
            }
            else
            {
                velocity_local = clamp_space_vel;
            }

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

                capped_velocity *= max_speed / denom_post;
            }
        }

#if UNITY_6000_0_OR_NEWER
        rigidbody2d.linearVelocity = capped_velocity;
#else
        rigidbody2d.velocity = capped_velocity;
#endif
    }
}
