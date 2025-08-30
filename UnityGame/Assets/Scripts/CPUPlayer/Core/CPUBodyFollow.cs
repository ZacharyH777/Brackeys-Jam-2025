using UnityEngine;

/*
* Move the CPU body root to trail the ball with a small offset.
* Keeps space so the hand can reach without over-chasing.
* @param ball The ball reference to follow
* @param center_line The table split line
* @param player_owner Used to know side
*/
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CPUBodyFollow : MonoBehaviour
{
    [Header("Follow")]
    [Tooltip("Ball to follow")]
    public BallPhysics2D ball;

    [Tooltip("Center line of table")]
    public Transform center_line;

    [Tooltip("Use Y to split sides")]
    public bool split_by_y = true;

    [Tooltip("Seconds to lead the ball")]
    public float lead_time = 0.25f;

    [Tooltip("World offset to keep reach space")]
    public Vector2 follow_offset = new Vector2(0.0f, 0.75f);

    [Header("Motion")]
    [Tooltip("Maximum speed")]
    public float max_speed = 10f;

    [Tooltip("Acceleration")]
    public float acceleration = 50f;

    [Tooltip("Deceleration")]
    public float deceleration = 70f;

    [Tooltip("Stop distance to target")]
    public float target_threshold = 0.15f;

    [Tooltip("Target smoothing 0 to 1")]
    public float smoothing = 0.8f;

    private Rigidbody2D rigidbody2d;
    private PlayerOwner player_owner;
    private Vector2 smoothed_target;
    private bool has_target;

    /*
    * Cache references and seed target.
    * @param none
    */
    void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        player_owner = GetComponent<PlayerOwner>();

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

        smoothed_target = transform.position;
    }

    /*
    * Drive body movement toward a predicted ball position with offset.
    * @param none
    */
    void FixedUpdate()
    {
        if (ball == null)
        {
            ApplyDeceleration();
            return;
        }

        Vector2 target = ComputeAnchorTarget();
        smoothed_target = Vector2.Lerp(smoothed_target, target, smoothing * Time.fixedDeltaTime * 5f);
        has_target = true;

        Vector2 pos = transform.position;
        Vector2 delta = smoothed_target - pos;
        float dist = delta.magnitude;

        if (dist <= target_threshold)
        {
            ApplyDeceleration();
            return;
        }

        Vector2 desired_velocity = delta.normalized * max_speed;
        ApplyAccelerationToward(desired_velocity);
        CapMaxSpeed();
    }

    /*
    * Compute a body anchor that leads the ball and keeps an offset on our side.
    * @param none
    */
    private Vector2 ComputeAnchorTarget()
    {
        Vector2 ball_pos = ball.transform.position;
        Vector2 ball_vel = Vector2.zero;

        if (ball.TryGetComponent<Rigidbody2D>(out var rb2d))
        {
#if UNITY_6000_0_OR_NEWER
            ball_vel = rb2d.linearVelocity;
#else
            ball_vel = rb2d.velocity;
#endif
        }

        Vector2 lead = ball_pos + ball_vel * Mathf.Max(0f, lead_time);

        Vector2 axis_offset = Vector2.zero;
        if (center_line != null && player_owner != null)
        {
            Vector2 center = center_line.position;
            if (split_by_y == true)
            {
                if (player_owner.player_id == PlayerId.P1)
                {
                    axis_offset.y = -Mathf.Abs(follow_offset.y);
                }
                else
                {
                    axis_offset.y = Mathf.Abs(follow_offset.y);
                }
                axis_offset.x = follow_offset.x;
            }
            else
            {
                if (player_owner.player_id == PlayerId.P1)
                {
                    axis_offset.x = -Mathf.Abs(follow_offset.x);
                }
                else
                {
                    axis_offset.x = Mathf.Abs(follow_offset.x);
                }
                axis_offset.y = follow_offset.y;
            }
            return lead + axis_offset;
        }

        return lead + follow_offset;
    }

    /*
    * Accelerate toward a velocity with a capped change.
    * @param desired Desired velocity
    */
    private void ApplyAccelerationToward(Vector2 desired)
    {
        float dt = Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
        Vector2 current = rigidbody2d.linearVelocity;
#else
        Vector2 current = rigidbody2d.velocity;
#endif
        Vector2 dv = desired - current;
        float max_change = acceleration * dt;

        if (dv.magnitude > max_change)
        {
            dv = dv.normalized * max_change;
        }

        rigidbody2d.AddForce(rigidbody2d.mass * dv, ForceMode2D.Impulse);
    }

    /*
    * Apply symmetric deceleration until stop.
    * @param none
    */
    private void ApplyDeceleration()
    {
#if UNITY_6000_0_OR_NEWER
        Vector2 v = rigidbody2d.linearVelocity;
#else
        Vector2 v = rigidbody2d.velocity;
#endif
        float speed = v.magnitude;

        if (speed <= 0f)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        float new_speed = Mathf.Max(0f, speed - deceleration * dt);
        Vector2 new_v = v.normalized * new_speed;
        Vector2 dv = new_v - v;

        rigidbody2d.AddForce(rigidbody2d.mass * dv, ForceMode2D.Impulse);
    }

    /*
    * Clamp top speed.
    * @param none
    */
    private void CapMaxSpeed()
    {
#if UNITY_6000_0_OR_NEWER
        Vector2 v = rigidbody2d.linearVelocity;
#else
        Vector2 v = rigidbody2d.velocity;
#endif
        float speed = v.magnitude;

        if (speed > max_speed)
        {
            Vector2 capped = v.normalized * max_speed;
#if UNITY_6000_0_OR_NEWER
            rigidbody2d.linearVelocity = capped;
#else
            rigidbody2d.velocity = capped;
#endif
        }
    }

    /*
    * Set follow offset at runtime.
    * @param offset New world offset
    */
    public void SetFollowOffset(Vector2 offset)
    {
        follow_offset = offset;
    }
}
