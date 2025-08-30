using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StatPreset", menuName = "PingPong/Stat Preset", order = 1)]
public class StatPreset : ScriptableObject
{
    [System.Serializable]
    public struct PresetStat
    {
        public string name;
        public float value;
        public float minValue;
        public float maxValue;
        [TextArea(1, 3)]
        public string description;
    }

    [Header("Preset Information")]
    public string presetName;
    [TextArea(2, 4)]
    public string description;

    [Header("Stats")]
    public List<PresetStat> stats = new List<PresetStat>();

    [Header("Metadata")]
    public string category = "General";
    public int difficulty = 1; // 1-5 scale
    public Color presetColor = Color.white;

    // Helper methods
    public float GetStatValue(string statName, float defaultValue = 0f)
    {
        foreach (var stat in stats)
        {
            if (stat.name == statName)
                return stat.value;
        }
        return defaultValue;
    }

    public bool HasStat(string statName)
    {
        foreach (var stat in stats)
        {
            if (stat.name == statName)
                return true;
        }
        return false;
    }

    public void SetStatValue(string statName, float value)
    {
        for (int i = 0; i < stats.Count; i++)
        {
            if (stats[i].name == statName)
            {
                var stat = stats[i];
                stat.value = value;
                stats[i] = stat;
                return;
            }
        }
        
        // Add new stat if it doesn't exist
        stats.Add(new PresetStat 
        { 
            name = statName, 
            value = value,
            minValue = float.MinValue,
            maxValue = float.MaxValue
        });
    }

    public StatPreset Clone()
    {
        var clone = CreateInstance<StatPreset>();
        clone.presetName = presetName + " (Clone)";
        clone.description = description;
        clone.stats = new List<PresetStat>(stats);
        clone.category = category;
        clone.difficulty = difficulty;
        clone.presetColor = presetColor;
        return clone;
    }

#if UNITY_EDITOR
    [ContextMenu("Validate Stat Names")]
    private void ValidateStatNames()
    {
        var uniqueNames = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var stat in stats)
        {
            if (!uniqueNames.Add(stat.name))
            {
                duplicates.Add(stat.name);
            }
        }

        if (duplicates.Count > 0)
        {
            Debug.LogWarning($"Duplicate stat names found in {name}: {string.Join(", ", duplicates)}");
        }
        else
        {
            Debug.Log($"All stat names in {name} are unique.");
        }
    }

    [ContextMenu("Log All Stats")]
    private void LogAllStats()
    {
        Debug.Log($"=== Stats in {presetName} ===");
        foreach (var stat in stats)
        {
            Debug.Log($"{stat.name}: {stat.value} (range: {stat.minValue} to {stat.maxValue})");
        }
    }
#endif
}