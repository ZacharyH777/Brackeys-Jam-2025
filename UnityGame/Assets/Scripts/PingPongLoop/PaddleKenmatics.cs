using UnityEngine;

/*
* Exposes 2D velocity for paddles using two sources.
* Chooses per-axis the larger magnitude from body and arm rigidbodies.
* Falls back to transform delta if both are missing.
*/
[DisallowMultipleComponent]
public sealed class PaddleKinematics : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("Rigidbody on body")]
    public Rigidbody2D rigidbody2d_body;
    [Tooltip("Rigidbody on arm")]
    public Rigidbody2D rigidbody2d_arm;

    [Header("Output")]
    [Tooltip("Current velocity in world")]
    public Vector2 current_velocity;

    private Vector3 last_pos;

    /*
    Ensure sources are set and cache last position.
    */
    void Awake()
    {
        bool has_body = rigidbody2d_body != null;
        bool has_arm = rigidbody2d_arm != null;

        if (!has_body && !has_arm)
        {
            rigidbody2d_body = GetComponent<Rigidbody2D>();
        }

        last_pos = transform.position;
    }

    /*
    Update velocity using sources or transform delta when needed.
    */
    void FixedUpdate()
    {
        bool has_body = rigidbody2d_body != null;
        bool has_arm = rigidbody2d_arm != null;

        if (has_body || has_arm)
        {
            Vector2 v_body = Vector2.zero;
            Vector2 v_arm = Vector2.zero;

            if (has_body)
            {
                v_body = rigidbody2d_body.linearVelocity;
            }

            if (has_arm)
            {
                v_arm = rigidbody2d_arm.linearVelocity;
            }

            float x;
            if (has_body && has_arm)
            {
                if (Mathf.Abs(v_body.x) >= Mathf.Abs(v_arm.x))
                {
                    x = v_body.x;
                }
                else
                {
                    x = v_arm.x;
                }
            }
            else if (has_body)
            {
                x = v_body.x;
            }
            else
            {
                x = v_arm.x;
            }

            float y;
            if (has_body && has_arm)
            {
                if (Mathf.Abs(v_body.y) >= Mathf.Abs(v_arm.y))
                {
                    y = v_body.y;
                }
                else
                {
                    y = v_arm.y;
                }
            }
            else if (has_body)
            {
                y = v_body.y;
            }
            else
            {
                y = v_arm.y;
            }

            current_velocity = new Vector2(x, y);
            return;
        }

        Vector3 pos = transform.position;
        Vector2 delta = new Vector2(pos.x - last_pos.x, pos.y - last_pos.y);
        float dt = Time.fixedDeltaTime;

        if (dt > 0f)
        {
            current_velocity = delta / dt;
        }

        last_pos = pos;
    }
}
