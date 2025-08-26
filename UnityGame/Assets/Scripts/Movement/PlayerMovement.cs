using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 12f;
    public float acceleration = 60f;
    public float deceleration = 80f;

    [Header("Input (Unity 6.2 Input System)")]
    [Tooltip("Optional: drag a Value/Vector2 action here (e.g., 'Move').")]
    public InputActionReference moveAction;

    [Tooltip("If no InputActionReference is set, we'll look for this action on a PlayerInput on the same GameObject.")]
    public string moveActionName = "Move";

    [Tooltip("Try to auto-bind from PlayerInput if Move Action is null.")]
    public bool fetchFromPlayerInputIfNull = true;

    private Rigidbody2D rb;
    private InputAction _moveAction;
    private Vector2 _move;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.angularDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void OnEnable()
    {
        BindInput();
    }

    void OnDisable()
    {
        UnbindInput();
    }

    private void BindInput()
    {
        if (moveAction != null && moveAction.action != null)
        {
            _moveAction = moveAction.action;
        }

        else if (fetchFromPlayerInputIfNull)
        {
            var pi = GetComponent<PlayerInput>();
            if (pi != null && !string.IsNullOrEmpty(moveActionName))
            {
                try
                {
                    _moveAction = pi.actions?.FindAction(moveActionName, throwIfNotFound: true);
                }
                catch { _moveAction = null; }
            }
        }

        if (_moveAction != null)
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled  += OnMoveCanceled;
            if (!_moveAction.enabled) _moveAction.Enable();
        }
        else
        {
            Debug.LogWarning($"[{nameof(PlayerMovement2D)}] No Move action bound. Assign an InputActionReference or add PlayerInput with an action named '{moveActionName}'.");
        }
    }

    private void UnbindInput()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled  -= OnMoveCanceled;
            _moveAction = null;
        }
        _move = Vector2.zero;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx) => _move = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx)  => _move = Vector2.zero;

    void FixedUpdate()
    {
        Vector2 in2 = _move;
        if (in2.sqrMagnitude > 1f) in2.Normalize();

        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;

        if (in2.sqrMagnitude > 1e-6f)
        {
            Vector2 target = in2 * maxSpeed;
            Vector2 deltaV = target - v;

            float maxStep = acceleration * dt;
            float mag = deltaV.magnitude;
            if (mag > maxStep) deltaV *= maxStep / Mathf.Max(mag, 1e-6f);

            rb.AddForce(rb.mass * deltaV, ForceMode2D.Impulse);
        }
        else
        {
            float speed = v.magnitude;
            if (speed > 0f)
            {
                float drop = deceleration * dt;
                float newSpeed = Mathf.Max(speed - drop, 0f);
                if (!Mathf.Approximately(newSpeed, speed))
                {
                    Vector2 deltaV = (newSpeed - speed) * (v / Mathf.Max(speed, 1e-6f));
                    rb.AddForce(rb.mass * deltaV, ForceMode2D.Impulse);
                }
            }
        }

        var vp = rb.linearVelocity;
        if (vp.magnitude > maxSpeed) rb.linearVelocity = vp.normalized * maxSpeed;
    }
}
