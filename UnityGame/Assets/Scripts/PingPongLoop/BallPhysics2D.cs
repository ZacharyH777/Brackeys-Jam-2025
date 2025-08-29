using System;
using UnityEngine;

/*
* 2D ball with fake Z height, table bounce, floor end, paddle impulse, and spin drag.
* Ball must have Rigidbody2D and a trigger CircleCollider2D.
*/
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("PingPong/Ball Physics 2D")]
public sealed class BallPhysics2D : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Optional sprite to offset with height")]
    public Transform visual;

    [Header("Physics")]
    [Tooltip("Base air drag")]
    public float air_drag = 0.6f;
    [Tooltip("Magnus scale from spin")]
    public float spin_magnus = 0.8f;

    [Header("Z Motion")]
    [Tooltip("Gravity along Z")]
    public float z_gravity = -30f;
    [Tooltip("Bounce keep ratio")]
    public float table_restitution = 0.35f;
    [Tooltip("Table extra boost")]
    public float table_z_boost = 2.0f;

    [Header("Impulses")]
    [Tooltip("Paddle impulse scale")]
    public float paddle_impulse = 1.3f;
    [Tooltip("Hit extra Z boost")]
    public float hit_z_boost = 1.4f;
    [Tooltip("Hit extra Y boost")]
    public float hit_y_boost = 1.0f;

    [Header("Spin")]
    [Tooltip("Spin change scale")]
    public float spin_on_hit = 0.9f;
    [Tooltip("Spin decay rate")]
    public float spin_decay = 1.5f;

    [Header("Zones")]
    [Tooltip("Floor KO height")]
    public float floor_end_height = -10f;

    [Header("Hits")]
    [Tooltip("Debounce time between any hits")]
    public float hit_cooldown_seconds = 0.06f;

    [Header("Debug")]
    [Tooltip("Enable logs")]
    public bool debug_logging;

    public event Action on_round_end;

    private Rigidbody2D rigidbody2d;

    private bool inside_table;
    private bool inside_floor;

    private float z_height;
    private float z_velocity;
    private float spin_scalar;

    private PaddleKinematics last_hitter;
    private float last_hit_time;

    /*
    Prepare rigidbody and defaults.
    */
    void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        if (rigidbody2d == null)
        {
            Debug.LogWarning("Rigidbody2D is missing");
        }

        z_height = 0f;
        z_velocity = 0f;
        spin_scalar = 0f;
        last_hitter = null;
        last_hit_time = -999f;

        if (rigidbody2d != null)
        {
            rigidbody2d.gravityScale = 0f;
            rigidbody2d.linearDamping = 0f;
            rigidbody2d.angularDamping = 0f;
            rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    /*
    Clear state on enable so a new rally or scene reload does not keep locks.
    */
    void OnEnable()
    {
        inside_table = false;
        inside_floor = false;
        last_hitter = null;
        last_hit_time = -999f;
    }

    /*
    Integrate fake Z, apply air and spin forces, and handle table or floor rules.
    */
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        z_velocity += z_gravity * dt;
        z_height += z_velocity * dt;

        ApplyAirAndSpin(dt);
        HandleTableBounce();
        HandleFloorEnd();

        if (visual != null)
        {
            Vector3 p = visual.localPosition;
            p.z = z_height;
            visual.localPosition = p;
        }
    }

    /*
    Continuous zone detection keeps flags true even if spawned inside.
    @param other Trigger peer collider
    */
    void OnTriggerStay2D(Collider2D other)
    {
        SurfaceZone zone = other.GetComponent<SurfaceZone>();
        if (zone == null)
        {
            return;
        }

        if (zone.zone_type == SurfaceZone.ZoneType.Table)
        {
            inside_table = true;
        }
        else if (zone.zone_type == SurfaceZone.ZoneType.Floor)
        {
            inside_floor = true;
        }
        else if (zone.zone_type == SurfaceZone.ZoneType.Paddle)
        {
            TryApplyPaddleHit(other);
        }
    }

    /*
    Clear flags when leaving zones.
    @param other Trigger peer collider
    */
    void OnTriggerExit2D(Collider2D other)
    {
        SurfaceZone zone = other.GetComponent<SurfaceZone>();
        if (zone == null)
        {
            return;
        }

        if (zone.zone_type == SurfaceZone.ZoneType.Table)
        {
            inside_table = false;
        }
        else if (zone.zone_type == SurfaceZone.ZoneType.Floor)
        {
            inside_floor = false;
        }
    }

    /*
    Public setter to inject Z directly if needed.
    @param height New height
    @param velocity New Z velocity
    */
    public void SetZ(float height, float velocity)
    {
        z_height = height;
        z_velocity = velocity;
    }

    /*
    Get current Z height.
    @return Z height
    */
    public float GetZ()
    {
        return z_height;
    }

    /*
    Manually clear the paddle lock.
    */
    public void ClearHitLock()
    {
        last_hitter = null;
        last_hit_time = -999f;
    }

    /*
    Apply air drag and a simple Magnus force perpendicular to velocity.
    @param dt Fixed delta time
    */
    private void ApplyAirAndSpin(float dt)
    {
        if (rigidbody2d == null)
        {
            return;
        }

        Vector2 v = rigidbody2d.linearVelocity;

        if (air_drag > 0f)
        {
            Vector2 drag = -air_drag * v;
            v += drag * dt;
        }

        if (Mathf.Abs(spin_scalar) > 0.0001f)
        {
            Vector2 perp = new Vector2(-v.y, v.x);
            Vector2 magnus = spin_magnus * spin_scalar * perp;
            v += magnus * dt;

            float decay = Mathf.Max(0f, 1f - spin_decay * dt);
            spin_scalar *= decay;
        }

        rigidbody2d.linearVelocity = v;
    }

    /*
    Bounce off the table when inside and at or below zero height.
    Keeps some energy and adds a little boost.
    */
    private void HandleTableBounce()
    {
        if (!inside_table)
        {
            return;
        }

        if (z_height <= 0f)
        {
            z_height = 0f;
            float keep = Mathf.Abs(z_velocity) * table_restitution;
            z_velocity = keep + table_z_boost;

            if (debug_logging)
            {
                Debug.LogWarning("Table bounce");
            }
        }
    }

    /*
    End round if inside floor and below KO height.
    */
    private void HandleFloorEnd()
    {
        if (!inside_floor)
        {
            return;
        }

        if (z_height <= floor_end_height)
        {
            if (debug_logging)
            {
                Debug.LogWarning("Round ended");
            }

            Action cb = on_round_end;
            if (cb != null)
            {
                cb();
            }

            enabled = false;
        }
    }

    /*
    Apply paddle hit impulse and spin based on relative motion.
    Enforces rule that the same paddle cannot hit twice in a row.
    Also debounces multiple hits while overlapping.
    @param other Collider from the paddle
    */
    private void TryApplyPaddleHit(Collider2D other)
    {
        if (rigidbody2d == null)
        {
            return;
        }

        PaddleKinematics kin = other.GetComponentInParent<PaddleKinematics>();
        if (kin == null)
        {
            return;
        }

        float now = Time.time;

        /* Debounce */
        if (now - last_hit_time < hit_cooldown_seconds)
        {
            return;
        }

        /* Lockout */
        if (last_hitter != null)
        {
            if (kin == last_hitter)
            {
                if (debug_logging)
                {
                    Debug.LogWarning("Ignored same paddle hit");
                }
                return;
            }
        }

        Vector2 paddle_v = kin.current_velocity;
        Vector2 ball_v = rigidbody2d.linearVelocity;

        /* Base impulse from relative motion */
        Vector2 rel = paddle_v - ball_v;
        Vector2 impulse = paddle_impulse * rel;
        ball_v += impulse;

        /* Y boost: set Y to sign(paddle_y) * (hit_y_boost + |paddle_y|) */
        float y_sign = 0f;
        if (paddle_v.y > 0f)
        {
            y_sign = 1f;
        }
        else if (paddle_v.y < 0f)
        {
            y_sign = -1f;
        }
        else
        {
            /* Fallback sign if paddle is perfectly vertical-still */
            Vector2 paddle_to_ball = (Vector2)transform.position - (Vector2)other.transform.position;
            if (paddle_to_ball.y > 0f)
            {
                y_sign = 1f;
            }
            else if (paddle_to_ball.y < 0f)
            {
                y_sign = -1f;
            }
            else
            {
                y_sign = 1f;
            }
        }

        float target_y = (hit_y_boost + Mathf.Abs(paddle_v.y)) * y_sign;
        ball_v.y = target_y;

        /* Z pop + spin */
        z_velocity += hit_z_boost;

        Vector2 paddle_to_ball2 = (Vector2)transform.position - (Vector2)other.transform.position;
        float signed = paddle_v.x * paddle_to_ball2.y - paddle_v.y * paddle_to_ball2.x;
        float spin_add = spin_on_hit * Mathf.Sign(signed) * Mathf.Min(paddle_v.magnitude, 20f);
        spin_scalar += spin_add;

        last_hitter = kin;
        last_hit_time = now;

        rigidbody2d.linearVelocity = ball_v;

        if (debug_logging)
        {
            Debug.LogWarning("Paddle hit");
        }
    }
}