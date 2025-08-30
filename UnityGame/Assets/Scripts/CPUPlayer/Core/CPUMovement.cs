using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CPUMovement : MonoBehaviour
{
    public enum ControlMode { Body, Target }

    [Header("Mode")]
    [Tooltip("Detect mode from name")]
    public bool auto_detect_mode = true;
    [Tooltip("Control mode to use")]
    public ControlMode control_mode = ControlMode.Body;

    [Header("Refs")]
    [Tooltip("Ball to track")]
    public BallPhysics2D ball;
    [Tooltip("Center line of table")]
    public Transform center_line;
    [Tooltip("Aim anchor on table")]
    public Transform center_target;
    [Tooltip("Use Y to split sides")]
    public bool split_by_y = true;

    [Header("Movement")]
    [Tooltip("Maximum movement speed")]
    public float max_speed = 12f;
    [Tooltip("Acceleration toward target")]
    public float acceleration = 60f;
    [Tooltip("Deceleration when no target")]
    public float deceleration = 80f;
    [Tooltip("Stop distance to target")]
    public float target_threshold = 0.2f;
    [Tooltip("Target smoothing 0 to 1")]
    public float movement_smoothing = 0.8f;
    [Tooltip("Max single step distance")]
    public float max_movement_distance = 5f;

    [Header("Body Follow")]
    [Tooltip("Seconds to lead the ball")]
    public float lead_time = 0.20f;
    [Tooltip("World offset for reach")]
    public Vector2 follow_offset = new Vector2(0.0f, 0.75f);

    [Header("Intercept")]
    [Tooltip("Predict seconds ahead")]
    public float predict_time = 0.18f;
    [Tooltip("Reachable chase distance")]
    public float reachable_distance = 3.0f;
    [Tooltip("Position accuracy 0 to 1")]
    public float accuracy = 0.85f;
    [Tooltip("Random aim noise")]
    public float position_noise = 0.25f;

    [Header("Strike")]
    [Tooltip("Strike radius around hand")]
    public float strike_radius = 0.60f;
    [Tooltip("Min seconds between strikes")]
    public float strike_cooldown = 0.35f;
    [Tooltip("Min outgoing speed")]
    public float min_out_speed = 8.0f;
    [Tooltip("Max outgoing speed")]
    public float max_out_speed = 16.0f;
    [Tooltip("Aim jitter amount")]
    public float aim_jitter = 0.35f;

    [Header("Targets")]
    [Tooltip("Optional PingPongMovement for anim")]
    public PingPongMovement ping_pong_target;

    private Rigidbody2D rigidbody2d;
    private PlayerOwner player_owner;

    private Vector2 current_target_position;
    private Vector2 smoothed_target_position;
    private bool has_target;

    private Vector2 last_ball_position;
    private Vector2 last_ball_velocity;
    private float next_strike_time;

    /*
    * Cache components, detect mode, auto-find refs, seed state.
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

        current_target_position = transform.position;
        smoothed_target_position = current_target_position;

        if (auto_detect_mode == true)
        {
            DetectModeFromName();
        }

        AutoFindReferences();
    }

    /*
    * Find "ball", "net", and "ai_target" by name if missing.
    * Falls back to FindFirstObjectByType when needed.
    * @param none
    */
    private void AutoFindReferences()
    {
        if (ball == null)
        {
            var go_ball = GameObject.Find("ball");
            if (go_ball != null)
            {
                ball = go_ball.GetComponent<BallPhysics2D>();
                if (ball == null)
                {
                    ball = go_ball.GetComponentInChildren<BallPhysics2D>();
                }
            }
            if (ball == null)
            {
                var found_ball = FindFirstObjectByType<BallPhysics2D>(FindObjectsSortMode.InstanceID);
                if (found_ball != null)
                {
                    ball = found_ball;
                }
            }
            if (ball == null)
            {
                Debug.LogWarning("CPUMovement could not find 'ball'");
            }
        }

        if (center_line == null)
        {
            var go_net = GameObject.Find("net");
            if (go_net != null)
            {
                center_line = go_net.transform;
            }
            if (center_line == null)
            {
                var loop = FindFirstObjectByType<PingPongLoop>(FindObjectsSortMode.InstanceID);
                if (loop != null)
                {
                    center_line = loop.center_line;
                }
            }
            if (center_line == null)
            {
                Debug.LogWarning("CPUMovement could not find 'net' or PingPongLoop.center_line");
            }
        }

        if (center_target == null)
        {
            var go_target = GameObject.Find("ai_target");
            if (go_target != null)
            {
                center_target = go_target.transform;
            }
            if (center_target == null)
            {
                // center_target is optional; no warning needed
            }
        }

        if (player_owner == null)
        {
            player_owner = GetComponent<PlayerOwner>();
        }
    }

    void FixedUpdate()
    {
        UpdateBallTracking();

        if (control_mode == ControlMode.Target)
        {
            FixedUpdateTargetController();
        }
        else
        {
            FixedUpdateBodyController();
        }

        DriveMovement();
        UpdatePingPongTarget();
    }

    private void UpdateBallTracking()
    {
        if (ball == null)
        {
            return;
        }

        Vector2 pos = ball.transform.position;

        if (last_ball_position != Vector2.zero)
        {
            Vector2 delta = pos - last_ball_position;
            float dt = Time.fixedDeltaTime;
            if (dt > 0f)
            {
                last_ball_velocity = delta / dt;
            }
        }

        last_ball_position = pos;
    }

    private void FixedUpdateBodyController()
    {
        if (ball == null)
        {
            has_target = false;
            return;
        }

        Vector2 ball_pos = ball.transform.position;
        Vector2 ball_vel = last_ball_velocity;

        Vector2 lead = ball_pos + ball_vel * Mathf.Max(0f, lead_time);

        Vector2 axis_offset = follow_offset;

        if (center_line != null && player_owner != null)
        {
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
        }

        Vector2 target = lead + axis_offset;
        SetTargetPosition(target);
    }

    private void FixedUpdateTargetController()
    {
        if (ball == null)
        {
            has_target = false;
            return;
        }

        Vector2 ball_pos = ball.transform.position;
        Vector2 ball_vel = last_ball_velocity;

        Vector2 predicted = ball_pos + ball_vel * Mathf.Max(0.0f, predict_time);

        bool on_our_side = IsBallOnOurSide(predicted);
        bool coming_to_us = IsBallComingToOurSide(ball_pos, ball_vel);

        if (on_our_side == true || coming_to_us == true)
        {
            Vector2 intercept = ComputeIntercept(predicted);
            intercept = ApplyAccuracyAndNoise(intercept);
            SetTargetPosition(intercept);
        }
        else
        {
            Vector2 defend = GetDefensivePosition();
            SetTargetPosition(defend);
        }

        TryStrikeBall();
    }

    private void DriveMovement()
    {
        if (has_target == false)
        {
            ApplyDeceleration();
            return;
        }

        smoothed_target_position = Vector2.Lerp(smoothed_target_position, current_target_position, movement_smoothing * Time.fixedDeltaTime * 5f);

        Vector2 pos = transform.position;
        Vector2 to_target = smoothed_target_position - pos;
        float dist = to_target.magnitude;

        if (dist <= target_threshold)
        {
            ApplyDeceleration();
            return;
        }

        Vector2 desired_dir = Vector2.zero;
        if (to_target.sqrMagnitude > 0f)
        {
            desired_dir = to_target.normalized;
        }

        Vector2 desired_velocity = desired_dir * max_speed;
        ApplyAccelerationTowardVelocity(desired_velocity);
        CapMaxSpeed();
    }

    private Vector2 ComputeIntercept(Vector2 predicted_ball_pos)
    {
        Vector2 current_pos = transform.position;
        Vector2 toward = predicted_ball_pos - current_pos;

        float dist = toward.magnitude;
        if (dist > reachable_distance)
        {
            Vector2 dir = Vector2.zero;
            if (dist > 0f)
            {
                dir = toward / dist;
            }
            Vector2 clamped = current_pos + dir * reachable_distance;
            return clamped;
        }

        return predicted_ball_pos;
    }

    private Vector2 ApplyAccuracyAndNoise(Vector2 target_pos)
    {
        Vector2 current_pos = transform.position;
        Vector2 move = target_pos - current_pos;

        Vector2 scaled = move * Mathf.Clamp01(accuracy);

        Vector2 noise = Vector2.zero;
        float nx = Random.Range(-position_noise, position_noise);
        float ny = Random.Range(-position_noise, position_noise);
        noise.x = nx;
        noise.y = ny;

        Vector2 result = current_pos + scaled + noise;
        return result;
    }

    private Vector2 GetDefensivePosition()
    {
        if (center_line == null)
        {
            return transform.position;
        }

        Vector2 center = center_line.position;
        Vector2 off = Vector2.zero;

        if (player_owner != null)
        {
            if (split_by_y == true)
            {
                if (player_owner.player_id == PlayerId.P1)
                {
                    off.y = -1.2f;
                }
                else
                {
                    off.y = 1.2f;
                }
            }
            else
            {
                if (player_owner.player_id == PlayerId.P1)
                {
                    off.x = -1.2f;
                }
                else
                {
                    off.x = 1.2f;
                }
            }
        }

        Vector2 pos = center + off;
        return pos;
    }

    private void TryStrikeBall()
    {
        if (Time.time < next_strike_time)
        {
            return;
        }

        if (ball == null)
        {
            return;
        }

        Vector2 hand_pos = transform.position;
        Vector2 ball_pos = ball.transform.position;

        float d = Vector2.Distance(hand_pos, ball_pos);
        if (d > strike_radius)
        {
            return;
        }

        Vector2 aim = ChooseAimPointFarSide();
        Vector2 desired_v = ComputeDesiredBallVelocity(ball_pos, aim);

        Rigidbody2D ball_body = ball.GetComponent<Rigidbody2D>();
        if (ball_body == null)
        {
            Debug.LogWarning("Ball rigidbody was not found");
            return;
        }

#if UNITY_6000_0_OR_NEWER
        Vector2 current_v = ball_body.linearVelocity;
#else
        Vector2 current_v = ball_body.velocity;
#endif
        Vector2 dv = desired_v - current_v;

        Vector2 impulse = ball_body.mass * dv;
        ball_body.AddForce(impulse, ForceMode2D.Impulse);

        next_strike_time = Time.time + Mathf.Max(0.05f, strike_cooldown);
    }

    private Vector2 ChooseAimPointFarSide()
    {
        if (center_line == null)
        {
            Vector2 fallback = last_ball_position + new Vector2(0f, 2f);
            return fallback;
        }

        Vector2 anchor = center_line.position;

        if (center_target != null)
        {
            anchor = center_target.position;
        }

        Vector2 jitter = Vector2.zero;
        float jx = Random.Range(-aim_jitter, aim_jitter);
        float jy = Random.Range(-aim_jitter, aim_jitter);
        jitter.x = jx;
        jitter.y = jy;

        Vector2 aim = anchor + jitter;

        if (player_owner != null)
        {
            if (split_by_y == true)
            {
                if (player_owner.player_id == PlayerId.P1)
                {
                    if (aim.y <= center_line.position.y)
                    {
                        aim.y = center_line.position.y + Mathf.Abs(aim_jitter) + 0.5f;
                    }
                }
                else
                {
                    if (aim.y >= center_line.position.y)
                    {
                        aim.y = center_line.position.y - Mathf.Abs(aim_jitter) - 0.5f;
                    }
                }
            }
            else
            {
                if (player_owner.player_id == PlayerId.P1)
                {
                    if (aim.x <= center_line.position.x)
                    {
                        aim.x = center_line.position.x + Mathf.Abs(aim_jitter) + 0.5f;
                    }
                }
                else
                {
                    if (aim.x >= center_line.position.x)
                    {
                        aim.x = center_line.position.x - Mathf.Abs(aim_jitter) - 0.5f;
                    }
                }
            }
        }

        return aim;
    }

    private Vector2 ComputeDesiredBallVelocity(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        float t = Mathf.Max(0.10f, predict_time);
        float speed = distance / t;

        if (speed < min_out_speed)
        {
            speed = min_out_speed;
        }

        if (speed > max_out_speed)
        {
            speed = max_out_speed;
        }

        Vector2 dir = to - from;
        if (dir.sqrMagnitude > 0f)
        {
            dir = dir.normalized;
        }

        Vector2 v = dir * speed;
        return v;
    }

    private bool IsBallOnOurSide(Vector2 ball_pos)
    {
        if (center_line == null)
        {
            return true;
        }

        if (player_owner == null)
        {
            return true;
        }

        Vector2 c = center_line.position;

        if (split_by_y == true)
        {
            if (player_owner.player_id == PlayerId.P1)
            {
                return ball_pos.y < c.y;
            }
            else
            {
                return ball_pos.y > c.y;
            }
        }
        else
        {
            if (player_owner.player_id == PlayerId.P1)
            {
                return ball_pos.x < c.x;
            }
            else
            {
                return ball_pos.x > c.x;
            }
        }
    }

    private bool IsBallComingToOurSide(Vector2 ball_pos, Vector2 ball_vel)
    {
        if (center_line == null)
        {
            return false;
        }

        if (player_owner == null)
        {
            return false;
        }

        if (ball_vel.magnitude < 0.05f)
        {
            return false;
        }

        Vector2 c = center_line.position;

        if (split_by_y == true)
        {
            if (player_owner.player_id == PlayerId.P1)
            {
                if (ball_pos.y > c.y && ball_vel.y < 0f)
                {
                    return true;
                }
                return false;
            }
            else
            {
                if (ball_pos.y < c.y && ball_vel.y > 0f)
                {
                    return true;
                }
                return false;
            }
        }
        else
        {
            if (player_owner.player_id == PlayerId.P1)
            {
                if (ball_pos.x > c.x && ball_vel.x < 0f)
                {
                    return true;
                }
                return false;
            }
            else
            {
                if (ball_pos.x < c.x && ball_vel.x > 0f)
                {
                    return true;
                }
                return false;
            }
        }
    }

    public void SetTargetPosition(Vector2 target)
    {
        Vector2 current_pos = transform.position;
        Vector2 diff = target - current_pos;
        float d = diff.magnitude;

        if (d > max_movement_distance)
        {
            Vector2 dir = Vector2.zero;
            if (d > 0f)
            {
                dir = diff / d;
            }
            target = current_pos + dir * max_movement_distance;
        }

        current_target_position = target;
        has_target = true;
    }

    public void ClearTarget()
    {
        has_target = false;
    }

    private void ApplyAccelerationTowardVelocity(Vector2 desired_velocity)
    {
        float dt = Time.fixedDeltaTime;
#if UNITY_6000_0_OR_NEWER
        Vector2 current_velocity = rigidbody2d.linearVelocity;
#else
        Vector2 current_velocity = rigidbody2d.velocity;
#endif
        Vector2 dv = desired_velocity - current_velocity;
        float max_change = acceleration * dt;

        if (dv.magnitude > max_change)
        {
            dv = dv.normalized * max_change;
        }

        rigidbody2d.AddForce(rigidbody2d.mass * dv, ForceMode2D.Impulse);
    }

    private void ApplyDeceleration()
    {
        float dt = Time.fixedDeltaTime;
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

        float new_speed = Mathf.Max(0f, speed - deceleration * dt);
        Vector2 new_v = Vector2.zero;

        if (speed > 0f)
        {
            new_v = v.normalized * new_speed;
        }

        Vector2 dv = new_v - v;
        rigidbody2d.AddForce(rigidbody2d.mass * dv, ForceMode2D.Impulse);
    }

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
            Vector2 capped = Vector2.zero;
            if (speed > 0f)
            {
                capped = v.normalized * max_speed;
            }
#if UNITY_6000_0_OR_NEWER
            rigidbody2d.linearVelocity = capped;
#else
            rigidbody2d.velocity = capped;
#endif
        }
    }

    private void UpdatePingPongTarget()
    {
        if (ping_pong_target == null)
        {
            return;
        }

#if UNITY_6000_0_OR_NEWER
        Vector2 v = rigidbody2d.linearVelocity;
#else
        Vector2 v = rigidbody2d.velocity;
#endif
        if (v.magnitude <= 0.1f)
        {
            return;
        }

        Vector2 input_vec = v.normalized;
        float scale = Mathf.Clamp01(v.magnitude / max_speed);
        input_vec = input_vec * scale;
    }

    private void DetectModeFromName()
    {
        string n = gameObject.name.ToLower();
        bool looks_like_target = false;

        if (n.Contains("targetp1") == true)
        {
            looks_like_target = true;
        }

        if (n.Contains("targetp2") == true)
        {
            looks_like_target = true;
        }

        if (looks_like_target == true)
        {
            control_mode = ControlMode.Target;
        }
        else
        {
            control_mode = ControlMode.Body;
        }
    }

    public Vector2 CurrentTarget
    {
        get { return current_target_position; }
    }

    public bool HasTarget
    {
        get { return has_target; }
    }

#if UNITY_6000_0_OR_NEWER
    public Vector2 CurrentVelocity
    {
        get { return rigidbody2d.linearVelocity; }
    }
#else
    public Vector2 CurrentVelocity
    {
        get { return rigidbody2d.velocity; }
    }
#endif

    public void SetMovementParameters(float new_max_speed, float new_accel, float new_decel)
    {
        if (new_max_speed < 0f) new_max_speed = 0f;
        if (new_accel < 0f) new_accel = 0f;
        if (new_decel < 0f) new_decel = 0f;

        max_speed = new_max_speed;
        acceleration = new_accel;
        deceleration = new_decel;
    }

    public void SetMovementSmoothing(float smoothing)
    {
        movement_smoothing = Mathf.Clamp01(smoothing);
    }

    void OnDrawGizmosSelected()
    {
        if (has_target == false)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(current_target_position, 0.3f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(smoothed_target_position, 0.2f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, current_target_position);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(current_target_position, target_threshold);

        if (control_mode == ControlMode.Target)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, strike_radius);
        }
    }
}
