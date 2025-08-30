using UnityEngine;

public enum CPUDifficulty { Easy = 0, Medium = 1, Hard = 2 }

[DisallowMultipleComponent]
[RequireComponent(typeof(StatSystem))]
public sealed class CPUPlayer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Ball physics component to track and predict")]
    public BallPhysics2D ball;
    [Tooltip("Player owner component to identify which player this CPU controls")]
    public PlayerOwner player_owner;
    [Tooltip("CPU movement component that will receive movement commands")]
    public CPUMovement cpu_movement;

    [Header("AI Configuration")]
    [Tooltip("AI difficulty level (also loads corresponding stat preset)")]
    public CPUDifficulty difficulty = CPUDifficulty.Medium;
    [Tooltip("Custom stat preset to override difficulty preset")]
    public StatPreset customStatPreset;
    [Tooltip("Apply stat preset on start")]
    public bool applyPresetOnStart = true;

    [Header("Legacy Settings (Overridden by Stats)")]
    [Tooltip("Base reaction delay in seconds - overridden by stats")]
    public float base_reaction_time = 0.2f;
    [Tooltip("Random reaction variation (±) - overridden by stats")]
    public float reaction_variation = 0.1f;
    [Tooltip("How far ahead to predict ball position (seconds) - overridden by stats")]
    public float prediction_time = 0.5f;
    [Tooltip("How accurately CPU targets ball (0-1) - overridden by stats")]
    public float accuracy = 0.8f;
    [Tooltip("Random position offset for imperfection - overridden by stats")]
    public float position_noise = 0.3f;
    [Tooltip("Distance threshold to consider ball 'reachable' - overridden by stats")]
    public float reachable_distance = 3f;
    [Tooltip("Delay before serving when it's CPU's turn - overridden by stats")]
    public float serve_delay_min = 0.8f;
    [Tooltip("Maximum serve delay variation - overridden by stats")]
    public float serve_delay_max = 1.5f;

    [Header("Table Awareness")]
    [Tooltip("Center line transform for side detection")]
    public Transform center_line;
    [Tooltip("If true split by Y, else split by X (should match PingPongLoop)")]
    public bool split_by_y = true;

    // runtime state
    private Vector2 current_target_position;
    private float next_reaction_time;
    private bool is_serving_pending;
    private float serve_time;
    private Vector2 last_ball_position;
    private Vector2 last_ball_velocity;
    
    // stat system
    private StatSystem statSystem;
    
    // cached stat values for performance
    private float cached_reaction_time;
    private float cached_reaction_variation;
    private float cached_prediction_time;
    private float cached_accuracy;
    private float cached_position_noise;
    private float cached_reachable_distance;
    private float cached_serve_delay_min;
    private float cached_serve_delay_max;
    private float cached_movement_smoothing;

    void Start()
    {
        // Initialize stat system
        statSystem = GetComponent<StatSystem>();
        if (statSystem == null)
        {
            statSystem = gameObject.AddComponent<StatSystem>();
        }

        // Apply stat presets
        if (applyPresetOnStart)
        {
            ApplyStatPreset();
        }

        // Auto-find references if not assigned
        if (ball == null)
            ball = FindFirstObjectByType<BallPhysics2D>();
        
        if (player_owner == null)
            player_owner = GetComponent<PlayerOwner>();
        
        if (cpu_movement == null)
            cpu_movement = GetComponent<CPUMovement>();
        
        if (center_line == null)
        {
            var pingPongLoop = FindFirstObjectByType<PingPongLoop>();
            if (pingPongLoop != null)
                center_line = pingPongLoop.center_line;
        }

        // Cache stat values and set up change listeners
        CacheStatValues();
        SetupStatListeners();

        // Initialize state
        current_target_position = transform.position;
        next_reaction_time = Time.time + cached_reaction_time;
    }

    void Update()
    {
        if (ball == null || player_owner == null || cpu_movement == null)
            return;

        // Update ball tracking
        TrackBall();

        // Handle serving if pending
        if (is_serving_pending && Time.time >= serve_time)
        {
            PerformServe();
        }

        // Make decisions at reaction intervals
        if (Time.time >= next_reaction_time)
        {
            MakeDecision();
            ScheduleNextReaction();
        }

        // Send movement target to CPU movement component
        if (cpu_movement != null)
        {
            cpu_movement.SetTargetPosition(current_target_position);
        }
    }

    private void ApplyStatPreset()
    {
        if (statSystem == null) return;

        StatPreset preset = customStatPreset;
        if (preset == null)
        {
            preset = AIStatPresets.GetPresetByDifficulty(difficulty);
        }

        if (preset != null)
        {
            statSystem.ApplyStatPreset(preset);
        }
        else
        {
            // Fallback: create stats from legacy values
            CreateDefaultStats();
        }
    }

    private void CreateDefaultStats()
    {
        if (statSystem == null) return;

        statSystem.AddStat(AIStatNames.REACTION_TIME, base_reaction_time, 0.1f, 2.0f);
        statSystem.AddStat(AIStatNames.REACTION_VARIATION, reaction_variation, 0.0f, 1.0f);
        statSystem.AddStat(AIStatNames.PREDICTION_TIME, prediction_time, 0.1f, 2.0f);
        statSystem.AddStat(AIStatNames.MOVEMENT_ACCURACY, accuracy, 0.0f, 1.0f);
        statSystem.AddStat(AIStatNames.POSITION_NOISE, position_noise, 0.0f, 2.0f);
        statSystem.AddStat(AIStatNames.REACHABLE_DISTANCE, reachable_distance, 1.0f, 10.0f);
        statSystem.AddStat(AIStatNames.SERVE_DELAY_MIN, serve_delay_min, 0.5f, 5.0f);
        statSystem.AddStat(AIStatNames.SERVE_DELAY_MAX, serve_delay_max, 1.0f, 10.0f);
        statSystem.AddStat(AIStatNames.MOVEMENT_SMOOTHING, 0.8f, 0.1f, 1.0f);
    }

    private void CacheStatValues()
    {
        if (statSystem == null) return;

        cached_reaction_time = statSystem.GetStatValue(AIStatNames.REACTION_TIME);
        cached_reaction_variation = statSystem.GetStatValue(AIStatNames.REACTION_VARIATION);
        cached_prediction_time = statSystem.GetStatValue(AIStatNames.PREDICTION_TIME);
        cached_accuracy = statSystem.GetStatValue(AIStatNames.MOVEMENT_ACCURACY);
        cached_position_noise = statSystem.GetStatValue(AIStatNames.POSITION_NOISE);
        cached_reachable_distance = statSystem.GetStatValue(AIStatNames.REACHABLE_DISTANCE);
        cached_serve_delay_min = statSystem.GetStatValue(AIStatNames.SERVE_DELAY_MIN);
        cached_serve_delay_max = statSystem.GetStatValue(AIStatNames.SERVE_DELAY_MAX);
        cached_movement_smoothing = statSystem.GetStatValue(AIStatNames.MOVEMENT_SMOOTHING);

        // Update CPU movement smoothing if available
        if (cpu_movement != null)
        {
            cpu_movement.SetMovementSmoothing(cached_movement_smoothing);
        }
    }

    private void SetupStatListeners()
    {
        if (statSystem == null) return;

        statSystem.OnStatChanged += OnStatChanged;
    }

    private void OnStatChanged(string statName, float oldValue, float newValue)
    {
        // Recache the specific stat that changed
        switch (statName)
        {
            case AIStatNames.REACTION_TIME:
                cached_reaction_time = newValue;
                break;
            case AIStatNames.REACTION_VARIATION:
                cached_reaction_variation = newValue;
                break;
            case AIStatNames.PREDICTION_TIME:
                cached_prediction_time = newValue;
                break;
            case AIStatNames.MOVEMENT_ACCURACY:
                cached_accuracy = newValue;
                break;
            case AIStatNames.POSITION_NOISE:
                cached_position_noise = newValue;
                break;
            case AIStatNames.REACHABLE_DISTANCE:
                cached_reachable_distance = newValue;
                break;
            case AIStatNames.SERVE_DELAY_MIN:
                cached_serve_delay_min = newValue;
                break;
            case AIStatNames.SERVE_DELAY_MAX:
                cached_serve_delay_max = newValue;
                break;
            case AIStatNames.MOVEMENT_SMOOTHING:
                cached_movement_smoothing = newValue;
                if (cpu_movement != null)
                    cpu_movement.SetMovementSmoothing(newValue);
                break;
        }
    }

    private void TrackBall()
    {
        if (ball == null) return;

        Vector2 ball_pos = ball.transform.position;
        
        // Calculate velocity if we have previous position
        if (last_ball_position != Vector2.zero)
        {
            last_ball_velocity = (ball_pos - last_ball_position) / Time.deltaTime;
        }

        last_ball_position = ball_pos;
    }

    private void MakeDecision()
    {
        if (ball == null) return;

        Vector2 ball_pos = ball.transform.position;
        Vector2 ball_vel = last_ball_velocity;

        // Predict where ball will be
        Vector2 predicted_ball_pos = ball_pos + (ball_vel * cached_prediction_time);

        // Check if ball is coming toward our side
        bool ball_on_our_side = IsBallOnOurSide(predicted_ball_pos);
        bool ball_coming_to_us = IsBallComingToOurSide(ball_pos, ball_vel);

        if (ball_on_our_side || ball_coming_to_us)
        {
            // Calculate where we should move to intercept
            Vector2 intercept_pos = CalculateInterceptPosition(predicted_ball_pos);
            
            // Apply accuracy and noise
            intercept_pos = ApplyAccuracyAndNoise(intercept_pos);

            current_target_position = intercept_pos;
        }
        else
        {
            // Ball is going away or on opponent's side, move to defensive position
            current_target_position = GetDefensivePosition();
        }
    }

    private Vector2 CalculateInterceptPosition(Vector2 predicted_ball_pos)
    {
        // Simple intercept: move toward predicted ball position
        // In a more advanced AI, this would calculate paddle reach, optimal hit angle, etc.
        
        Vector2 current_pos = transform.position;
        Vector2 toward_ball = (predicted_ball_pos - current_pos);
        
        // Limit how far we'll chase the ball
        if (toward_ball.magnitude > cached_reachable_distance)
        {
            toward_ball = toward_ball.normalized * cached_reachable_distance;
        }

        return current_pos + toward_ball;
    }

    private Vector2 ApplyAccuracyAndNoise(Vector2 target_pos)
    {
        // Apply accuracy (how close to perfect positioning)
        Vector2 current_pos = transform.position;
        Vector2 perfect_move = target_pos - current_pos;
        Vector2 actual_move = perfect_move * cached_accuracy;

        // Add some random noise for imperfection
        Vector2 noise = new Vector2(
            Random.Range(-cached_position_noise, cached_position_noise),
            Random.Range(-cached_position_noise, cached_position_noise)
        );

        return current_pos + actual_move + noise;
    }

    private Vector2 GetDefensivePosition()
    {
        // Return to center of our side for defensive positioning
        if (center_line == null)
            return transform.position;

        Vector2 center_pos = center_line.position;
        PlayerId our_side = player_owner.player_id;

        // Offset from center based on which player we are
        Vector2 defensive_offset = Vector2.zero;
        
        if (split_by_y)
        {
            // Split by Y axis
            defensive_offset.y = (our_side == PlayerId.P1) ? -1.5f : 1.5f;
        }
        else
        {
            // Split by X axis  
            defensive_offset.x = (our_side == PlayerId.P1) ? -1.5f : 1.5f;
        }

        return center_pos + defensive_offset;
    }

    private bool IsBallOnOurSide(Vector2 ball_pos)
    {
        if (center_line == null) return true;

        Vector2 center_pos = center_line.position;
        PlayerId our_side = player_owner.player_id;

        if (split_by_y)
        {
            // Split by Y axis
            if (our_side == PlayerId.P1)
                return ball_pos.y < center_pos.y;
            else
                return ball_pos.y > center_pos.y;
        }
        else
        {
            // Split by X axis
            if (our_side == PlayerId.P1)
                return ball_pos.x < center_pos.x;
            else
                return ball_pos.x > center_pos.x;
        }
    }

    private bool IsBallComingToOurSide(Vector2 ball_pos, Vector2 ball_vel)
    {
        if (center_line == null || ball_vel.magnitude < 0.1f) 
            return false;

        Vector2 center_pos = center_line.position;
        PlayerId our_side = player_owner.player_id;

        if (split_by_y)
        {
            // Split by Y axis
            if (our_side == PlayerId.P1)
                return ball_pos.y > center_pos.y && ball_vel.y < 0; // Ball above center, moving down
            else
                return ball_pos.y < center_pos.y && ball_vel.y > 0; // Ball below center, moving up
        }
        else
        {
            // Split by X axis
            if (our_side == PlayerId.P1)
                return ball_pos.x > center_pos.x && ball_vel.x < 0; // Ball right of center, moving left
            else
                return ball_pos.x < center_pos.x && ball_vel.x > 0; // Ball left of center, moving right
        }
    }

    private void ScheduleNextReaction()
    {
        float reaction_delay = cached_reaction_time + Random.Range(-cached_reaction_variation, cached_reaction_variation);
        reaction_delay = Mathf.Max(0.05f, reaction_delay); // Minimum 50ms reaction
        next_reaction_time = Time.time + reaction_delay;
    }

    // Called by external systems when it's time for this CPU to serve
    public void RequestServe()
    {
        if (is_serving_pending) return; // Already pending

        float serve_delay = Random.Range(cached_serve_delay_min, cached_serve_delay_max);
        serve_time = Time.time + serve_delay;
        is_serving_pending = true;
        
        Debug.Log($"CPU Player {player_owner?.player_id} will serve in {serve_delay:F2}s");
    }

    private void PerformServe()
    {
        is_serving_pending = false;
        
        if (player_owner != null)
        {
            Debug.Log($"CPU Player {player_owner.player_id} executing serve now!");
            PingPongLoop.RequestServe(player_owner.player_id);
        }
    }

    // Public API for other systems to check if this is a CPU player
    public bool IsCPUPlayer => true;

    // Allow external systems to modify difficulty at runtime
    public void SetDifficulty(CPUDifficulty newDifficulty)
    {
        difficulty = newDifficulty;
        ApplyStatPreset();
    }

    // Stat system API for runtime modifications
    public StatSystem GetStatSystem() => statSystem;

    public float GetStatValue(string statName)
    {
        return statSystem?.GetStatValue(statName) ?? 0f;
    }

    public bool SetStatValue(string statName, float value)
    {
        return statSystem?.SetStatBaseValue(statName, value) ?? false;
    }

    public StatModifier AddStatModifier(string statName, float value, StatModifierType type, int order = 0, object source = null)
    {
        return statSystem?.AddModifier(statName, value, type, order, source);
    }

    public bool RemoveStatModifier(string statName, StatModifier modifier)
    {
        return statSystem?.RemoveModifier(statName, modifier) ?? false;
    }

    public void ApplyCustomPreset(StatPreset preset)
    {
        if (preset != null && statSystem != null)
        {
            customStatPreset = preset;
            statSystem.ApplyStatPreset(preset);
        }
    }

    // Advanced AI methods
    public void AdjustDifficultyBasedOnPerformance(float playerWinRatio)
    {
        if (!statSystem.HasStat(AIStatNames.ADAPTIVE_DIFFICULTY)) return;
        
        float adaptiveEnabled = statSystem.GetStatValue(AIStatNames.ADAPTIVE_DIFFICULTY);
        if (adaptiveEnabled < 0.5f) return; // Not enabled
        
        float learningRate = statSystem.GetStatValue(AIStatNames.LEARNING_RATE);
        
        // If player is winning too much, make AI harder
        if (playerWinRatio > 0.7f)
        {
            AdjustDifficulty(learningRate);
        }
        // If player is losing too much, make AI easier
        else if (playerWinRatio < 0.3f)
        {
            AdjustDifficulty(-learningRate);
        }
    }

    private void AdjustDifficulty(float adjustment)
    {
        // Adjust multiple stats slightly
        AddStatModifier(AIStatNames.REACTION_TIME, -adjustment * 0.05f, StatModifierType.FlatAddition, 100, this);
        AddStatModifier(AIStatNames.MOVEMENT_ACCURACY, adjustment * 0.1f, StatModifierType.FlatAddition, 100, this);
        AddStatModifier(AIStatNames.PREDICTION_TIME, adjustment * 0.1f, StatModifierType.FlatAddition, 100, this);
    }

    void OnDestroy()
    {
        // Clean up stat listeners
        if (statSystem != null)
        {
            statSystem.OnStatChanged -= OnStatChanged;
        }
    }
}