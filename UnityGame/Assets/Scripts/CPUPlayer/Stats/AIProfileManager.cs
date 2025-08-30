using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AIProfile
{
    public string profileName;
    public string description;
    public StatPreset statPreset;
    public Sprite profileIcon;
    public Color profileColor = Color.white;
    public int difficultyRating = 1; // 1-5 scale
    public string playstyle = "Balanced";

    [Header("Profile Metadata")]
    public bool isUnlocked = true;
    public int requiredWins = 0;
    public string unlockCondition = "";

    public AIProfile(string name, string desc, StatPreset preset)
    {
        profileName = name;
        description = desc;
        statPreset = preset;
    }
}

[CreateAssetMenu(fileName = "AIProfileDatabase", menuName = "PingPong/AI Profile Database", order = 2)]
public class AIProfileDatabase : ScriptableObject
{
    [Header("Available Profiles")]
    public List<AIProfile> profiles = new List<AIProfile>();

    [Header("Default Settings")]
    public AIProfile defaultProfile;
    public bool allowCustomProfiles = true;

    public AIProfile GetProfile(string profileName)
    {
        return profiles.Find(p => p.profileName == profileName);
    }

    public List<AIProfile> GetUnlockedProfiles()
    {
        return profiles.FindAll(p => p.isUnlocked);
    }

    public List<AIProfile> GetProfilesByDifficulty(int difficulty)
    {
        return profiles.FindAll(p => p.difficultyRating == difficulty);
    }

    public AIProfile GetRandomProfile(bool unlockedOnly = true)
    {
        var availableProfiles = unlockedOnly ? GetUnlockedProfiles() : profiles;
        if (availableProfiles.Count == 0) return defaultProfile;
        
        int randomIndex = Random.Range(0, availableProfiles.Count);
        return availableProfiles[randomIndex];
    }

#if UNITY_EDITOR
    [ContextMenu("Initialize Default Profiles")]
    public void InitializeDefaultProfiles()
    {
        profiles.Clear();

        // Easy Profiles
        profiles.Add(new AIProfile("Beginner Bot", "Perfect for learning the basics", AIStatPresets.CreateEasyPreset())
        {
            difficultyRating = 1,
            playstyle = "Forgiving",
            profileColor = Color.green
        });

        // Medium Profiles  
        profiles.Add(new AIProfile("Balanced Player", "Even match for most players", AIStatPresets.CreateMediumPreset())
        {
            difficultyRating = 3,
            playstyle = "Balanced",
            profileColor = Color.yellow
        });

        // Hard Profiles
        profiles.Add(new AIProfile("Pro Champion", "Serious challenge ahead", AIStatPresets.CreateHardPreset())
        {
            difficultyRating = 5,
            playstyle = "Competitive",
            profileColor = Color.red
        });

        // Specialty Profiles
        profiles.Add(new AIProfile("The Wall", "Defensive specialist", AIStatPresets.CreateDefensivePreset())
        {
            difficultyRating = 4,
            playstyle = "Defensive",
            profileColor = Color.blue
        });

        profiles.Add(new AIProfile("Power Hitter", "Aggressive attacker", AIStatPresets.CreateAggressivePreset())
        {
            difficultyRating = 4,
            playstyle = "Aggressive",
            profileColor = Color.magenta
        });

        profiles.Add(new AIProfile("Learning AI", "Adapts to your playstyle", AIStatPresets.CreateAdaptivePreset())
        {
            difficultyRating = 4,
            playstyle = "Adaptive",
            profileColor = Color.cyan,
            requiredWins = 5,
            unlockCondition = "Win 5 matches against any AI"
        });

        if (profiles.Count > 0)
            defaultProfile = profiles[0];

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"Initialized {profiles.Count} default AI profiles");
    }
#endif
}

public class AIProfileManager : MonoBehaviour
{
    [Header("Profile Database")]
    public AIProfileDatabase profileDatabase;
    
    [Header("Current Profile")]
    public AIProfile currentProfile;
    
    [Header("Profile Switching")]
    public bool allowRuntimeProfileSwitching = true;
    public bool saveProfileSelection = true;
    
    [Header("Unlocking System")]
    public bool enableUnlockSystem = true;
    
    private CPUPlayer targetAI;
    private const string PROFILE_SAVE_KEY = "SelectedAIProfile";
    private const string UNLOCK_DATA_KEY = "UnlockedProfiles";

    // Events
    public System.Action<AIProfile> OnProfileChanged;
    public System.Action<AIProfile> OnProfileUnlocked;

    void Start()
    {
        // Find target AI
        targetAI = GetComponent<CPUPlayer>() ?? FindFirstObjectByType<CPUPlayer>();
        
        if (targetAI == null)
        {
            Debug.LogWarning("AIProfileManager: No CPU Player found");
            return;
        }

        InitializeProfiles();
        LoadSavedProfile();
    }

    private void InitializeProfiles()
    {
        if (profileDatabase == null)
        {
            Debug.LogError("AIProfileManager: No profile database assigned");
            return;
        }

        // Load unlock data
        if (enableUnlockSystem)
        {
            LoadUnlockData();
        }

        // Set default profile if none selected
        if (currentProfile == null)
        {
            currentProfile = profileDatabase.defaultProfile ?? profileDatabase.GetRandomProfile();
        }

        // Apply current profile
        ApplyProfile(currentProfile);
    }

    public void ApplyProfile(AIProfile profile)
    {
        if (profile == null || targetAI == null) return;
        
        currentProfile = profile;

        // Apply the stat preset
        if (profile.statPreset != null)
        {
            targetAI.ApplyCustomPreset(profile.statPreset);
        }

        // Update difficulty enum for compatibility
        UpdateDifficultyFromProfile(profile);

        // Save selection if enabled
        if (saveProfileSelection)
        {
            SaveCurrentProfile();
        }

        // Notify listeners
        OnProfileChanged?.Invoke(profile);

        Debug.Log($"Applied AI profile: {profile.profileName}");
    }

    public void SwitchToProfile(string profileName)
    {
        if (!allowRuntimeProfileSwitching) return;

        var profile = profileDatabase?.GetProfile(profileName);
        if (profile != null && profile.isUnlocked)
        {
            ApplyProfile(profile);
        }
        else
        {
            Debug.LogWarning($"Profile '{profileName}' not found or not unlocked");
        }
    }

    public void SwitchToRandomProfile(int? difficultyRating = null)
    {
        if (!allowRuntimeProfileSwitching || profileDatabase == null) return;

        List<AIProfile> availableProfiles;
        
        if (difficultyRating.HasValue)
        {
            availableProfiles = profileDatabase.GetProfilesByDifficulty(difficultyRating.Value);
            availableProfiles = availableProfiles.FindAll(p => p.isUnlocked);
        }
        else
        {
            availableProfiles = profileDatabase.GetUnlockedProfiles();
        }

        if (availableProfiles.Count > 0)
        {
            var randomProfile = availableProfiles[Random.Range(0, availableProfiles.Count)];
            ApplyProfile(randomProfile);
        }
    }

    public void UnlockProfile(string profileName)
    {
        if (!enableUnlockSystem || profileDatabase == null) return;

        var profile = profileDatabase.GetProfile(profileName);
        if (profile != null && !profile.isUnlocked)
        {
            profile.isUnlocked = true;
            SaveUnlockData();
            OnProfileUnlocked?.Invoke(profile);
            Debug.Log($"Unlocked AI profile: {profileName}");
        }
    }

    public bool IsProfileUnlocked(string profileName)
    {
        var profile = profileDatabase?.GetProfile(profileName);
        return profile?.isUnlocked ?? false;
    }

    public List<AIProfile> GetAvailableProfiles()
    {
        return profileDatabase?.GetUnlockedProfiles() ?? new List<AIProfile>();
    }

    public AIProfile GetCurrentProfile()
    {
        return currentProfile;
    }

    private void UpdateDifficultyFromProfile(AIProfile profile)
    {
        if (targetAI == null) return;

        // Map difficulty rating to CPUDifficulty enum
        CPUDifficulty difficulty = profile.difficultyRating switch
        {
            1 or 2 => CPUDifficulty.Easy,
            3 => CPUDifficulty.Medium,
            4 or 5 => CPUDifficulty.Hard,
            _ => CPUDifficulty.Medium
        };

        targetAI.difficulty = difficulty;
    }

    private void SaveCurrentProfile()
    {
        if (currentProfile != null)
        {
            PlayerPrefs.SetString(PROFILE_SAVE_KEY, currentProfile.profileName);
        }
    }

    private void LoadSavedProfile()
    {
        if (saveProfileSelection && PlayerPrefs.HasKey(PROFILE_SAVE_KEY))
        {
            string savedProfileName = PlayerPrefs.GetString(PROFILE_SAVE_KEY);
            var savedProfile = profileDatabase?.GetProfile(savedProfileName);
            
            if (savedProfile != null && savedProfile.isUnlocked)
            {
                currentProfile = savedProfile;
                ApplyProfile(currentProfile);
            }
        }
    }

    private void SaveUnlockData()
    {
        if (profileDatabase == null) return;

        var unlockedNames = new List<string>();
        foreach (var profile in profileDatabase.profiles)
        {
            if (profile.isUnlocked)
            {
                unlockedNames.Add(profile.profileName);
            }
        }

        string data = string.Join(",", unlockedNames);
        PlayerPrefs.SetString(UNLOCK_DATA_KEY, data);
    }

    private void LoadUnlockData()
    {
        if (!PlayerPrefs.HasKey(UNLOCK_DATA_KEY) || profileDatabase == null) return;

        string data = PlayerPrefs.GetString(UNLOCK_DATA_KEY);
        var unlockedNames = new HashSet<string>(data.Split(','));

        foreach (var profile in profileDatabase.profiles)
        {
            profile.isUnlocked = unlockedNames.Contains(profile.profileName);
        }
    }

    // Public API for game systems
    public void OnPlayerWinStreak(int streak)
    {
        if (!enableUnlockSystem) return;

        // Example unlock conditions based on win streaks
        if (streak >= 5)
        {
            UnlockProfile("Learning AI");
        }
        if (streak >= 10)
        {
            UnlockProfile("Pro Champion");
        }
    }

    public void OnPlayerPerformance(float accuracy, float reactionTime)
    {
        if (!enableUnlockSystem) return;

        // Unlock profiles based on player skill demonstration
        if (accuracy > 0.8f && reactionTime < 0.3f)
        {
            UnlockProfile("Power Hitter");
        }
    }

    // Editor helper methods
#if UNITY_EDITOR
    [ContextMenu("Create Profile Database")]
    private void CreateProfileDatabase()
    {
        if (profileDatabase == null)
        {
            profileDatabase = ScriptableObject.CreateInstance<AIProfileDatabase>();
            profileDatabase.InitializeDefaultProfiles();
            
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Create AI Profile Database",
                "AIProfileDatabase",
                "asset",
                "Choose where to save the AI profile database");
                
            if (!string.IsNullOrEmpty(path))
            {
                UnityEditor.AssetDatabase.CreateAsset(profileDatabase, path);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"Created AI Profile Database at {path}");
            }
        }
    }
#endif
}