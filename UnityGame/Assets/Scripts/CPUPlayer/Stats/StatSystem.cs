using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Stat
{
    [SerializeField] private string statName;
    [SerializeField] private float baseValue;
    [SerializeField] private float minValue = float.MinValue;
    [SerializeField] private float maxValue = float.MaxValue;

    private float currentValue;
    private readonly List<StatModifier> modifiers = new List<StatModifier>();

    public string StatName => statName;
    public float BaseValue => baseValue;
    public float CurrentValue => currentValue;
    public float MinValue => minValue;
    public float MaxValue => maxValue;

    // Events
    public event Action<float, float> OnValueChanged; // oldValue, newValue

    public Stat(string name, float baseValue, float minValue = float.MinValue, float maxValue = float.MaxValue)
    {
        this.statName = name;
        this.baseValue = baseValue;
        this.minValue = minValue;
        this.maxValue = maxValue;
        this.currentValue = baseValue;
    }

    public void SetBaseValue(float value)
    {
        float oldValue = currentValue;
        baseValue = Mathf.Clamp(value, minValue, maxValue);
        RecalculateValue();
        
        if (!Mathf.Approximately(oldValue, currentValue))
        {
            OnValueChanged?.Invoke(oldValue, currentValue);
        }
    }

    public void AddModifier(StatModifier modifier)
    {
        if (modifier == null) return;
        
        float oldValue = currentValue;
        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Order.CompareTo(b.Order));
        RecalculateValue();
        
        if (!Mathf.Approximately(oldValue, currentValue))
        {
            OnValueChanged?.Invoke(oldValue, currentValue);
        }
    }

    public bool RemoveModifier(StatModifier modifier)
    {
        if (modifier == null) return false;
        
        float oldValue = currentValue;
        bool removed = modifiers.Remove(modifier);
        
        if (removed)
        {
            RecalculateValue();
            if (!Mathf.Approximately(oldValue, currentValue))
            {
                OnValueChanged?.Invoke(oldValue, currentValue);
            }
        }
        
        return removed;
    }

    public void RemoveAllModifiers()
    {
        float oldValue = currentValue;
        modifiers.Clear();
        RecalculateValue();
        
        if (!Mathf.Approximately(oldValue, currentValue))
        {
            OnValueChanged?.Invoke(oldValue, currentValue);
        }
    }

    private void RecalculateValue()
    {
        float value = baseValue;

        // Apply percentage modifiers first
        foreach (var mod in modifiers)
        {
            if (mod.Type == StatModifierType.PercentageMultiplier)
            {
                value *= (1f + mod.Value);
            }
        }

        // Then apply flat modifiers
        foreach (var mod in modifiers)
        {
            if (mod.Type == StatModifierType.FlatAddition)
            {
                value += mod.Value;
            }
        }

        // Finally apply override modifiers (highest priority)
        foreach (var mod in modifiers)
        {
            if (mod.Type == StatModifierType.Override)
            {
                value = mod.Value;
                break; // Override takes precedence, stop processing
            }
        }

        currentValue = Mathf.Clamp(value, minValue, maxValue);
    }

    public StatModifier CreateModifier(float value, StatModifierType type, int order = 0, object source = null)
    {
        return new StatModifier(value, type, order, source);
    }
}

public enum StatModifierType
{
    FlatAddition,        // +5, -3, etc.
    PercentageMultiplier, // +50% = 0.5, -25% = -0.25
    Override             // Set to exact value, ignores other modifiers
}

[Serializable]
public class StatModifier
{
    [SerializeField] private float value;
    [SerializeField] private StatModifierType type;
    [SerializeField] private int order;
    [SerializeField] private object source; // Can track what applied this modifier

    public float Value => value;
    public StatModifierType Type => type;
    public int Order => order;
    public object Source => source;

    public StatModifier(float value, StatModifierType type, int order = 0, object source = null)
    {
        this.value = value;
        this.type = type;
        this.order = order;
        this.source = source;
    }

    public override string ToString()
    {
        string typeStr = Type switch
        {
            StatModifierType.FlatAddition => value >= 0 ? $"+{value}" : value.ToString(),
            StatModifierType.PercentageMultiplier => $"{(value >= 0 ? "+" : "")}{value * 100:F1}%",
            StatModifierType.Override => $"={value}",
            _ => value.ToString()
        };
        
        return $"{typeStr} (Order: {order})";
    }
}

[DisallowMultipleComponent]
public class StatSystem : MonoBehaviour
{
    [SerializeField] private List<StatData> initialStats = new List<StatData>();
    
    private readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
    
    // Events
    public event Action<string, float, float> OnStatChanged; // statName, oldValue, newValue

    [Serializable]
    public struct StatData
    {
        public string name;
        public float baseValue;
        public float minValue;
        public float maxValue;
        [Tooltip("Optional description for editor clarity")]
        public string description;
    }

    void Awake()
    {
        InitializeStats();
    }

    private void InitializeStats()
    {
        foreach (var statData in initialStats)
        {
            if (string.IsNullOrEmpty(statData.name)) continue;
            
            var stat = new Stat(statData.name, statData.baseValue, statData.minValue, statData.maxValue);
            stat.OnValueChanged += (oldValue, newValue) => OnStatChanged?.Invoke(stat.StatName, oldValue, newValue);
            stats[statData.name] = stat;
        }
    }

    // Core stat operations
    public bool HasStat(string statName)
    {
        return stats.ContainsKey(statName);
    }

    public Stat GetStat(string statName)
    {
        return stats.TryGetValue(statName, out var stat) ? stat : null;
    }

    public float GetStatValue(string statName)
    {
        return stats.TryGetValue(statName, out var stat) ? stat.CurrentValue : 0f;
    }

    public bool SetStatBaseValue(string statName, float value)
    {
        if (stats.TryGetValue(statName, out var stat))
        {
            stat.SetBaseValue(value);
            return true;
        }
        return false;
    }

    // Dynamic stat creation
    public Stat AddStat(string name, float baseValue, float minValue = float.MinValue, float maxValue = float.MaxValue)
    {
        if (stats.ContainsKey(name))
        {
            Debug.LogWarning($"Stat '{name}' already exists. Use GetStat() to modify existing stats.");
            return stats[name];
        }

        var stat = new Stat(name, baseValue, minValue, maxValue);
        stat.OnValueChanged += (oldValue, newValue) => OnStatChanged?.Invoke(name, oldValue, newValue);
        stats[name] = stat;
        return stat;
    }

    public bool RemoveStat(string name)
    {
        return stats.Remove(name);
    }

    // Modifier operations
    public bool AddModifier(string statName, StatModifier modifier)
    {
        if (stats.TryGetValue(statName, out var stat))
        {
            stat.AddModifier(modifier);
            return true;
        }
        return false;
    }

    public StatModifier AddModifier(string statName, float value, StatModifierType type, int order = 0, object source = null)
    {
        if (stats.TryGetValue(statName, out var stat))
        {
            var modifier = new StatModifier(value, type, order, source);
            stat.AddModifier(modifier);
            return modifier;
        }
        return null;
    }

    public bool RemoveModifier(string statName, StatModifier modifier)
    {
        if (stats.TryGetValue(statName, out var stat))
        {
            return stat.RemoveModifier(modifier);
        }
        return false;
    }

    public void RemoveAllModifiers(string statName)
    {
        stats[statName]?.RemoveAllModifiers();
    }

    public void RemoveAllModifiersFromSource(object source)
    {
        foreach (var stat in stats.Values)
        {
            var modifiersToRemove = new List<StatModifier>();
            
            // We can't directly access modifiers, so we'll need to track them externally
            // For now, this is a placeholder for more advanced source tracking
        }
    }

    // Utility methods
    public IEnumerable<string> GetAllStatNames()
    {
        return stats.Keys;
    }

    public Dictionary<string, float> GetAllStatValues()
    {
        var values = new Dictionary<string, float>();
        foreach (var kvp in stats)
        {
            values[kvp.Key] = kvp.Value.CurrentValue;
        }
        return values;
    }

    // Preset operations for easy configuration
    public void ApplyStatPreset(StatPreset preset)
    {
        if (preset == null) return;

        foreach (var presetStat in preset.stats)
        {
            if (!HasStat(presetStat.name))
            {
                AddStat(presetStat.name, presetStat.value, presetStat.minValue, presetStat.maxValue);
            }
            else
            {
                SetStatBaseValue(presetStat.name, presetStat.value);
            }
        }
    }

    // Debug helpers
    public void LogAllStats()
    {
        Debug.Log($"=== Stats for {gameObject.name} ===");
        foreach (var kvp in stats)
        {
            var stat = kvp.Value;
            Debug.Log($"{stat.StatName}: {stat.CurrentValue} (base: {stat.BaseValue})");
        }
    }

    // Save/Load support (for future implementation)
    public string SerializeStats()
    {
        var data = new Dictionary<string, float>();
        foreach (var kvp in stats)
        {
            data[kvp.Key] = kvp.Value.BaseValue;
        }
        return JsonUtility.ToJson(data);
    }

    public void DeserializeStats(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<Dictionary<string, float>>(json);
            foreach (var kvp in data)
            {
                SetStatBaseValue(kvp.Key, kvp.Value);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to deserialize stats: {e.Message}");
        }
    }
}