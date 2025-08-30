using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utility for generating CPU player prefabs from existing player prefabs
/// </summary>
public static class CPUPrefabGenerator
{
#if UNITY_EDITOR
    /// <summary>
    /// Create a CPU variant of a player prefab
    /// </summary>
    public static GameObject CreateCPUVariant(GameObject originalPrefab, string variantName = null)
    {
        if (originalPrefab == null)
        {
            Debug.LogError("Original prefab is null");
            return null;
        }

        // Create variant name if not provided
        if (string.IsNullOrEmpty(variantName))
        {
            variantName = originalPrefab.name + "_CPU";
        }

        // Instantiate the original prefab
        GameObject cpuVariant = PrefabUtility.InstantiatePrefab(originalPrefab) as GameObject;
        if (cpuVariant == null)
        {
            Debug.LogError($"Failed to instantiate prefab: {originalPrefab.name}");
            return null;
        }

        cpuVariant.name = variantName;

        // Convert to CPU player
        ConvertToCPUPlayer(cpuVariant);

        // Create prefab asset
        string prefabPath = $"Assets/Prefabs/CPU/{variantName}.prefab";
        
        // Ensure directory exists
        string directory = System.IO.Path.GetDirectoryName(prefabPath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        // Save as prefab
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(cpuVariant, prefabPath);
        
        // Clean up scene instance
        Object.DestroyImmediate(cpuVariant);

        if (prefabAsset != null)
        {
            Debug.Log($"Created CPU prefab variant: {prefabPath}");
            return prefabAsset;
        }
        else
        {
            Debug.LogError($"Failed to create CPU prefab: {prefabPath}");
            return null;
        }
    }

    /// <summary>
    /// Convert a GameObject to be CPU-controlled
    /// </summary>
    public static void ConvertToCPUPlayer(GameObject playerObject)
    {
        if (playerObject == null) return;

        Debug.Log($"Converting {playerObject.name} to CPU player...");

        // Remove or disable PlayerInput component
        var playerInput = playerObject.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            // Don't destroy completely as other components might reference it
            playerInput.enabled = false;
            Debug.Log("- Disabled PlayerInput component");
        }

        // Remove or disable PlayerMovement component
        var playerMovement = playerObject.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            Debug.Log("- Disabled PlayerMovement component");
        }

        // Add StatSystem if not present
        var statSystem = playerObject.GetComponent<StatSystem>();
        if (statSystem == null)
        {
            statSystem = playerObject.AddComponent<StatSystem>();
            Debug.Log("- Added StatSystem component");
        }

        // Add CPUPlayer component
        var cpuPlayer = playerObject.GetComponent<CPUPlayer>();
        if (cpuPlayer == null)
        {
            cpuPlayer = playerObject.AddComponent<CPUPlayer>();
            ConfigureCPUPlayer(cpuPlayer, playerMovement);
            Debug.Log("- Added CPUPlayer component");
        }

        // Add CPUMovement component
        var cpuMovement = playerObject.GetComponent<CPUMovement>();
        if (cpuMovement == null)
        {
            cpuMovement = playerObject.AddComponent<CPUMovement>();
            ConfigureCPUMovement(cpuMovement, playerMovement);
            Debug.Log("- Added CPUMovement component");
        }

        // Link components
        if (cpuPlayer.cpu_movement == null)
        {
            cpuPlayer.cpu_movement = cpuMovement;
        }

        // Ensure PlayerOwner exists and is set correctly
        var playerOwner = playerObject.GetComponent<PlayerOwner>();
        if (playerOwner == null)
        {
            playerOwner = playerObject.AddComponent<PlayerOwner>();
            playerOwner.player_id = PlayerId.P2; // Default to P2 for CPU
            Debug.Log("- Added PlayerOwner component (set to P2)");
        }

        if (cpuPlayer.player_owner == null)
        {
            cpuPlayer.player_owner = playerOwner;
        }

        Debug.Log($"✓ Successfully converted {playerObject.name} to CPU player");
    }

    private static void ConfigureCPUPlayer(CPUPlayer cpuPlayer, PlayerMovement originalMovement)
    {
        if (cpuPlayer == null) return;

        // Set default difficulty
        cpuPlayer.difficulty = CPUDifficulty.Medium;
        cpuPlayer.applyPresetOnStart = true;

        // Copy legacy values from original movement if available
        if (originalMovement != null)
        {
            // Map some movement parameters to AI parameters
            cpuPlayer.base_reaction_time = 0.25f; // Default
            cpuPlayer.accuracy = 0.8f;
            cpuPlayer.reachable_distance = 3.5f;
        }

        Debug.Log("- Configured CPUPlayer with default settings");
    }

    private static void ConfigureCPUMovement(CPUMovement cpuMovement, PlayerMovement originalMovement)
    {
        if (cpuMovement == null) return;

        // Copy movement parameters from original if available
        if (originalMovement != null)
        {
            cpuMovement.max_speed = originalMovement.max_speed;
            cpuMovement.acceleration = originalMovement.acceleration;
            cpuMovement.deceleration = originalMovement.deceleration;
            cpuMovement.ping_pong_target = originalMovement.ping_pong_target;
            
            Debug.Log("- Copied movement parameters from original PlayerMovement");
        }
        else
        {
            // Set reasonable defaults
            cpuMovement.max_speed = 12f;
            cpuMovement.acceleration = 60f;
            cpuMovement.deceleration = 80f;
            
            Debug.Log("- Set default movement parameters");
        }

        cpuMovement.movement_smoothing = 0.8f;
        cpuMovement.target_threshold = 0.2f;
    }

    /// <summary>
    /// Create CPU variants for all characters in a character prefab array
    /// </summary>
    public static void CreateCPUVariantsForAllCharacters(FixedSlotSpawner.CharacterPrefab[] characterPrefabs)
    {
        if (characterPrefabs == null) return;

        int created = 0;
        
        foreach (var character in characterPrefabs)
        {
            if (string.IsNullOrEmpty(character.characterName)) continue;

            // Create CPU variants for up and down prefabs
            if (character.upPrefabWithPlayerInput != null)
            {
                string upName = $"{character.characterName}_Up_CPU";
                var upCPU = CreateCPUVariant(character.upPrefabWithPlayerInput, upName);
                if (upCPU != null) created++;
            }

            if (character.downPrefabWithPlayerInput != null)
            {
                string downName = $"{character.characterName}_Down_CPU";
                var downCPU = CreateCPUVariant(character.downPrefabWithPlayerInput, downName);
                if (downCPU != null) created++;
            }
        }

        Debug.Log($"Created {created} CPU prefab variants");
    }

    /// <summary>
    /// Menu item to create CPU variants from selected prefabs
    /// </summary>
    [MenuItem("Tools/Ping Pong/Create CPU Variants from Selection")]
    public static void CreateCPUVariantsFromSelection()
    {
        var selectedObjects = Selection.gameObjects;
        
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select one or more player prefabs to convert to CPU variants.", "OK");
            return;
        }

        int created = 0;
        
        foreach (var obj in selectedObjects)
        {
            // Check if it's a prefab
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            if (prefabAsset == null)
            {
                // Check if obj itself is a prefab asset
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                {
                    prefabAsset = obj;
                }
            }

            if (prefabAsset != null)
            {
                var cpuVariant = CreateCPUVariant(prefabAsset);
                if (cpuVariant != null) created++;
            }
            else
            {
                Debug.LogWarning($"{obj.name} is not a prefab, skipping...");
            }
        }

        if (created > 0)
        {
            EditorUtility.DisplayDialog("Success", $"Created {created} CPU prefab variants!", "OK");
            AssetDatabase.Refresh();
        }
        else
        {
            EditorUtility.DisplayDialog("No Variants Created", "No CPU variants were created. Make sure you selected valid prefabs.", "OK");
        }
    }

    /// <summary>
    /// Validate that a prefab is suitable for CPU conversion
    /// </summary>
    public static bool ValidatePrefabForCPUConversion(GameObject prefab)
    {
        if (prefab == null) return false;

        // Check for required components
        bool hasMovement = prefab.GetComponent<PlayerMovement>() != null;
        bool hasRigidbody = prefab.GetComponent<Rigidbody2D>() != null;
        
        if (!hasMovement)
        {
            Debug.LogWarning($"{prefab.name} doesn't have PlayerMovement component");
        }
        
        if (!hasRigidbody)
        {
            Debug.LogWarning($"{prefab.name} doesn't have Rigidbody2D component");
        }

        return hasMovement && hasRigidbody;
    }
#endif
}