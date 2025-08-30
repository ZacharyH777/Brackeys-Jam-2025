using UnityEngine;

/*
* Plan and execute paddle strikes so the ball lands on the far side.
* Computes hand world position from the transform chain.
* Chooses an aim point and generates an impulse or swing.
* @param ball The ball to strike
* @param center_line The table split line
* @param center_target Optional aim anchor
*/
[DisallowMultipleComponent]
public sealed class CPUHandStriker : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Ball reference")]
    public BallPhysics2D ball;

    [Tooltip("Center line of table")]
    public Transform center_line;

    [Tooltip("Aim anchor on table")]
    public Transform center_target;

    [Tooltip("Optional ball rigidbody override")]
    public Rigidbody2D ball_body_override;

    [Header("Strike Window")]
    [Tooltip("Strike radius around hand")]
    public float strike_radius = 0.65f;

    [Tooltip("Minimum seconds between strikes")]
    public float strike_cooldown = 0.35f;

    [Tooltip("Predict seconds ahead")]
    public float predict_time = 0.18f;

    [Header("Ball Output")]
    [Tooltip("Min outgoing speed")]
    public float min_out_speed = 7.5f;

    [Tooltip("Max outgoing speed")]
    public float max_out_speed = 16f;

    [Tooltip("Add aim randomness")]
    public float aim_jitter = 0.35f;

    [Header("Method")]
    [Tooltip("Apply impulse to ball")]
    public bool strike_ball_direct = true;

    [Tooltip("Drive paddle rigidbody")]
    public bool strike_via_paddle = false;

    [Tooltip("Optional paddle rigidbody")]
    public Rigidbody2D paddle_body;

    [Header("Side Split")]
    [Tooltip("Use Y to split sides")]
    public bool split_by_y = true;

    private PlayerOwner player_owner;
    private float next_strike_time;

    /*
    * Cache side info.
    * @param none
    */
    void Awake()
    {
        player_owner = GetComponentInParent<PlayerOwner>();
    }

    /*
    * Poll for strike chances and execute if valid.
    * @param none
    */
    void Update()
    {
        if (ball == null)
        {
            return;
        }

        if (Time.time < next_strike_time)
        {
            return;
        }

        Vector2 hand_pos = GetHandWorldPosition();
        Vector2 ball_pos = ball.transform.position;

        if (Vector2.Distance(hand_pos, ball_pos) > strike_radius)
        {
            return;
        }

        Vector2 aim_point = ChooseAimPoint();
        Vector2 desired_v = ComputeDesiredBallVelocity(ball_pos, aim_point);
        Rigidbody2D ball_body = ResolveBallBody();

        if (ball_body == null)
        {
            Debug.LogWarning("CPUHandStriker could not find ball rigidbody");
            return;
        }

#if UNITY_6000_0_OR_NEWER
        Vector2 current_v = ball_body.linearVelocity;
#else
        Vector2 current_v = ball_body.velocity;
#endif
        Vector2 dv = desired_v - current_v;

        if (strike_ball_direct == true)
        {
            Vector2 impulse = ball_body.mass * dv;
            ball_body.AddForce(impulse, ForceMode2D.Impulse);
            next_strike_time = Time.time + strike_cooldown;
            return;
        }

        if (strike_via_paddle == true)
        {
            if (paddle_body == null)
            {
                Debug.LogWarning("CPUHandStriker needs paddle body");
                return;
            }

            Vector2 swing_v = dv;
            float swing_speed = Mathf.Clamp(swing_v.magnitude, min_out_speed, max_out_speed);
            Vector2 swing_dir = Vector2.zero;

            if (swing_v.magnitude > 0.001f)
            {
                swing_dir = swing_v.normalized;
            }

#if UNITY_6000_0_OR_NEWER
            paddle_body.linearVelocity = swing_dir * swing_speed;
#else
            paddle_body.velocity = swing_dir * swing_speed;
#endif
            next_strike_time = Time.time + strike_cooldown;
        }
    }

    /*
    * Compute hand world position by walking the parent chain.
    * @param none
    */
    public Vector2 GetHandWorldPosition()
    {
        Transform t = transform;
        Vector3 p = Vector3.zero;
        Quaternion r = Quaternion.identity;
        Vector3 s = Vector3.one;

        while (t != null)
        {
            p = t.localPosition + (r * Vector3.Scale(s, Vector3.zero)) + p;
            r = t.localRotation * r;
            s = Vector3.Scale(t.localScale, s);
            t = t.parent;
        }

        return transform.position;
    }

    /*
    * Pick an aim point on the far side or at a center target.
    * @param none
    */
    private Vector2 ChooseAimPoint()
    {
        if (center_line == null)
        {
            Vector2 fallback = ball.transform.position + new Vector3(0f, 2f, 0f);
            return fallback;
        }

        Vector2 anchor = Vector2.zero;

        if (center_target != null)
        {
            anchor = center_target.position;
        }
        else
        {
            anchor = center_line.position;
        }

        Vector2 jitter = new Vector2(Random.Range(-aim_jitter, aim_jitter), Random.Range(-aim_jitter, aim_jitter));
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

    /*
    * Compute an outgoing ball velocity to reach the aim point.
    * Uses a simple time guess and clamps speed.
    * @param from Current ball position
    * @param to Target point on table
    */
    private Vector2 ComputeDesiredBallVelocity(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        float desired_speed = Mathf.Clamp(distance / Mathf.Max(0.1f, predict_time), min_out_speed, max_out_speed);

        Vector2 dir = to - from;
        if (dir.sqrMagnitude > 0f)
        {
            dir.Normalize();
        }

        return dir * desired_speed;
    }

    /*
    * Return the rigidbody2d for the ball.
    * @param none
    */
    private Rigidbody2D ResolveBallBody()
    {
        if (ball_body_override != null)
        {
            return ball_body_override;
        }

        Rigidbody2D rb = null;
        if (ball != null)
        {
            rb = ball.GetComponent<Rigidbody2D>();
        }

        return rb;
    }
}
