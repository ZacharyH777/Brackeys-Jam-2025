using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RuntimeAIStatModifier : MonoBehaviour
{
    [Header("Target AI")]
    [Tooltip("CPU Player to modify stats for")]
    public CPUPlayer targetAI;
    [Tooltip("Auto-find CPU player if not assigned")]
    public bool autoFindCPUPlayer = true;

    [Header("UI Controls")]
    [Tooltip("Dropdown for selecting stat to modify")]
    public TMP_Dropdown statDropdown;
    [Tooltip("Slider for adjusting stat value")]
    public Slider statSlider;
    [Tooltip("Input field for precise value entry")]
    public TMP_InputField statInputField;
    [Tooltip("Text showing current stat value")]
    public TextMeshProUGUI currentValueText;
    [Tooltip("Button to reset stat to default")]
    public Button resetButton;
    [Tooltip("Button to apply preset")]
    public Button applyPresetButton;
    [Tooltip("Dropdown for preset selection")]
    public TMP_Dropdown presetDropdown;

    [Header("Modification Settings")]
    [Tooltip("Create temporary modifiers instead of changing base values")]
    public bool useTemporaryModifiers = true;
    [Tooltip("Source object for tracking modifiers")]
    public object modifierSource;

    private string currentStatName;
    private StatModifier currentModifier;

    // Available stats for modification
    private readonly string[] availableStats = {
        AIStatNames.REACTION_TIME,
        AIStatNames.REACTION_VARIATION,
        AIStatNames.PREDICTION_TIME,
        AIStatNames.MOVEMENT_ACCURACY,
        AIStatNames.POSITION_NOISE,
        AIStatNames.MOVEMENT_SPEED_MULTIPLIER,
        AIStatNames.MOVEMENT_SMOOTHING,
        AIStatNames.SERVE_DELAY_MIN,
        AIStatNames.SERVE_DELAY_MAX,
        AIStatNames.REACHABLE_DISTANCE,
        AIStatNames.CONSISTENCY
    };

    void Start()
    {
        if (modifierSource == null)
            modifierSource = this;

        InitializeTargetAI();
        InitializeUI();
    }

    private void InitializeTargetAI()
    {
        if (targetAI == null && autoFindCPUPlayer)
        {
            targetAI = FindFirstObjectByType<CPUPlayer>();
            if (targetAI == null)
            {
                Debug.LogWarning("RuntimeAIStatModifier: No CPU Player found in scene");
                return;
            }
        }

        if (targetAI == null)
        {
            Debug.LogError("RuntimeAIStatModifier: No target AI assigned");
            enabled = false;
        }
    }

    private void InitializeUI()
    {
        // Setup stat dropdown
        if (statDropdown != null)
        {
            statDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            
            foreach (string stat in availableStats)
            {
                if (targetAI.GetStatSystem()?.HasStat(stat) == true)
                {
                    string displayName = FormatStatName(stat);
                    options.Add(displayName);
                }
            }
            
            statDropdown.AddOptions(options);
            statDropdown.onValueChanged.AddListener(OnStatSelected);
            
            if (options.Count > 0)
            {
                OnStatSelected(0); // Initialize with first stat
            }
        }

        // Setup preset dropdown
        if (presetDropdown != null)
        {
            presetDropdown.ClearOptions();
            presetDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Easy AI",
                "Medium AI", 
                "Hard AI",
                "Adaptive AI",
                "Aggressive AI",
                "Defensive AI"
            });
        }

        // Setup slider
        if (statSlider != null)
        {
            statSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        // Setup input field
        if (statInputField != null)
        {
            statInputField.onEndEdit.AddListener(OnInputFieldChanged);
        }

        // Setup buttons
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetCurrentStat);
        }

        if (applyPresetButton != null)
        {
            applyPresetButton.onClick.AddListener(ApplySelectedPreset);
        }
    }

    private void OnStatSelected(int index)
    {
        if (index < 0 || index >= availableStats.Length) return;
        if (targetAI == null) return;

        string selectedStat = availableStats[index];
        var statSystem = targetAI.GetStatSystem();
        
        if (statSystem?.HasStat(selectedStat) != true) return;

        currentStatName = selectedStat;
        var stat = statSystem.GetStat(selectedStat);

        // Update slider range and current value
        if (statSlider != null)
        {
            statSlider.minValue = stat.MinValue == float.MinValue ? 0f : stat.MinValue;
            statSlider.maxValue = stat.MaxValue == float.MaxValue ? 2f : stat.MaxValue;
            statSlider.value = stat.CurrentValue;
        }

        // Update input field
        if (statInputField != null)
        {
            statInputField.text = stat.CurrentValue.ToString("F3");
        }

        UpdateCurrentValueDisplay();
    }

    private void OnSliderChanged(float value)
    {
        if (targetAI == null || string.IsNullOrEmpty(currentStatName)) return;

        ApplyStatChange(value);
        
        // Update input field to match slider
        if (statInputField != null)
        {
            statInputField.text = value.ToString("F3");
        }
    }

    private void OnInputFieldChanged(string valueStr)
    {
        if (targetAI == null || string.IsNullOrEmpty(currentStatName)) return;

        if (float.TryParse(valueStr, out float value))
        {
            ApplyStatChange(value);
            
            // Update slider to match input field
            if (statSlider != null)
            {
                statSlider.value = value;
            }
        }
    }

    private void ApplyStatChange(float newValue)
    {
        if (targetAI == null || string.IsNullOrEmpty(currentStatName)) return;

        var statSystem = targetAI.GetStatSystem();
        if (statSystem == null) return;

        if (useTemporaryModifiers)
        {
            // Remove existing modifier
            if (currentModifier != null)
            {
                statSystem.RemoveModifier(currentStatName, currentModifier);
            }

            // Create new modifier as override
            var baseStat = statSystem.GetStat(currentStatName);
            float difference = newValue - baseStat.BaseValue;
            
            if (!Mathf.Approximately(difference, 0f))
            {
                currentModifier = statSystem.AddModifier(currentStatName, difference, StatModifierType.FlatAddition, 1000, modifierSource);
            }
        }
        else
        {
            // Change base value directly
            statSystem.SetStatBaseValue(currentStatName, newValue);
        }

        UpdateCurrentValueDisplay();
    }

    private void ResetCurrentStat()
    {
        if (targetAI == null || string.IsNullOrEmpty(currentStatName)) return;

        var statSystem = targetAI.GetStatSystem();
        if (statSystem == null) return;

        if (useTemporaryModifiers && currentModifier != null)
        {
            statSystem.RemoveModifier(currentStatName, currentModifier);
            currentModifier = null;
        }

        var stat = statSystem.GetStat(currentStatName);
        if (stat != null)
        {
            if (statSlider != null)
                statSlider.value = stat.BaseValue;
            if (statInputField != null)
                statInputField.text = stat.BaseValue.ToString("F3");
        }

        UpdateCurrentValueDisplay();
    }

    private void ApplySelectedPreset()
    {
        if (targetAI == null || presetDropdown == null) return;

        StatPreset preset = presetDropdown.value switch
        {
            0 => AIStatPresets.CreateEasyPreset(),
            1 => AIStatPresets.CreateMediumPreset(),
            2 => AIStatPresets.CreateHardPreset(),
            3 => AIStatPresets.CreateAdaptivePreset(),
            4 => AIStatPresets.CreateAggressivePreset(),
            5 => AIStatPresets.CreateDefensivePreset(),
            _ => null
        };

        if (preset != null)
        {
            targetAI.ApplyCustomPreset(preset);
            
            // Refresh UI to show new values
            OnStatSelected(statDropdown.value);
        }
    }

    private void UpdateCurrentValueDisplay()
    {
        if (targetAI == null || string.IsNullOrEmpty(currentStatName)) return;
        if (currentValueText == null) return;

        float currentValue = targetAI.GetStatValue(currentStatName);
        currentValueText.text = $"Current: {currentValue:F3}";
    }

    private string FormatStatName(string statName)
    {
        return statName switch
        {
            AIStatNames.REACTION_TIME => "Reaction Time",
            AIStatNames.REACTION_VARIATION => "Reaction Variation",
            AIStatNames.PREDICTION_TIME => "Prediction Time",
            AIStatNames.MOVEMENT_ACCURACY => "Movement Accuracy",
            AIStatNames.POSITION_NOISE => "Position Noise",
            AIStatNames.MOVEMENT_SPEED_MULTIPLIER => "Movement Speed",
            AIStatNames.MOVEMENT_SMOOTHING => "Movement Smoothing",
            AIStatNames.SERVE_DELAY_MIN => "Min Serve Delay",
            AIStatNames.SERVE_DELAY_MAX => "Max Serve Delay",
            AIStatNames.REACHABLE_DISTANCE => "Chase Distance",
            AIStatNames.CONSISTENCY => "Consistency",
            _ => statName
        };
    }

    // Public API for external systems
    public void SetTargetAI(CPUPlayer ai)
    {
        targetAI = ai;
        InitializeUI();
    }

    public void ModifyStatTemporarily(string statName, float value, float duration = -1f)
    {
        if (targetAI?.GetStatSystem() == null) return;

        var modifier = targetAI.AddStatModifier(statName, value, StatModifierType.FlatAddition, 500, this);
        
        if (duration > 0f && modifier != null)
        {
            StartCoroutine(RemoveModifierAfterDelay(statName, modifier, duration));
        }
    }

    private System.Collections.IEnumerator RemoveModifierAfterDelay(string statName, StatModifier modifier, float delay)
    {
        yield return new WaitForSeconds(delay);
        targetAI?.RemoveStatModifier(statName, modifier);
    }

    void OnDestroy()
    {
        // Clean up UI listeners
        if (statDropdown != null)
            statDropdown.onValueChanged.RemoveListener(OnStatSelected);
        if (statSlider != null)
            statSlider.onValueChanged.RemoveListener(OnSliderChanged);
        if (statInputField != null)
            statInputField.onEndEdit.RemoveListener(OnInputFieldChanged);
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetCurrentStat);
        if (applyPresetButton != null)
            applyPresetButton.onClick.RemoveListener(ApplySelectedPreset);
    }
}