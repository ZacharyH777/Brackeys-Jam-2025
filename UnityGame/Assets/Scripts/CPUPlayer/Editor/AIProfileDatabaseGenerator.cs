using UnityEngine;
using UnityEditor;
using System.IO;

public class AIProfileDatabaseGenerator
{
    [MenuItem("Tools/PingPong/Create AI Profile Database", priority = 1)]
    public static void CreateAIProfileDatabase()
    {
        // Create the database instance
        var database = ScriptableObject.CreateInstance<AIProfileDatabase>();
        database.InitializeDefaultProfiles();
        
        // Ensure the directory exists
        string assetPath = "Assets/ScriptableObjects/AI/";
        if (!Directory.Exists(assetPath))
        {
            Directory.CreateDirectory(assetPath);
        }
        
        // Create the asset
        string fullPath = assetPath + "AIProfileDatabase.asset";
        AssetDatabase.CreateAsset(database, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Select the created asset
        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
        
        Debug.Log($"Created AI Profile Database at {fullPath}");
    }
    
    [MenuItem("Tools/PingPong/Assign AI Profile Database to All Managers", priority = 2)]
    public static void AssignDatabaseToAllManagers()
    {
        // Find the database
        string[] guids = AssetDatabase.FindAssets("t:AIProfileDatabase");
        if (guids.Length == 0)
        {
            Debug.LogError("No AIProfileDatabase found. Create one first using 'Tools/PingPong/Create AI Profile Database'");
            return;
        }
        
        string databasePath = AssetDatabase.GUIDToAssetPath(guids[0]);
        AIProfileDatabase database = AssetDatabase.LoadAssetAtPath<AIProfileDatabase>(databasePath);
        
        if (database == null)
        {
            Debug.LogError("Failed to load AIProfileDatabase");
            return;
        }
        
        // Find all AIProfileManager components in the scene
        AIProfileManager[] managers = Object.FindObjectsByType<AIProfileManager>(FindObjectsSortMode.None);
        
        if (managers.Length == 0)
        {
            Debug.LogWarning("No AIProfileManager components found in the current scene");
            return;
        }
        
        // Assign the database to all managers
        foreach (var manager in managers)
        {
            manager.profileDatabase = database;
            EditorUtility.SetDirty(manager);
        }
        
        Debug.Log($"Assigned AI Profile Database to {managers.Length} manager(s)");
    }
}