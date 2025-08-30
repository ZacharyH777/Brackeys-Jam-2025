using UnityEngine;

public static class AIStatNames
{
    // Reaction & Timing
    public const string REACTION_TIME = "ReactionTime";
    public const string REACTION_VARIATION = "ReactionVariation";
    public const string PREDICTION_TIME = "PredictionTime";
    
    // Movement & Accuracy
    public const string MOVEMENT_ACCURACY = "MovementAccuracy";
    public const string POSITION_NOISE = "PositionNoise";
    public const string MOVEMENT_SPEED_MULTIPLIER = "MovementSpeedMultiplier";
    public const string MOVEMENT_SMOOTHING = "MovementSmoothing";
    
    // Serving
    public const string SERVE_DELAY_MIN = "ServeDelayMin";
    public const string SERVE_DELAY_MAX = "ServeDelayMax";
    
    // Distance & Positioning
    public const string REACHABLE_DISTANCE = "ReachableDistance";
    public const string DEFENSIVE_POSITION_OFFSET = "DefensivePositionOffset";
    
    // Advanced AI Features
    public const string ADAPTIVE_DIFFICULTY = "AdaptiveDifficulty";
    public const string LEARNING_RATE = "LearningRate";
    public const string AGGRESSION_LEVEL = "AggressionLevel";
    public const string CONSISTENCY = "Consistency";
    
    // Match Analysis
    public const string OPPONENT_PREDICTION_ACCURACY = "OpponentPredictionAccuracy";
    public const string PATTERN_RECOGNITION = "PatternRecognition";
    public const string RISK_TAKING = "RiskTaking";
}

public static class AIStatPresets
{
    public static StatPreset CreateEasyPreset()
    {
        var preset = ScriptableObject.CreateInstance<StatPreset>();
        preset.presetName = "Easy AI";
        preset.description = "Beginner-friendly AI with slower reactions and less accuracy";
        preset.category = "Difficulty";
        preset.difficulty = 1;
        preset.presetColor = Color.green;

        preset.stats.Add(CreatePresetStat(AIStatNames.REACTION_TIME, 0.4f, 0.1f, 2.0f, "Base reaction delay in seconds"));
        preset.stats.Add(CreatePresetStat(AIStatNames.REACTION_VARIATION, 0.2f, 0.0f, 1.0f, "Random reaction variation (±)"));
        preset.stats.Add(CreatePresetStat(AIStatNames.PREDICTION_TIME, 0.3f, 0.1f, 2.0f, "Ball prediction lookahead time"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_ACCURACY, 0.6f, 0.0f, 1.0f, "How accurately AI targets ball"));
        preset.stats.Add(CreatePresetStat(AIStatNames.POSITION_NOISE, 0.5f, 0.0f, 2.0f, "Random position offset"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_SPEED_MULTIPLIER, 0.8f, 0.1f, 2.0f, "Movement speed modifier"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_SMOOTHING, 0.6f, 0.1f, 1.0f, "Movement smoothing factor"));
        preset.stats.Add(CreatePresetStat(AIStatNames.SERVE_DELAY_MIN, 1.2f, 0.5f, 5.0f, "Minimum serve delay"));
        preset.stats.Add(CreatePresetStat(AIStatNames.SERVE_DELAY_MAX, 2.5f, 1.0f, 10.0f, "Maximum serve delay"));
        preset.stats.Add(CreatePresetStat(AIStatNames.REACHABLE_DISTANCE, 2.5f, 1.0f, 10.0f, "Max chase distance"));
        preset.stats.Add(CreatePresetStat(AIStatNames.CONSISTENCY, 0.4f, 0.0f, 1.0f, "How consistent AI performance is"));

        return preset;
    }

    public static StatPreset CreateMediumPreset()
    {
        var preset = ScriptableObject.CreateInstance<StatPreset>();
        preset.presetName = "Medium AI";
        preset.description = "Balanced AI with moderate reactions and accuracy";
        preset.category = "Difficulty";
        preset.difficulty = 3;
        preset.presetColor = Color.yellow;

        preset.stats.Add(CreatePresetStat(AIStatNames.REACTION_TIME, 0.25f, 0.1f, 2.0f, "Base reaction delay in seconds"));
        preset.stats.Add(CreatePresetStat(AIStatNames.REACTION_VARIATION, 0.15f, 0.0f, 1.0f, "Random reaction variation (±)"));
        preset.stats.Add(CreatePresetStat(AIStatNames.PREDICTION_TIME, 0.5f, 0.1f, 2.0f, "Ball prediction lookahead time"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_ACCURACY, 0.8f, 0.0f, 1.0f, "How accurately AI targets ball"));
        preset.stats.Add(CreatePresetStat(AIStatNames.POSITION_NOISE, 0.3f, 0.0f, 2.0f, "Random position offset"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_SPEED_MULTIPLIER, 1.0f, 0.1f, 2.0f, "Movement speed modifier"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_SMOOTHING, 0.8f, 0.1f, 1.0f, "Movement smoothing factor"));
        preset.stats.Add(CreatePresetStat(AIStatNames.SERVE_DELAY_MIN, 0.8f, 0.5f, 5.0f, "Minimum serve delay"));
        preset.stats.Add(CreatePresetStat(AIStatNames.SERVE_DELAY_MAX, 1.5f, 1.0f, 10.0f, "Maximum serve delay"));
        preset.stats.Add(CreatePresetStat(AIStatNames.REACHABLE_DISTANCE, 3.5f, 1.0f, 10.0f, "Max chase distance"));
        preset.stats.Add(CreatePresetStat(AIStatNames.CONSISTENCY, 0.7f, 0.0f, 1.0f, "How consistent AI performance is"));

        return preset;
    }

    public static StatPreset CreateHardPreset()
    {
        var preset = ScriptableObject.CreateInstance<StatPreset>();
        preset.presetName = "Hard AI";
        preset.description = "Challenging AI with fast reactions and high accuracy";
        preset.category = "Difficulty";
        preset.difficulty = 5;
        preset.presetColor = Color.red;

        preset.stats.Add(CreatePresetStat(AIStatNames.REACTION_TIME, 0.15f, 0.1f, 2.0f, "Base reaction delay in seconds"));
        preset.stats.Add(CreatePresetStat(AIStatNames.REACTION_VARIATION, 0.08f, 0.0f, 1.0f, "Random reaction variation (±)"));
        preset.stats.Add(CreatePresetStat(AIStatNames.PREDICTION_TIME, 0.8f, 0.1f, 2.0f, "Ball prediction lookahead time"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_ACCURACY, 0.95f, 0.0f, 1.0f, "How accurately AI targets ball"));
        preset.stats.Add(CreatePresetStat(AIStatNames.POSITION_NOISE, 0.1f, 0.0f, 2.0f, "Random position offset"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_SPEED_MULTIPLIER, 1.2f, 0.1f, 2.0f, "Movement speed modifier"));
        preset.stats.Add(CreatePresetStat(AIStatNames.MOVEMENT_SMOOTHING, 0.9f, 0.1f, 1.0f, "Movement smoothing factor"));
        preset.stats.Add(CreatePresetStat(AIStatNames.SERVE_DELAY_MIN, 0.6f, 0.5f, 5.0f, "Minimum serve delay"));
        preset.stats.Add(CreatePresetStat(AIStatNames.SERVE_DELAY_MAX, 1.0f, 1.0f, 10.0f, "Maximum serve delay"));
        preset.stats.Add(CreatePresetStat(AIStatNames.REACHABLE_DISTANCE, 5.0f, 1.0f, 10.0f, "Max chase distance"));
        preset.stats.Add(CreatePresetStat(AIStatNames.CONSISTENCY, 0.9f, 0.0f, 1.0f, "How consistent AI performance is"));

        return preset;
    }

    public static StatPreset CreateAdaptivePreset()
    {
        var preset = ScriptableObject.CreateInstance<StatPreset>();
        preset.presetName = "Adaptive AI";
        preset.description = "AI that learns and adapts to player behavior";
        preset.category = "Advanced";
        preset.difficulty = 4;
        preset.presetColor = Color.cyan;

        // Start with medium baseline
        var medium = CreateMediumPreset();
        preset.stats.AddRange(medium.stats);

        // Add adaptive features
        preset.stats.Add(CreatePresetStat(AIStatNames.ADAPTIVE_DIFFICULTY, 1.0f, 0.0f, 1.0f, "Enable adaptive difficulty"));
        preset.stats.Add(CreatePresetStat(AIStatNames.LEARNING_RATE, 0.1f, 0.0f, 1.0f, "How quickly AI adapts"));
        preset.stats.Add(CreatePresetStat(AIStatNames.OPPONENT_PREDICTION_ACCURACY, 0.6f, 0.0f, 1.0f, "Player behavior prediction"));
        preset.stats.Add(CreatePresetStat(AIStatNames.PATTERN_RECOGNITION, 0.7f, 0.0f, 1.0f, "Pattern detection ability"));

        return preset;
    }

    public static StatPreset CreateAggressivePreset()
    {
        var preset = ScriptableObject.CreateInstance<StatPreset>();
        preset.presetName = "Aggressive AI";
        preset.description = "Highly aggressive AI that takes risks for powerful shots";
        preset.category = "Playstyle";
        preset.difficulty = 4;
        preset.presetColor = Color.magenta;

        // Start with hard baseline but modify for aggression
        var hard = CreateHardPreset();
        preset.stats.AddRange(hard.stats);

        // Modify for aggressive playstyle
        preset.SetStatValue(AIStatNames.AGGRESSION_LEVEL, 0.9f);
        preset.SetStatValue(AIStatNames.RISK_TAKING, 0.8f);
        preset.SetStatValue(AIStatNames.REACHABLE_DISTANCE, 6.0f); // Chase more balls
        preset.SetStatValue(AIStatNames.MOVEMENT_SPEED_MULTIPLIER, 1.3f); // Move faster
        preset.SetStatValue(AIStatNames.CONSISTENCY, 0.6f); // Less consistent due to risk-taking

        preset.stats.Add(CreatePresetStat(AIStatNames.AGGRESSION_LEVEL, 0.9f, 0.0f, 1.0f, "How aggressive AI playstyle is"));
        preset.stats.Add(CreatePresetStat(AIStatNames.RISK_TAKING, 0.8f, 0.0f, 1.0f, "Willingness to take risky shots"));

        return preset;
    }

    public static StatPreset CreateDefensivePreset()
    {
        var preset = ScriptableObject.CreateInstance<StatPreset>();
        preset.presetName = "Defensive AI";
        preset.description = "Patient, defensive AI focused on consistency";
        preset.category = "Playstyle";
        preset.difficulty = 3;
        preset.presetColor = Color.blue;

        // Start with medium baseline
        var medium = CreateMediumPreset();
        preset.stats.AddRange(medium.stats);

        // Modify for defensive playstyle
        preset.SetStatValue(AIStatNames.CONSISTENCY, 0.95f); // Very consistent
        preset.SetStatValue(AIStatNames.POSITION_NOISE, 0.1f); // Very precise positioning
        preset.SetStatValue(AIStatNames.AGGRESSION_LEVEL, 0.2f); // Low aggression
        preset.SetStatValue(AIStatNames.RISK_TAKING, 0.1f); // Very safe
        preset.SetStatValue(AIStatNames.DEFENSIVE_POSITION_OFFSET, 2.0f); // Stay back more

        preset.stats.Add(CreatePresetStat(AIStatNames.AGGRESSION_LEVEL, 0.2f, 0.0f, 1.0f, "How aggressive AI playstyle is"));
        preset.stats.Add(CreatePresetStat(AIStatNames.RISK_TAKING, 0.1f, 0.0f, 1.0f, "Willingness to take risky shots"));
        preset.stats.Add(CreatePresetStat(AIStatNames.DEFENSIVE_POSITION_OFFSET, 2.0f, 0.5f, 5.0f, "How far back to position defensively"));

        return preset;
    }

    private static StatPreset.PresetStat CreatePresetStat(string name, float value, float min, float max, string description)
    {
        return new StatPreset.PresetStat
        {
            name = name,
            value = value,
            minValue = min,
            maxValue = max,
            description = description
        };
    }

    // Helper to get preset by difficulty
    public static StatPreset GetPresetByDifficulty(CPUDifficulty difficulty)
    {
        return difficulty switch
        {
            CPUDifficulty.Easy => CreateEasyPreset(),
            CPUDifficulty.Medium => CreateMediumPreset(),
            CPUDifficulty.Hard => CreateHardPreset(),
            _ => CreateMediumPreset()
        };
    }
}