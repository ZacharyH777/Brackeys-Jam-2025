using System;
using UnityEngine;

/*
* 2D ball with fake Z height, table bounce, floor end, paddle hit shaping, and spin drag.
* Ball requires Rigidbody2D and a trigger CircleCollider2D.
* Paddles use non trigger colliders and a PaddleKinematics on a parent.
*/
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("PingPong/Ball Physics 2D")]
public sealed class BallPhysics2D : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Optional sprite offset by height")]
    public Transform visual;

    [Header("Physics")]
    [Tooltip("Base air drag")]
    public float air_drag = 0.6f;
    [Tooltip("Magnus scale from spin")]
    public float spin_magnus = 0.8f;

    [Header("Z Motion")]
    [Tooltip("Gravity along z")]
    public float z_gravity = -30f;
    [Tooltip("Bounce keep ratio")]
    public float table_restitution = 0.35f;
    [Tooltip("Table extra boost")]
    public float table_z_boost = 2.0f;

    [Header("Impulses")]
    [Tooltip("Overall impulse scale")]
    public float paddle_impulse = 1.3f;
    [Tooltip("Extra z pop on hit")]
    public float hit_z_boost = 1.4f;
    [Tooltip("Baseline y pop")]
    public float hit_y_boost = 1.0f;

    [Header("Shaping")]
    [Tooltip("Zero uses paddle speed one uses relative")]
    public float relative_impulse_weight = 0.35f;
    [Tooltip("Cap paddle speed x")]
    public float influence_max_speed_x = 6f;
    [Tooltip("Cap paddle speed y")]
    public float influence_max_speed_y = 6f;
    [Tooltip("Impulse scale x")]
    public float impulse_x_multiplier = 1.0f;
    [Tooltip("Impulse scale y")]
    public float impulse_y_multiplier = 1.0f;
    [Tooltip("Post scale x delta")]
    public float post_velocity_multiplier_x = 0.25f;
    [Tooltip("Post scale y delta")]
    public float post_velocity_multiplier_y = 0.25f;

    [Header("Clamps")]
    [Tooltip("Clamp abs vx")]
    public float max_ball_speed_x = 9f;
    [Tooltip("Clamp abs vy")]
    public float max_ball_speed_y = 9f;

    [Header("Y Boost")]
    [Tooltip("Extra y boost scale")]
    public float y_boost_multiplier = 1.0f;
    [Tooltip("Clamp abs y boost")]
    public float max_y_boost = 6f;
    [Tooltip("If true boost sets vy")]
    public bool y_boost_sets_y = false;

    [Header("Spin")]
    [Tooltip("Spin added per hit")]
    public float spin_on_hit = 0.9f;
    [Tooltip("Spin decay per second")]
    public float spin_decay = 1.5f;

    [Header("Zones")]
    [Tooltip("Floor ko height")]
    public float floor_end_height = -10f;
    [Tooltip("Unlock on table bounce")]
    public bool unlock_on_table_bounce = false;

    [Header("Hits")]
    [Tooltip("Debounce seconds")]
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

    [Header("Serve State")]
    [Tooltip("When true, physics are suspended (table/floor ignored) until serve toss")]
    public bool serving_suspended = false;

    public event System.Action<PaddleKinematics, Collider2D> on_paddle_hit;
    public event System.Action<Vector2> on_table_bounce;

    public void SetServeSuspended(bool suspended)
    {
        serving_suspended = suspended;
        if (rigidbody2d != null)
        {
#if UNITY_6000_0_OR_NEWER
            rigidbody2d.linearVelocity = Vector2.zero;
#else
            rigidbody2d.velocity = Vector2.zero;
#endif
            rigidbody2d.angularVelocity = 0f;
            rigidbody2d.bodyType = suspended ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }
    }

    public PaddleKinematics GetLastHitter()
    {
        return last_hitter;
    }

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
    Clear transient state on enable.
    */
    void OnEnable()
    {
        inside_table = false;
        inside_floor = false;
        last_hitter = null;
        last_hit_time = -999f;
    }

    /*
    Integrate fake z, apply air and spin, handle zones, update visual.
    */
    void FixedUpdate()
    {

        if (serving_suspended)
        {
            // keep visual height consistent, but do no physics
            if (visual != null)
            {
                Vector3 p = visual.localPosition;
                p.z = z_height;
                visual.localPosition = p;
            }
            return;
        }

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
    Track zones and process paddle hits.
    @param other Trigger peer collider
    */
    void OnTriggerStay2D(Collider2D other)
    {

        if (serving_suspended)
        {
            // Ignore all zones while suspended (prevents pre-serve bounces/hits)
            return;
        }
        
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
    Clear zone flags.
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
    Set z state.
    @param height New z height
    @param velocity New z velocity
    */
    public void SetZ(float height, float velocity)
    {
        z_height = height;
        z_velocity = velocity;
    }

    /*
    Get z height.
    @return Current z height
    */
    public float GetZ()
    {
        return z_height;
    }

    /*
    Clear the paddle lock.
    */
    public void ClearHitLock()
    {
        last_hitter = null;
        last_hit_time = -999f;
    }

    /*
    Apply air drag and Magnus force.
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

            float decay = 1f - spin_decay * dt;
            if (decay < 0f)
            {
                decay = 0f;
            }
            spin_scalar *= decay;
        }

        rigidbody2d.linearVelocity = v;
    }

    /*
    Bounce off table when inside and at or below zero height.
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

            Action<Vector2> cb_bounce = on_table_bounce;
            if (cb_bounce != null)
            {
                cb_bounce(transform.position);
            }

            float keep = Mathf.Abs(z_velocity) * table_restitution;
            z_velocity = keep + table_z_boost;

            if (unlock_on_table_bounce)
            {
                ClearHitLock();
            }

            if (debug_logging)
            {
                Debug.LogWarning("Table bounce");
            }
        }
    }

    /*
    End round if inside floor and below ko height.
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
    Apply paddle hit shaping, boost, spin, and clamps.
    Enforces one hit per side and adds debounce.
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

        if (now - last_hit_time < hit_cooldown_seconds)
        {
            return;
        }

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

        Vector2 paddle_v_used = paddle_v;
        if (paddle_v_used.x > influence_max_speed_x)
        {
            paddle_v_used.x = influence_max_speed_x;
        }
        if (paddle_v_used.x < -influence_max_speed_x)
        {
            paddle_v_used.x = -influence_max_speed_x;
        }
        if (paddle_v_used.y > influence_max_speed_y)
        {
            paddle_v_used.y = influence_max_speed_y;
        }
        if (paddle_v_used.y < -influence_max_speed_y)
        {
            paddle_v_used.y = -influence_max_speed_y;
        }

        Vector2 v0 = rigidbody2d.linearVelocity;

        float w = Mathf.Clamp01(relative_impulse_weight);
        Vector2 rel_full = paddle_v_used - v0;
        Vector2 rel_blended;
        rel_blended.x = Mathf.Lerp(paddle_v_used.x, rel_full.x, w);
        rel_blended.y = Mathf.Lerp(paddle_v_used.y, rel_full.y, w);

        Vector2 impulse = paddle_impulse * rel_blended;
        impulse.x *= impulse_x_multiplier;
        impulse.y *= impulse_y_multiplier;

        Vector2 v_after = v0 + impulse;

        Vector2 delta = v_after - v0;
        delta.x *= post_velocity_multiplier_x;
        delta.y *= post_velocity_multiplier_y;

        Vector2 v = v0 + delta;

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

        float y_mag = (hit_y_boost + Mathf.Abs(paddle_v.y)) * y_boost_multiplier;
        if (y_mag > max_y_boost)
        {
            y_mag = max_y_boost;
        }
        if (y_mag < -max_y_boost)
        {
            y_mag = -max_y_boost;
        }
        float y_add = y_sign * y_mag;

        if (y_boost_sets_y)
        {
            v.y = y_add;
        }
        else
        {
            v.y += y_add;
        }

        z_velocity += hit_z_boost;

        Vector2 paddle_to_ball2 = (Vector2)transform.position - (Vector2)other.transform.position;
        float signed = paddle_v.x * paddle_to_ball2.y - paddle_v.y * paddle_to_ball2.x;
        float spin_add = spin_on_hit * Mathf.Sign(signed) * Mathf.Min(paddle_v.magnitude, 20f);
        spin_scalar += spin_add;

        if (v.x > max_ball_speed_x)
        {
            v.x = max_ball_speed_x;
        }
        if (v.x < -max_ball_speed_x)
        {
            v.x = -max_ball_speed_x;
        }
        if (v.y > max_ball_speed_y)
        {
            v.y = max_ball_speed_y;
        }
        if (v.y < -max_ball_speed_y)
        {
            v.y = -max_ball_speed_y;
        }

        last_hitter = kin;
        last_hit_time = now;

        Action<PaddleKinematics, Collider2D> cb_hit = on_paddle_hit;
        if (cb_hit != null)
        {
            cb_hit(kin, other);
        }

        rigidbody2d.linearVelocity = v;

        if (debug_logging)
        {
            Debug.LogWarning("Paddle hit");
        }
    }
}
