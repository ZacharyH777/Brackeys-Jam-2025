using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ServeDirector : MonoBehaviour
{
    [Header("Ball")]
    public BallPhysics2D ball;

    [Header("Spawns")]
    public Transform p1_spawn;
    public Transform p2_spawn;

    [Header("Toss")]
    [Tooltip("Initial Z velocity (fake height) applied at toss.")]
    public float toss_z_velocity = 6f;
    [Tooltip("Table-plane speed on toss in the server->receiver forward direction.")]
    public float toss_forward_speed = 0.8f;
    [Tooltip("Optional small X/Y randomization for a little variance (0 = none).")]
    public float toss_xy_jitter = 0f;

    [Header("Forward Mapping")]
    [Tooltip("If true, 'forward' is +Y for P1 and -Y for P2; if false, 'forward' is +X / -X.")]
    public bool forward_by_y = true;
    [Tooltip("Flip the computed forward (use if your table orientation is reversed).")]
    public bool invert_forward = false;

    [Header("Safety / Timing")]
    [Tooltip("Short grace period after toss (cosmetic).")]
    public float settle_seconds = 0.15f;
    [Tooltip("If true, also zero ball spin when positioning for serve.")]
    public bool clear_spin_on_serve = true;

    // Optional informational events
    public event Action<PlayerId> on_positioned_for_serve;
    public event Action<PlayerId> on_tossed;
    public event Action on_settled;

    private Rigidbody2D _rb;

    void Awake()
    {
        EnsureBallAndRigidbody();
    }

    // ---------------- API ----------------

    /// <summary>
    /// Place ball at server spawn, fully freeze ball physics, clear velocities & Z,
    /// and leave it suspended until TossBall() is called by the serve press.
    /// </summary>
    public void PositionBallForServe(PlayerId server)
    {
        if (!EnsureBallAndRigidbody()) return;

        Transform spawn = (server == PlayerId.P1) ? p1_spawn : p2_spawn;
        if (spawn == null)
        {
            Debug.LogWarning("[ServeDirector] Spawn transform missing for " + server);
            return;
        }

        // Place & reset RB2D
        ball.enabled = true; // in case something disabled it between points
        ball.transform.position = spawn.position;

#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = Vector2.zero;
#else
        _rb.velocity = Vector2.zero;
#endif
        _rb.angularVelocity = 0f;

        // Reset ball internal state & suspend
        ball.ClearHitLock();
        ball.SetZ(0f, 0f);
        if (clear_spin_on_serve)
        {
            // If you have a public API to clear spin, call it; if not, it's safe to ignore.
            // Example (if present): ball.SetSpin(0f);
        }
        ball.SetServeSuspended(true);

        // notify
        on_positioned_for_serve?.Invoke(server);
    }

    /// <summary>
    /// Wake physics, push XY forward and pop Z; used when the server presses Serve.
    /// </summary>
    public void TossBall(PlayerId server)
    {
        if (!EnsureBallAndRigidbody()) return;

        // Wake ball physics first
        ball.SetServeSuspended(false);

        Vector2 forward = GetServeForward(server);
        Vector2 xy = forward * Mathf.Max(0f, toss_forward_speed);

        // Optional tiny lateral randomness
        if (toss_xy_jitter > 0f)
        {
            float jx = UnityEngine.Random.Range(-toss_xy_jitter, toss_xy_jitter);
            float jy = UnityEngine.Random.Range(-toss_xy_jitter, toss_xy_jitter);
            xy += new Vector2(jx, jy);
        }

#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = xy;
#else
        _rb.velocity = xy;
#endif
        ball.SetZ(0f, toss_z_velocity);

        on_tossed?.Invoke(server);

        // Cosmetic settle window (no hard effect; useful if you want to gate SFX/UI)
        StopAllCoroutines();
        StartCoroutine(SettleWindow());
    }

    // ---------------- Internals ----------------

    private Vector2 GetServeForward(PlayerId server)
    {
        // Base forward along chosen axis
        Vector2 axis = forward_by_y ? Vector2.up : Vector2.right;

        // P1 goes +axis, P2 goes -axis
        Vector2 dir = (server == PlayerId.P1) ? axis : -axis;

        if (invert_forward) dir = -dir;

        return dir.normalized;
    }

    private IEnumerator SettleWindow()
    {
        float t = 0f;
        while (t < settle_seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        on_settled?.Invoke();
    }

    private bool EnsureBallAndRigidbody()
    {
        if (ball == null)
        {
            Debug.LogWarning("[ServeDirector] Ball reference missing.");
            return false;
        }
        if (_rb == null)
        {
            _rb = ball.GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                Debug.LogWarning("[ServeDirector] Ball Rigidbody2D missing.");
                return false;
            }
        }
        return true;
    }
}
