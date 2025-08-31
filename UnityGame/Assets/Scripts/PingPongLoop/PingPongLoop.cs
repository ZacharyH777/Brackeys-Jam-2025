using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.SceneManagement;
#endif


public enum PlayerId { P1 = 0, P2 = 1 }

public enum FaultType
{
    None = 0,
    Floor = 1,
    DoubleBounce = 2,       // same side twice without a hit between
    VolleyBeforeBounce = 3, // hit before any bounce after opponent's hit
    WrongSideBounce = 4,    // bounced back on hitter's own side
    ConsecutiveHit = 5,     // same player hit twice in a row
    IllegalServe = 6,
    ServeOutOfTurn = 7
}

[DisallowMultipleComponent]
public sealed class PingPongLoop : MonoBehaviour
{
    // ---------- Static API: call this from PlayerMovement when A/Space is pressed ----------
    private static PingPongLoop _instance;
    public static void RequestServe(PlayerId requester)
    {
        if (_instance != null) _instance.OnServeRequestFromPlayer(requester);
    }

    [Header("Ball")]
    [Tooltip("Ball physics")]
    public BallPhysics2D ball;

    [Header("Table Side")]
    [Tooltip("Center transform (e.g., the net)")]
    public Transform center_line;
    [Tooltip("If true split by Y, else split by X")]
    public bool split_by_y = true;

    [Header("Serve Rules")]
    [Tooltip("True = server must bounce on server then on receiver; False = direct to receiver")]
    public bool two_bounce_serve = false;

    [Header("Match")]
    [Tooltip("First server")]
    public PlayerId starting_server = PlayerId.P1;
    [Tooltip("Points to win")]
    public int target_score = 7;
    [Tooltip("Switch server every N points")]
    public int switch_every = 2;

    [Header("Serve")]
    [Tooltip("Serve director")]
    public ServeDirector serve_director;
    [Tooltip("Delay (s) before next serve is armed")]
    public float next_serve_delay = 0.8f;

    [Header("Optional Input (for end screen)")]
    public InputActionReference continue_action_ref;

    [Header("Scene")]
    public string next_scene_name = "";
    public float end_delay_seconds = 3f;

    [Header("Input Cleanup on Game Over")]
    [Tooltip("Unpair all PlayerInput devices when the game ends.")]
    public bool unpair_devices_on_game_over = true;
    [Tooltip("Reset DeviceRegistry so next scene re-scans devices.")]
    public bool reset_device_registry_on_game_over = true;

    [Header("Debug")]
    public bool verbose_logging = true;

    // runtime
    private PlayerId current_server;
    private int serves_left_in_pair;
    private int p1_score, p2_score;

    private bool game_over;
    private bool in_rally;               // ball is "live"
    private bool serve_pending_press;    // waiting for server to press
    private bool serve_after_toss;       // toss happened; still in serve phase

    private bool end_pressed;
    private InputAction continue_action;

    // hit/bounce tracking
    private bool has_last_hitter;
    private PlayerId last_hitter_player;
    private int bounces_since_last_hit;

    // universal same-side double-bounce tracker
    private bool has_last_bounce_side;
    private PlayerId last_bounce_side;
    private int same_side_bounce_count;

    // last fault (for logs/UI)
    private FaultType last_fault_type = FaultType.None;
    private PlayerId last_fault_by = PlayerId.P1;

    // PLaySound
    public PlaySound playSound;

    // ---------------- Lifecycle ----------------
    void Awake()
    {
        _instance = this;

        ResetMatch();

        if (ball == null)
        {
            Debug.LogWarning("PingPongLoop: ball reference missing");
        }
        else
        {
            ball.on_round_end    += OnBallFloorKo;
            ball.on_table_bounce += OnBallTableBounce;
            ball.on_paddle_hit   += OnBallPaddleHit;
        }
    }

    void Start()
    {
        ArmServePending(); // place + suspend ball; wait for press
    }

    void OnEnable()
    {
        if (continue_action_ref != null)
        {
            continue_action = continue_action_ref.action;
            if (continue_action != null)
            {
                continue_action.performed += OnContinuePerformed;
                continue_action.Enable();
            }
        }
    }

    void OnDisable()
    {
        if (continue_action != null)
        {
            continue_action.performed -= OnContinuePerformed;
            continue_action = null;
        }

        if (ball != null)
        {
            ball.on_round_end    -= OnBallFloorKo;
            ball.on_table_bounce -= OnBallTableBounce;
            ball.on_paddle_hit   -= OnBallPaddleHit;
        }

        if (_instance == this) _instance = null;
    }

    // ---------------- Public helpers ----------------
    public void ResetMatch()
    {
        current_server = starting_server;
        serves_left_in_pair = switch_every;
        p1_score = p2_score = 0;

        game_over = false;
        in_rally = false;
        serve_pending_press = false;
        serve_after_toss = false;

        end_pressed = false;

        ClearHitBounceState();

        last_fault_type = FaultType.None;
        last_fault_by = PlayerId.P1;

        if (verbose_logging) Debug.Log($"Match reset. Server: {current_server}");
    }

    public PlayerId GetCurrentServer() => current_server;
    public void GetScores(out int p1, out int p2) { p1 = p1_score; p2 = p2_score; }
    public bool IsGameOver() => game_over;
    public FaultType GetLastFaultType() => last_fault_type;
    public PlayerId GetLastFaultBy() => last_fault_by;

    // ---------------- Serve flow ----------------
    private void ArmServePending()
    {
        // place ball at spawn, suspend physics
        if (serve_director != null && ball != null)
        {
            serve_director.PositionBallForServe(current_server);
            ball.SetServeSuspended(true);
        }

        in_rally = false;
        serve_pending_press = true;
        serve_after_toss = false;

        ClearHitBounceState(); // no last hitter/bounce

        if (verbose_logging) Debug.Log($"Waiting for {current_server} to press serve…");
        
        // Notify CPU player if it's their turn to serve
        NotifyCPUPlayerIfNeeded();
    }
    
    private void NotifyCPUPlayerIfNeeded()
    {
        // Check if the current server is CPU and notify the appropriate CPU player
        bool isCPUServer = false;
        
        if (current_server == PlayerId.P2 && CharacterSelect.IsP2CPU)
        {
            isCPUServer = true;
        }
        // Also check for P1 CPU (in case user tests with P1 as CPU)
        else if (current_server == PlayerId.P1)
        {
            // Check if P1 is CPU by looking for CPU player components
            var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
            foreach (var cpu in cpuPlayers)
            {
                if (cpu.player_owner != null && cpu.player_owner.player_id == PlayerId.P1 && cpu.IsCPUPlayer)
                {
                    isCPUServer = true;
                    break;
                }
            }
        }
        
        if (isCPUServer)
        {
            // Find the CPU player in the scene and request serve
            var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
            foreach (var cpu in cpuPlayers)
            {
                if (cpu.player_owner != null && cpu.player_owner.player_id == current_server && cpu.IsCPUPlayer)
                {
                    cpu.RequestServe();
                    if (verbose_logging) Debug.Log($"Notified CPU Player {current_server} to serve");
                    break;
                }
            }
        }
    }

    private void OnServeRequestFromPlayer(PlayerId requester)
    {
        if (game_over || !serve_pending_press) return;

        if (requester != current_server)
        {
            if (verbose_logging) Debug.Log($"Ignore serve request from {requester}; current server is {current_server}.");
            return;
        }

        if (serve_director == null || ball == null)
        {
            Debug.LogWarning("Missing ServeDirector or Ball.");
            return;
        }

        // Wake physics and toss
        ball.SetServeSuspended(false);
        serve_director.TossBall(current_server);

        serve_pending_press = false;
        serve_after_toss = true;
        in_rally = true;

        // The server will be first legal hitter (confirmed on paddle event).
        has_last_hitter = false;
        bounces_since_last_hit = 0;
        has_last_bounce_side = false;
        same_side_bounce_count = 0;

        if (verbose_logging) Debug.Log($"Serve tossed by {current_server}");
    }

    // ---------------- Ball callbacks ----------------
    private void OnBallPaddleHit(PaddleKinematics kin, Collider2D col)
    {
        if (!in_rally || kin == null) return;

        var owner = kin.GetComponentInParent<PlayerOwner>();
        if (owner == null) { Debug.LogWarning("Paddle missing PlayerOwner"); return; }

        var hitter = owner.player_id;

        // During serve phase, only the server is allowed to hit
        if (serve_after_toss && hitter != current_server)
        {
            last_fault_type = FaultType.ServeOutOfTurn;
            last_fault_by = hitter;
            FaultAgainst(hitter, "Serve out of turn");
            return;
        }

        // First legal hit of the rally (usually server immediately after toss)
        if (!has_last_hitter)
        {
            has_last_hitter = true;
            last_hitter_player = hitter;
            bounces_since_last_hit = 0;

            // reset same-side tracker on any paddle hit
            has_last_bounce_side = false;
            same_side_bounce_count = 0;

            if (verbose_logging) Debug.Log($"First hit by {hitter}");
            return;
        }

        // Consecutive hit (safety)
        if (hitter == last_hitter_player)
        {
            last_fault_type = FaultType.ConsecutiveHit;
            last_fault_by = hitter;
            FaultAgainst(hitter, "Consecutive hit");
            return;
        }

        // Normal rally: receiver must allow one bounce before hitting
        if (!serve_after_toss && bounces_since_last_hit == 0)
        {
            last_fault_type = FaultType.VolleyBeforeBounce;
            last_fault_by = hitter;
            FaultAgainst(hitter, "Volley before bounce");
            return;
        }

        // Legal return
        last_hitter_player = hitter;
        bounces_since_last_hit = 0;

        // reset same-side tracker on a legal hit
        has_last_bounce_side = false;
        same_side_bounce_count = 0;

        // Once anyone has legally returned, we are out of serve-phase
        serve_after_toss = false;

        if (verbose_logging) Debug.Log($"Return by {hitter}");
    }

    private void OnBallTableBounce(Vector2 world_pos)
    {
        if (!in_rally) return;

        var side = GetSideForPoint(world_pos);

        // WRONG-SIDE: bounced back on hitter's own side (before crossing)
        if (has_last_hitter && side == last_hitter_player)
        {
            last_fault_type = FaultType.WrongSideBounce;
            last_fault_by = last_hitter_player;
            FaultAgainst(last_hitter_player, "Wrong side bounce");
            return;
        }

        // UNIVERSAL DOUBLE-BOUNCE: same side twice without paddle contact between
        if (has_last_bounce_side && side == last_bounce_side)
        {
            same_side_bounce_count += 1;
            if (same_side_bounce_count >= 2)
            {
                last_fault_type = FaultType.DoubleBounce;
                last_fault_by = side; // fault on that side
                FaultAgainst(side, "Double bounce");
                return;
            }
        }
        else
        {
            has_last_bounce_side = true;
            last_bounce_side = side;
            same_side_bounce_count = 1;
        }

        // Serve legality:
        if (serve_after_toss)
        {
            if (two_bounce_serve)
            {
                // Expect: first bounce on server, second on receiver
                if (same_side_bounce_count == 1)
                {
                    if (side != current_server)
                    {
                        last_fault_type = FaultType.IllegalServe;
                        last_fault_by = current_server;
                        FaultAgainst(current_server, "Illegal serve (first bounce not on server side)");
                        return;
                    }
                }
                else // second bounce
                {
                    var receiver = OpponentOf(current_server);
                    if (side != receiver)
                    {
                        last_fault_type = FaultType.IllegalServe;
                        last_fault_by = current_server;
                        FaultAgainst(current_server, "Illegal serve (second bounce not on receiver side)");
                        return;
                    }
                    // After second legal serve bounce we're in open rally:
                    has_last_hitter = true;
                    last_hitter_player = current_server;
                    bounces_since_last_hit = 1;
                    serve_after_toss = false;
                    if (verbose_logging) Debug.Log("Serve completed (two-bounce serve).");
                }
            }
            else
            {
                // Expect: first bounce on receiver
                var receiver = OpponentOf(current_server);
                if (side != receiver)
                {
                    last_fault_type = FaultType.IllegalServe;
                    last_fault_by = current_server;
                    FaultAgainst(current_server, "Illegal serve (must land on receiver)");
                    return;
                }
                has_last_hitter = true;
                last_hitter_player = current_server;
                bounces_since_last_hit = 1; // receiver side has 1 bounce pending
                serve_after_toss = false;
                if (verbose_logging) Debug.Log("Serve completed (direct).");
            }
        }
        else
        {
            // Open rally: count bounces since the last hitter
            bounces_since_last_hit += 1;
            // (Double-bounce case is handled above by the same-side tracker.)
        }
    }

    private void OnBallFloorKo()
    {
        if (!in_rally) return;

        PlayerId fault_by;
        if (serve_after_toss)
        {
            // During serve phase, floor is server's fault.
            fault_by = current_server;
        }
        else if (has_last_hitter)
        {
            // If NO bounce has happened since the last hit -> hitter hit out.
            // If at least one bounce happened on receiver side -> receiver failed to return.
            fault_by = (bounces_since_last_hit == 0) ? last_hitter_player
                                                     : OpponentOf(last_hitter_player);
        }
        else
        {
            // No one has hit yet (should not happen outside serve) – charge server.
            fault_by = current_server;
        }

        last_fault_type = FaultType.Floor;
        last_fault_by = fault_by;
        FaultAgainst(fault_by, "Floor");
    }

    // ---------------- Scoring / serve cycling ----------------
    private void FaultAgainst(PlayerId fault_by, string reason)
    {
        if (game_over || !in_rally) return;

        PlayerId winner = OpponentOf(fault_by);

        if (verbose_logging)
            Debug.Log($"Fault by {fault_by} ({reason}). Point to {winner}.");

        AwardPoint(winner);
    }

    public void AwardPoint(PlayerId winner)
    {
        playSound.sfx_point_score();
        if (game_over) return;

        in_rally = false;
        serve_after_toss = false;
        serve_pending_press = false;

        if (winner == PlayerId.P1) p1_score++; else p2_score++;

        if (verbose_logging)
            Debug.Log($"Point {winner} due to {last_fault_type} by {last_fault_by}. Score {p1_score} - {p2_score}");

        serves_left_in_pair--;
        if (serves_left_in_pair <= 0)
        {
            SwapServer();
            serves_left_in_pair = switch_every;
        }

        if (p1_score >= target_score || p2_score >= target_score)
        {
            EndGame(p1_score >= target_score ? PlayerId.P1 : PlayerId.P2);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(NextServePendingRoutine());
    }

    private IEnumerator NextServePendingRoutine()
    {
        float t = 0f;
        while (t < next_serve_delay) { t += Time.unscaledDeltaTime; yield return null; }
        ArmServePending();
    }

    private void EndGame(PlayerId winner)
    {
        game_over = true;

        // Freeze ball so no more callbacks fire during end screen
        if (ball != null)
        {
            ball.SetServeSuspended(true);
            ball.enabled = false;
        }

        if (verbose_logging) Debug.Log($"Game Over. {winner} wins.");

        // Clean up input ownership now so the next scene can re-claim devices
        if (unpair_devices_on_game_over) CleanupInputOwnership();

        if (reset_device_registry_on_game_over && DeviceRegistry.Instance != null)
        {
            DeviceRegistry.ResetAll();
        }

        StopAllCoroutines();
        StartCoroutine(EndSequenceRoutine());
    }

    private void CleanupInputOwnership()
    {
    #if UNITY_6000_0_OR_NEWER
        var players = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
    #else
        var players = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
    #endif
        foreach (var pi in players)
        {
            // Prefer your cleaner (idempotent) if present
            var cleaner = pi.GetComponentInParent<PlayerInputCleanup>();
            if (cleaner != null) cleaner.UnpairNow();
            else
            {
                var user = pi.user;
                if (user.valid) user.UnpairDevices();
            }
        }
    }

    private IEnumerator EndSequenceRoutine()
    {
        float elapsed = 0f;
        bool advanced = false;

        while (elapsed < end_delay_seconds && !advanced && !end_pressed)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        advanced = true;

        if (string.IsNullOrEmpty(next_scene_name))
        {
            Debug.LogWarning("Next scene name is empty");
            yield break;
        }

        SceneManager.LoadScene(next_scene_name, LoadSceneMode.Single);
    }

    // ---------------- Utils ----------------
    private void SwapServer()
    {
        current_server = (current_server == PlayerId.P1) ? PlayerId.P2 : PlayerId.P1;
        if (verbose_logging) Debug.Log($"Server swapped. Now: {current_server}");
    }

    private PlayerId OpponentOf(PlayerId id) => (id == PlayerId.P1) ? PlayerId.P2 : PlayerId.P1;

    private void OnContinuePerformed(InputAction.CallbackContext ctx)
    {
        if (game_over) end_pressed = true;
    }

    private void ClearHitBounceState()
    {
        has_last_hitter = false;
        last_hitter_player = PlayerId.P1;
        bounces_since_last_hit = 0;

        has_last_bounce_side = false;
        same_side_bounce_count = 0;
    }

    private PlayerId GetSideForPoint(Vector2 world_pos)
    {
        float line = 0f;
        if (center_line != null)
            line = split_by_y ? center_line.position.y : center_line.position.x;

        if (split_by_y)
            return (world_pos.y >= line) ? PlayerId.P2 : PlayerId.P1;
        else
            return (world_pos.x >= line) ? PlayerId.P2 : PlayerId.P1;
    }
}
