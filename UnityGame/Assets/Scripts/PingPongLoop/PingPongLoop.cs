using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum PlayerId { P1 = 0, P2 = 1 }

/*
 
Controls a single ping pong game: serve order, scoring, faults, win detection, and scene advance.

*/
[DisallowMultipleComponent]
public sealed class PingPongLoop : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("First server.")]
    public PlayerId starting_server = PlayerId.P1;
    [Tooltip("Points to win.")]
    public int target_score = 7;
    [Tooltip("Points per server.")]
    public int switch_every = 2;

    [Header("Input")]
    [Tooltip("Advance action.")]
    public InputActionReference continue_action_ref;

    [Header("Scene")]
    [Tooltip("Scene to load.")]
    public string next_scene_name = "";
    [Tooltip("Seconds to wait.")]
    public float end_delay_seconds = 3f;

    [Header("Debug")]
    [Tooltip("Show logs.")]
    public bool verbose_logging = true;

    private PlayerId current_server;
    private int serves_left_in_pair;
    private int p1_score;
    private int p2_score;
    private bool game_over;
    private bool in_rally;
    private InputAction continue_action;
    private bool end_pressed;

    /* Ensure clean state and set initial server. */
    void Awake()
    {
        ResetMatch();
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
    }

    /*
    Reset all scores and serving state and start the first rally setup.
    */
    public void ResetMatch()
    {
        current_server = starting_server;
        serves_left_in_pair = switch_every;
        p1_score = 0;
        p2_score = 0;
        game_over = false;
        in_rally = false;
        end_pressed = false;

        if (verbose_logging)
        {
            Debug.Log("Match reset. Server: " + current_server);
        }
    }

    /*
     
    Mark that the next rally is about to start. Call this after you have reset the ball for the next point.
    */
    public void StartNewRally()
    {
        if (game_over)
        {
            return;
        }

        in_rally = true;

        if (verbose_logging)
        {
            Debug.Log("Rally started. Server: " + current_server + " | Score " + p1_score + " - " + p2_score);
        }
    }

    /*
     
    Record a fault by a specific player and award the point to the opponent. Ends the rally automatically.

    Common reasons you can pass for clarity in logs:
    "Double bounce", "Floor", "Volley before bounce", "Consecutive hit"
    
    @param fault_by The player who committed the fault.
    @param reason A short reason string for logs.
    */
    public void FaultAgainst(PlayerId fault_by, string reason)
    {
        if (game_over)
        {
            return;
        }

        if (!in_rally)
        {
            Debug.LogWarning("Fault received while not in rally. Ignoring.");
            return;
        }

        PlayerId winner = OpponentOf(fault_by);

        if (verbose_logging)
        {
            Debug.Log("Fault by " + fault_by + " (" + reason + "). Point to " + winner + ".");
        }

        AwardPoint(winner);
    }

    /*
     
    Manually award a point to a player and handle serve switching and game over detection.

    @param winner The player who wins the point.
    */
    public void AwardPoint(PlayerId winner)
    {
        if (game_over)
        {
            return;
        }

        in_rally = false;

        if (winner == PlayerId.P1)
        {
            p1_score = p1_score + 1;
        }
        else
        {
            p2_score = p2_score + 1;
        }

        if (verbose_logging)
        {
            Debug.Log("Point " + winner + ". Score " + p1_score + " - " + p2_score);
        }

        serves_left_in_pair = serves_left_in_pair - 1;

        if (serves_left_in_pair <= 0)
        {
            SwapServer();
            serves_left_in_pair = switch_every;
        }

        bool p1_won = p1_score >= target_score;
        bool p2_won = p2_score >= target_score;

        if (p1_won || p2_won)
        {
            PlayerId game_winner = p1_won ? PlayerId.P1 : PlayerId.P2;
            EndGame(game_winner);
        }
    }

    /*
     
    Get the current server.

    @return The player who serves next.
    */
    public PlayerId GetCurrentServer()
    {
        return current_server;
    }

    /*
     
    Get the scores.

    @param p1 Returns player one score.
    @param p2 Returns player two score.
    */
    public void GetScores(out int p1, out int p2)
    {
        p1 = p1_score;
        p2 = p2_score;
    }

    /*
     
    True if the match is finished.

    @return Whether the game is over.
    */
    public bool IsGameOver()
    {
        return game_over;
    }

    private void SwapServer()
    {
        if (current_server == PlayerId.P1)
        {
            current_server = PlayerId.P2;
        }
        else
        {
            current_server = PlayerId.P1;
        }

        if (verbose_logging)
        {
            Debug.Log("Server swapped. Now serving: " + current_server);
        }
    }

    private PlayerId OpponentOf(PlayerId id)
    {
        if (id == PlayerId.P1)
        {
            return PlayerId.P2;
        }

        return PlayerId.P1;
    }

    private void EndGame(PlayerId winner)
    {
        game_over = true;

        Debug.Log("Game Over. " + winner + " wins.");

        StopAllCoroutines();
        StartCoroutine(EndSequenceRoutine());
    }

    private IEnumerator EndSequenceRoutine()
    {
        float elapsed = 0f;
        bool advanced = false;

        while (elapsed < end_delay_seconds && !advanced && !end_pressed)
        {
            elapsed = elapsed + Time.unscaledDeltaTime;
            yield return null;
        }

        advanced = true;

        if (string.IsNullOrEmpty(next_scene_name))
        {
            Debug.LogWarning("Next scene name is empty.");
            yield break;
        }

        SceneManager.LoadScene(next_scene_name, LoadSceneMode.Single);
    }

    private void OnContinuePerformed(InputAction.CallbackContext ctx)
    {
        if (!game_over)
        {
            return;
        }

        end_pressed = true;
    }
}
