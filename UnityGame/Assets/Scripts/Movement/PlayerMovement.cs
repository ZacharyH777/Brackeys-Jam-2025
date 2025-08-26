using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public float maxSpeed = 12f;
    public float acceleration = 60f;
    public float deceleration = 80f;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.angularDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        Vector2 in2 = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed) in2.y += 1f;
            if (kb.sKey.isPressed) in2.y -= 1f;
            if (kb.aKey.isPressed) in2.x -= 1f;
            if (kb.dKey.isPressed) in2.x += 1f;
        }
        if (in2.sqrMagnitude > 1f) in2.Normalize();

        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;

        if (in2.sqrMagnitude > 1e-6f)
        {
            Vector2 target = in2.normalized * maxSpeed;
            Vector2 deltaV = target - v;
            float maxStep = acceleration * dt;
            if (deltaV.magnitude > maxStep) deltaV = deltaV.normalized * maxStep;

            // In 2D, use impulse = mass * deltaV
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

        // Hard cap
        var vp = rb.linearVelocity;
        if (vp.magnitude > maxSpeed) rb.linearVelocity = vp.normalized * maxSpeed;
    }
}
