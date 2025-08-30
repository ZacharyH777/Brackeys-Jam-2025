using UnityEngine;

/// <summary>
/// Simple debugging and testing tool for CPU players.
/// Shows real-time CPU behavior and allows quick testing.
/// </summary>
public class CPUDebugger : MonoBehaviour
{
    [Header("🔍 Debug Settings")]
    [Tooltip("Show CPU decision making in real-time")]
    public bool showCPUDecisions = true;
    
    [Tooltip("Show CPU stats and values")]
    public bool showCPUStats = true;
    
    [Tooltip("Show ball prediction visualization")]
    public bool showBallPrediction = true;
    
    [Header("🎯 Target CPU")]
    [Tooltip("CPU player to debug (auto-finds if null)")]
    public CPUPlayer targetCPU;
    
    [Header("📊 Real-time Info")]
    [SerializeField] private string cpuState = "Not Found";
    [SerializeField] private string currentDecision = "None";
    [SerializeField] private float reactionTime = 0f;
    [SerializeField] private float movementAccuracy = 0f;
    [SerializeField] private float predictionTime = 0f;

    private bool debugUIEnabled = false;

    void Start()
    {
        // Auto-find CPU player if not assigned
        if (targetCPU == null)
        {
            targetCPU = FindFirstObjectByType<CPUPlayer>();
        }
        
        if (targetCPU == null)
        {
            Debug.LogWarning("CPUDebugger: No CPU player found in scene");
            cpuState = "No CPU Found";
        }
        else
        {
            cpuState = "CPU Found";
        }
    }

    void Update()
    {
        if (targetCPU != null)
        {
            UpdateDebugInfo();
        }
        
        // Toggle debug UI with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            debugUIEnabled = !debugUIEnabled;
        }
    }

    private void UpdateDebugInfo()
    {
        // Update CPU state info
        cpuState = targetCPU.IsCPUPlayer ? "Active CPU" : "Human Player";
        currentDecision = GetCPUDecision();
        
        // Update stats
        reactionTime = targetCPU.GetStatValue(AIStatNames.REACTION_TIME);
        movementAccuracy = targetCPU.GetStatValue(AIStatNames.MOVEMENT_ACCURACY);
        predictionTime = targetCPU.GetStatValue(AIStatNames.PREDICTION_TIME);
    }

    private string GetCPUDecision()
    {
        if (!targetCPU.IsCPUPlayer) return "Human Controlled";
        
        // Check if CPU is waiting to serve using reflection (since is_serving_pending is private)
        var servingField = targetCPU.GetType().GetField("is_serving_pending", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (servingField != null)
        {
            bool isServingPending = (bool)servingField.GetValue(targetCPU);
            if (isServingPending)
            {
                return "Preparing to Serve";
            }
        }
        
        // Get CPU movement component
        var cpuMovement = targetCPU.GetComponent<CPUMovement>();
        if (cpuMovement == null) return "No CPU Movement";
        
        return "Tracking Ball";
    }

    void OnGUI()
    {
        if (!debugUIEnabled || targetCPU == null) return;
        
        // Create a debug panel
        GUI.Box(new Rect(10, 10, 300, 200), "CPU Player Debug");
        
        int y = 35;
        GUI.Label(new Rect(20, y, 280, 20), $"CPU State: {cpuState}");
        y += 25;
        
        GUI.Label(new Rect(20, y, 280, 20), $"Decision: {currentDecision}");
        y += 25;
        
        GUI.Label(new Rect(20, y, 280, 20), $"Reaction Time: {reactionTime:F2}s");
        y += 20;
        
        GUI.Label(new Rect(20, y, 280, 20), $"Accuracy: {movementAccuracy:F1}%");
        y += 20;
        
        GUI.Label(new Rect(20, y, 280, 20), $"Prediction: {predictionTime:F2}s");
        y += 25;
        
        if (GUI.Button(new Rect(20, y, 120, 25), "Change Difficulty"))
        {
            CycleDifficulty();
        }
        
        if (GUI.Button(new Rect(150, y, 120, 25), "Reset CPU"))
        {
            ResetCPU();
        }
        
        // Instructions
        GUI.Label(new Rect(10, 220, 300, 40), "Press F1 to toggle this debug panel");
    }

    /// <summary>
    /// Cycle through CPU difficulties for testing
    /// </summary>
    [ContextMenu("🔄 Cycle CPU Difficulty")]
    public void CycleDifficulty()
    {
        if (targetCPU == null) return;
        
        var currentDifficulty = targetCPU.difficulty;
        CPUDifficulty newDifficulty = currentDifficulty switch
        {
            CPUDifficulty.Easy => CPUDifficulty.Medium,
            CPUDifficulty.Medium => CPUDifficulty.Hard,
            CPUDifficulty.Hard => CPUDifficulty.Easy,
            _ => CPUDifficulty.Medium
        };
        
        targetCPU.SetDifficulty(newDifficulty);
        Debug.Log($"CPU difficulty changed: {currentDifficulty} → {newDifficulty}");
    }

    /// <summary>
    /// Apply a random preset for testing
    /// </summary>
    [ContextMenu("🎲 Apply Random Preset")]
    public void ApplyRandomPreset()
    {
        if (targetCPU == null) return;
        
        var presets = new StatPreset[]
        {
            AIStatPresets.CreateEasyPreset(),
            AIStatPresets.CreateMediumPreset(),
            AIStatPresets.CreateHardPreset(),
            AIStatPresets.CreateAggressivePreset(),
            AIStatPresets.CreateDefensivePreset()
        };
        
        var randomPreset = presets[Random.Range(0, presets.Length)];
        targetCPU.ApplyCustomPreset(randomPreset);
        
        Debug.Log($"Applied random preset: {randomPreset.presetName}");
    }

    /// <summary>
    /// Reset CPU to default settings
    /// </summary>
    [ContextMenu("🔄 Reset CPU")]
    public void ResetCPU()
    {
        if (targetCPU == null) return;
        
        targetCPU.SetDifficulty(CPUDifficulty.Medium);
        Debug.Log("CPU reset to medium difficulty");
    }

    /// <summary>
    /// Test CPU with extreme settings
    /// </summary>
    [ContextMenu("🚀 Test Extreme CPU")]
    public void TestExtremeCPU()
    {
        if (targetCPU == null) return;
        
        // Create super-fast CPU for testing
        var extremePreset = new StatPreset
        {
            presetName = "Extreme Test",
            description = "For testing only - impossibly fast reactions"
        };
        
        extremePreset.stats.Add(new StatPreset.PresetStat
        {
            name = AIStatNames.REACTION_TIME,
            value = 0.01f // Almost instant reactions
        });
        
        extremePreset.stats.Add(new StatPreset.PresetStat
        {
            name = AIStatNames.MOVEMENT_ACCURACY,
            value = 1.0f // Perfect accuracy
        });
        
        targetCPU.ApplyCustomPreset(extremePreset);
        Debug.Log("Applied extreme CPU settings for testing");
    }

    /// <summary>
    /// Force CPU to make a specific decision (for testing)
    /// </summary>
    [ContextMenu("🎯 Test CPU Movement")]
    public void TestCPUMovement()
    {
        if (targetCPU == null) return;
        
        var cpuMovement = targetCPU.GetComponent<CPUMovement>();
        if (cpuMovement != null)
        {
            Debug.Log("Testing CPU movement response...");
            // This would trigger a test movement
        }
    }

    /// <summary>
    /// Log detailed CPU state information
    /// </summary>
    [ContextMenu("📊 Log CPU State")]
    public void LogCPUState()
    {
        if (targetCPU == null)
        {
            Debug.Log("No CPU player assigned");
            return;
        }
        
        Debug.Log("=== CPU DEBUG INFO ===");
        Debug.Log($"CPU Active: {targetCPU.IsCPUPlayer}");
        Debug.Log($"Difficulty: {targetCPU.difficulty}");
        
        var statSystem = targetCPU.GetStatSystem();
        if (statSystem != null)
        {
            Debug.Log("CPU Stats:");
            foreach (string statName in new[] { 
                AIStatNames.REACTION_TIME,
                AIStatNames.MOVEMENT_ACCURACY,
                AIStatNames.PREDICTION_TIME,
                AIStatNames.MOVEMENT_SPEED_MULTIPLIER
            })
            {
                if (statSystem.HasStat(statName))
                {
                    float value = targetCPU.GetStatValue(statName);
                    Debug.Log($"  {statName}: {value}");
                }
            }
        }
        
        Debug.Log("======================");
    }
}