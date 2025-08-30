using UnityEngine;

public class AIProfileDatabaseCreator : MonoBehaviour
{
    [Header("Auto-create database on Start")]
    public bool createOnStart = true;
    
    [Header("Database Reference")]
    public AIProfileDatabase createdDatabase;
    
    private void Start()
    {
        if (createOnStart && createdDatabase == null)
        {
            CreateDatabase();
        }
    }
    
    [ContextMenu("Create AI Profile Database")]
    public void CreateDatabase()
    {
        // Create the database instance
        var database = ScriptableObject.CreateInstance<AIProfileDatabase>();
        database.name = "AIProfileDatabase";
        
        // Initialize with default profiles but without StatPreset dependencies for now
        database.profiles.Clear();
        
        // Add basic profiles
        database.profiles.Add(new AIProfile("Beginner Bot", "Perfect for learning the basics", null)
        {
            difficultyRating = 1,
            playstyle = "Forgiving",
            profileColor = Color.green
        });
        
        database.profiles.Add(new AIProfile("Balanced Player", "Even match for most players", null)
        {
            difficultyRating = 3,
            playstyle = "Balanced",
            profileColor = Color.yellow
        });
        
        database.profiles.Add(new AIProfile("Pro Champion", "Serious challenge ahead", null)
        {
            difficultyRating = 5,
            playstyle = "Competitive",
            profileColor = Color.red
        });
        
        database.profiles.Add(new AIProfile("The Wall", "Defensive specialist", null)
        {
            difficultyRating = 4,
            playstyle = "Defensive",
            profileColor = Color.blue
        });
        
        database.profiles.Add(new AIProfile("Power Hitter", "Aggressive attacker", null)
        {
            difficultyRating = 4,
            playstyle = "Aggressive",
            profileColor = Color.magenta
        });
        
        // Set default profile
        if (database.profiles.Count > 1)
            database.defaultProfile = database.profiles[1]; // Balanced Player
        
        database.allowCustomProfiles = true;
        
        // Store reference
        createdDatabase = database;
        
        // Try to assign to any AIProfileManager in the scene
        var managers = FindObjectsByType<AIProfileManager>(FindObjectsSortMode.None);
        foreach (var manager in managers)
        {
            if (manager.profileDatabase == null)
            {
                manager.profileDatabase = database;
                Debug.Log($"Assigned database to AIProfileManager on {manager.gameObject.name}");
            }
        }
        
        Debug.Log($"Created AI Profile Database with {database.profiles.Count} profiles");
    }
}