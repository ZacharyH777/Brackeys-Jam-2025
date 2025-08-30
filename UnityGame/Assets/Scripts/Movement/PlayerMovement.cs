using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("Owner / Input")]
    [Tooltip("PlayerInput is provided by the spawner later; we bind when it arrives.")]
    public PlayerInput player_input; // can be null at Awake; we late-bind
    [Tooltip("If true, we periodically look up PlayerInput again until found.")]
    public bool late_bind_player_input = true;
    [Tooltip("Seconds between late-bind checks for PlayerInput.")]
    public float late_bind_interval = 0.25f;

    private int _lastServeFireFrame = -1;
    [SerializeField] private bool _debugServe = false;

    [Header("Targets")]
    [Tooltip("Receives the PlayerInput clone when available.")]
    public PingPongMovement ping_pong_target;

    public PlayerOwner player_owner;

    [Header("Movement")]
    public float max_speed = 12f;
    public float acceleration = 60f;
    public float deceleration = 80f;

    [Header("Input (Move)")]
    [Tooltip("Prefer: assign the Move action here. We'll resolve by ID in the cloned asset.")]
    public InputActionReference move_action_ref;
    public string move_action_name = "Move";
    public bool fetch_from_player_input_if_null = true;
    [Tooltip("Invert XY input.")]
    public bool invert_controls = false;

    [Header("Input (Serve)")]
    [Tooltip("Optional dedicated Serve action (Keyboard Space + Gamepad South/A). Resolved by ID from the cloned asset.")]
    public InputActionReference serve_action_ref;
    [Tooltip("Fallback name if no reference is provided or ID lookup fails.")]
    public string serve_action_name = "Serve";
    [Tooltip("If true and Serve action isn't found, poll this player's devices for Space/A.")]
    public bool serve_device_fallback = true;

    private Rigidbody2D rigidbody2d;

    // actions
    private InputAction move_action;
    private InputAction serve_action;

    // cached inputs
    private Vector2 move_input;

    // serve edge-detect
    private bool serve_prev_pressed;

    // late-bind timer
    private float next_bind_check_time;

    /*
    Ensure rigidbody2d is configured.
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

        // Try an initial grab; spawner may provide it later.
        if (player_input == null)
        {
            player_input = GetComponent<PlayerInput>() ?? GetComponentInParent<PlayerInput>();
        }
    }

    /*
    Subscribe to control changes and bind actions if already present.
    */
    void OnEnable()
    {
        if (player_input != null)
        {
            player_input.onControlsChanged += OnControlsChanged;
        }
        BindInput(); // safe if player_input is still null; we late-bind in Update too
    }

    /*
    Unsubscribe and unbind.
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
    */
    private void OnControlsChanged(PlayerInput changed_input)
    {
        // Re-resolve actions from the (possibly new) cloned asset
        BindInput();
    }

    /*
    Try to resolve Move and Serve actions from the PlayerInput's cloned actions asset.
    Will also hand the PlayerInput to ping_pong_target once.
    */
    private void BindInput()
    {
        // Clear previous bindings first
        UnbindInput();

        // If the spawner hasn't attached PlayerInput yet, we’ll try again in Update
        InputActionAsset action_asset = (player_input != null) ? player_input.actions : null;

        // --- MOVE ---
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
        else if (!fetch_from_player_input_if_null && move_action_ref != null)
        {
            // Absolute fallback (not per-player safe) — only used if explicitly allowed
            move_action = move_action_ref.action;
        }

        if (move_action != null)
        {
            move_action.performed += OnMovePerformed;
            move_action.canceled += OnMoveCanceled;

            if (!move_action.enabled)
            {
                if (ping_pong_target != null && player_input != null)
                {
                    ping_pong_target.SetOwner(player_input);
                }
                move_action.Enable();
            }
        }

        // --- SERVE ---
        if (action_asset != null)
        {
            if (serve_action_ref != null && serve_action_ref.action != null)
            {
                serve_action = action_asset.FindAction(serve_action_ref.action.id);
            }

            if (serve_action == null && !string.IsNullOrEmpty(serve_action_name))
            {
                serve_action = action_asset.FindAction(serve_action_name, throwIfNotFound: false);
            }
        }
        // no else-fallback here: we prefer per-player device polling until PlayerInput exists

        if (serve_action != null && !serve_action.enabled)
        {
            serve_action.Enable();
        }
    }

    /*
    Remove callbacks and clear cached state.
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

        serve_action = null;
        serve_prev_pressed = false;
    }

    /*
    Cache latest move vector.
    */
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        move_input = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        move_input = Vector2.zero;
    }

    /*
    Late-bind PlayerInput if the spawner adds it after Awake/OnEnable.
    Poll Serve, then apply movement in FixedUpdate.
    */
    void Update()
    {
        // Late-bind PlayerInput & actions
        if (late_bind_player_input && player_input == null && Time.unscaledTime >= next_bind_check_time)
        {
            player_input = GetComponent<PlayerInput>() ?? GetComponentInParent<PlayerInput>();
            next_bind_check_time = Time.unscaledTime + late_bind_interval;

            if (player_input != null)
            {
                player_input.onControlsChanged += OnControlsChanged;
                // resolve actions now that we have the clone
                BindInput();
            }
        }

        // If PlayerInput exists but Serve action is still null, try resolving once more
        if (serve_action == null && player_input != null && player_input.actions != null)
        {
            if (serve_action_ref != null && serve_action_ref.action != null)
            {
                serve_action = player_input.actions.FindAction(serve_action_ref.action.id);
            }
            if (serve_action == null && !string.IsNullOrEmpty(serve_action_name))
            {
                serve_action = player_input.actions.FindAction(serve_action_name, throwIfNotFound: false);
            }
            if (serve_action != null && !serve_action.enabled) serve_action.Enable();
        }

        // Poll serve (action first; else per-player devices)
        PollServe();
    }

    private void PollServe()
    {
        // Gate: only one fire per rendered frame, even if we poll in both Update & Fixed
        int frame = Time.frameCount;

        bool pressedNow = false;

        // Prefer action if available (works with cloned asset)
        if (serve_action != null)
        {
            // IsPressed() is stable across update modes; we do our own edge detection
            pressedNow = serve_action.IsPressed();
        }
        else if (serve_device_fallback && player_input != null)
        {
            // Per-player devices only (safe for splitscreen/multiplayer)
            foreach (var device in player_input.devices)
            {
                if (device is Keyboard kb && kb.spaceKey.wasPressedThisFrame) { pressedNow = true; break; }
                if (device is Gamepad gp && gp.buttonSouth.wasPressedThisFrame) { pressedNow = true; break; } // A / Cross
            }
        }

        bool risingEdge = pressedNow && !serve_prev_pressed;

        if (risingEdge && _lastServeFireFrame != frame)
        {
            var owner = player_owner;
            if (owner != null)
            {
                if (_debugServe) Debug.Log($"[Serve] Request by {owner.player_id} (frame {frame})");
                PingPongLoop.RequestServe(owner.player_id);
                _lastServeFireFrame = frame;
            }
            else if (_debugServe)
            {
                Debug.LogWarning("[Serve] No PlayerOwner found in parents.");
            }
        }

        serve_prev_pressed = pressedNow;
    }


    /*
    Physics: acceleration/deceleration + cap.
    */
    void FixedUpdate()
    {
        Vector2 input_vector = move_input;
        if (input_vector.sqrMagnitude > 1f) input_vector = input_vector.normalized;

        float dt = Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
        Vector2 current_velocity = rigidbody2d.linearVelocity;
#else
        Vector2 current_velocity = rigidbody2d.velocity;
#endif

        if (invert_controls)
        {
            ApplyAccelerationFromInput(-input_vector, dt, current_velocity);
        }
        else
        {
            ApplyAccelerationFromInput(input_vector, dt, current_velocity);
        }

        // Decelerate when idle
        if (input_vector.sqrMagnitude <= 1e-6f)
        {
#if UNITY_6000_0_OR_NEWER
            Vector2 v = rigidbody2d.linearVelocity;
#else
            Vector2 v = rigidbody2d.velocity;
#endif
            float speed = v.magnitude;
            if (speed > 0f)
            {
                float new_speed = Mathf.Max(0f, speed - deceleration * dt);
                if (!Mathf.Approximately(new_speed, speed))
                {
                    Vector2 n = (speed < 1e-6f) ? (v / 1e-6f) : (v / speed);
                    Vector2 delta_v = (new_speed - speed) * n;
                    rigidbody2d.AddForce(rigidbody2d.mass * delta_v, ForceMode2D.Impulse);
                }
            }
        }

        // Cap max speed
#if UNITY_6000_0_OR_NEWER
        Vector2 capped = rigidbody2d.linearVelocity;
#else
        Vector2 capped = rigidbody2d.velocity;
#endif
        float mag = capped.magnitude;
        if (mag > max_speed)
        {
            float denom = (mag < 1e-6f) ? 1e-6f : mag;
            capped *= (max_speed / denom);
#if UNITY_6000_0_OR_NEWER
            rigidbody2d.linearVelocity = capped;
#else
            rigidbody2d.velocity = capped;
#endif
        }
        PollServe();
    }

    private void ApplyAccelerationFromInput(Vector2 input_vector, float dt, Vector2 current_velocity)
    {
        if (input_vector.sqrMagnitude <= 1e-6f) return;

        Vector2 target = input_vector * max_speed;
        Vector2 delta_v = target - current_velocity;
        float max_step = acceleration * dt;
        float delta_mag = delta_v.magnitude;

        if (delta_mag > max_step)
        {
            float denom = (delta_mag < 1e-6f) ? 1e-6f : delta_mag;
            delta_v *= (max_step / denom);
        }

        rigidbody2d.AddForce(rigidbody2d.mass * delta_v, ForceMode2D.Impulse);
    }
}
