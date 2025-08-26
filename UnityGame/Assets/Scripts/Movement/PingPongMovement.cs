using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PingPongMovement : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 12f;
    public float acceleration = 60f;
    public float deceleration = 30f;

    [Header("Bounds (local space)")]
    [Tooltip("Squared radius in LOCAL space (e.g., 3.4 means radius = sqrt(3.4)).")]
    public float clampRadiusSq = 3.4f;
    [Tooltip("Minimum LOCAL Y value allowed.")]
    public float minLocalY = 0.4f;

    private Rigidbody2D rb;

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

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed)    input.y += 1f;
            if (kb.downArrowKey.isPressed)  input.y -= 1f;
            if (kb.leftArrowKey.isPressed)  input.x -= 1f;
            if (kb.rightArrowKey.isPressed) input.x += 1f;
        }
        if (input.sqrMagnitude > 1f) input.Normalize();

        float dt = Time.fixedDeltaTime;

#if UNITY_6000_0_OR_NEWER
        Vector2 v = rb.linearVelocity;
#else
        Vector2 v = rb.velocity;
#endif

        if (input.sqrMagnitude > 1e-6f)
        {
            Vector2 target = input * maxSpeed;
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

#if UNITY_6000_0_OR_NEWER
        Vector2 capped = rb.linearVelocity;
#else
        Vector2 capped = rb.velocity;
#endif
        float s = capped.magnitude;
        if (s > maxSpeed) capped *= maxSpeed / s;

        Transform origin = transform.parent;
        Vector2 pWorld = rb.position;
        Vector2 pLocal = origin ? (Vector2)origin.InverseTransformPoint(pWorld) : pWorld;

        bool changed = false;
        Vector2 clampedLocal = pLocal;

        float r2 = Mathf.Max(0f, clampRadiusSq);
        float r = r2 > 0f ? Mathf.Sqrt(r2) : 0f;

        if (clampedLocal.y < minLocalY)
        {
            clampedLocal.y = minLocalY;
            changed = true;
        }

        if (r2 > 0f)
        {
            float d2 = clampedLocal.sqrMagnitude;
            if (d2 > r2)
            {
                if (clampedLocal.y <= minLocalY + 1e-6f)
                {
                    float xMax = Mathf.Sqrt(Mathf.Max(0f, r2 - minLocalY * minLocalY));
                    clampedLocal.x = Mathf.Clamp(clampedLocal.x, -xMax, xMax);
                    clampedLocal.y = minLocalY;
                }
                else
                {
                    clampedLocal = clampedLocal.normalized * r;
                    if (clampedLocal.y < minLocalY)
                    {
                        float xMax = Mathf.Sqrt(Mathf.Max(0f, r2 - minLocalY * minLocalY));
                        clampedLocal.x = Mathf.Clamp(clampedLocal.x, -xMax, xMax);
                        clampedLocal.y = minLocalY;
                    }
                }
                changed = true;
            }
        }

        if (changed)
        {
            Vector2 clampedWorld = origin ? (Vector2)origin.TransformPoint(clampedLocal) : clampedLocal;
            rb.position = clampedWorld;

            Vector2 vWorld = capped;
            Vector2 vLocal = origin ? (Vector2)origin.InverseTransformVector(vWorld) : vWorld;

            bool onCircle = r2 > 0f && Mathf.Abs(clampedLocal.sqrMagnitude - r2) <= 1e-4f;
            if (onCircle && clampedLocal.sqrMagnitude > 1e-12f)
            {
                Vector2 nLocal = clampedLocal.normalized;
                float radial = Vector2.Dot(vLocal, nLocal);
                if (radial > 0f) vLocal -= radial * nLocal; 
            }

            if (clampedLocal.y <= minLocalY + 1e-6f && vLocal.y < 0f)
                vLocal.y = 0f;

            capped = origin ? (Vector2)origin.TransformVector(vLocal) : vLocal;
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = capped;
#else
        rb.velocity = capped;
#endif
    }
}
