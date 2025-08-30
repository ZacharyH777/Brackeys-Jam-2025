using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single-component solution for adding CPU player support to any scene.
/// Just add this to a GameObject and click "Setup CPU Support" - that's it!
/// </summary>
public class CPUOneClickSetup : MonoBehaviour
{
    [Header("🎮 CPU Setup")]
    [Tooltip("Use the context menu (right-click on component) or press Play to auto-setup")]
    [Space(10)]
    [TextArea(2, 4)]
    public string instructions = "✅ AUTOMATIC SETUP ENABLED\nCPU support will be configured when you press Play.\n\nManual setup: RIGHT-CLICK → '🚀 Setup CPU Support'";
    
    [Space(5)]
    [Tooltip("Auto-setup when Play button is pressed")]
    public bool autoSetupOnPlay = true;
    
    [Header("⚙️ Configuration")]
    public CPUDifficulty defaultDifficulty = CPUDifficulty.Medium;
    
    [Header("📊 Status")]
    [SerializeField] private bool isSetupComplete = false;
    [SerializeField] private string lastSetupResult = "Not configured";
    
    [Header("🔧 Auto-Found Components")]
    [SerializeField] private FixedSlotSpawner player1Spawner;
    [SerializeField] private FixedSlotSpawner player2Spawner;
    [SerializeField] private PingPongLoop gameController;
    [SerializeField] private BallPhysics2D ballPhysics;

    /// <summary>
    /// One-click setup for CPU support. Call this from the inspector or code.
    /// </summary>
    [ContextMenu("🚀 Setup CPU Support")]
    public void SetupCPUSupport()
    {
        Debug.Log("🚀 Starting One-Click CPU Setup...");
        
        try
        {
            // Step 1: Find all required components
            FindRequiredComponents();
            
            // Step 2: Validate scene
            if (!ValidateScene())
            {
                lastSetupResult = "❌ Scene validation failed - check console for details";
                return;
            }
            
            // Step 3: Configure spawners for CPU support
            ConfigureSpawners();
            
            // Step 4: Setup AI profile system
            SetupAISystem();
            
            // Step 5: Configure for single player mode
            SetupSinglePlayerMode();
            
            // Step 6: Final cleanup - disable any remaining PingPongMovement on CPU players
            CleanupCPUComponents();
            
            // Step 7: Ensure CPU serving works
            TestCPUServingConnection();
            
            isSetupComplete = true;
            lastSetupResult = "✅ CPU support configured successfully!";
            Debug.Log("✅ CPU setup complete! You can now play against CPU opponents.");
        }
        catch (System.Exception e)
        {
            isSetupComplete = false;
            lastSetupResult = $"❌ Setup failed: {e.Message}";
            Debug.LogError($"CPU setup failed: {e.Message}");
        }
    }

    private void FindRequiredComponents()
    {
        Debug.Log("🔍 Finding scene components...");
        
        // Find spawners
        var spawners = FindObjectsByType<FixedSlotSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            if (spawner.slot == FixedSlotSpawner.Slot.P1)
                player1Spawner = spawner;
            else if (spawner.slot == FixedSlotSpawner.Slot.P2)
                player2Spawner = spawner;
        }
        
        // Find game components
        gameController = FindFirstObjectByType<PingPongLoop>();
        ballPhysics = FindFirstObjectByType<BallPhysics2D>();
        
        Debug.Log($"Found: P1 Spawner={player1Spawner != null}, P2 Spawner={player2Spawner != null}, Game Controller={gameController != null}, Ball={ballPhysics != null}");
    }
    
    private bool ValidateScene()
    {
        Debug.Log("✓ Validating scene setup...");
        bool isValid = true;
        
        if (player1Spawner == null)
        {
            Debug.LogError("❌ Player 1 spawner not found! Make sure you have a FixedSlotSpawner with slot = P1");
            isValid = false;
        }
        
        if (player2Spawner == null)
        {
            Debug.LogError("❌ Player 2 spawner not found! Make sure you have a FixedSlotSpawner with slot = P2");
            isValid = false;
        }
        
        if (gameController == null)
        {
            Debug.LogError("❌ PingPongLoop game controller not found!");
            isValid = false;
        }
        
        if (ballPhysics == null)
        {
            Debug.LogError("❌ BallPhysics2D component not found!");
            isValid = false;
        }
        
        return isValid;
    }
    
    private void ConfigureSpawners()
    {
        Debug.Log("⚙️ Configuring spawners for CPU support...");
        
        // Configure both spawners with default characters
        ConfigureSpawner(player1Spawner, "Player 1");
        ConfigureSpawner(player2Spawner, "Player 2");
    }
    
    private void ConfigureSpawner(FixedSlotSpawner spawner, string spawnerName)
    {
        if (spawner == null) return;
        
        Debug.Log($"Configuring {spawnerName} spawner...");
        
        // If spawner has no characters, add default ones
        if (spawner.characterPrefabs == null || spawner.characterPrefabs.Length == 0)
        {
            Debug.LogWarning($"{spawnerName} spawner has no characters. You'll need to assign character prefabs manually.");
            return;
        }
        
        // Ensure all characters have CPU prefabs (can be same as player prefabs for auto-conversion)
        var characters = spawner.characterPrefabs;
        for (int i = 0; i < characters.Length; i++)
        {
            var character = characters[i];
            
            // Auto-assign CPU prefabs if missing
            if (character.upCPUPrefab == null && character.upPrefabWithPlayerInput != null)
            {
                character.upCPUPrefab = character.upPrefabWithPlayerInput;
            }
            
            if (character.downCPUPrefab == null && character.downPrefabWithPlayerInput != null)
            {
                character.downCPUPrefab = character.downPrefabWithPlayerInput;
            }
            
            characters[i] = character;
        }
        
        spawner.characterPrefabs = characters;
    }
    
    private void SetupAISystem()
    {
        Debug.Log("🧠 Setting up AI system...");
        
        // Create AI profile database creator if it doesn't exist
        var databaseCreator = FindFirstObjectByType<AIProfileDatabaseCreator>();
        if (databaseCreator == null)
        {
            var databaseCreatorGO = new GameObject("AI Profile Database Creator");
            databaseCreator = databaseCreatorGO.AddComponent<AIProfileDatabaseCreator>();
            databaseCreator.createOnStart = true;
            databaseCreator.CreateDatabase(); // Create immediately
            Debug.Log("Created AI Profile Database Creator");
        }
        
        // Create AI profile manager if it doesn't exist
        var profileManager = FindFirstObjectByType<AIProfileManager>();
        if (profileManager == null)
        {
            var profileManagerGO = new GameObject("AI Profile Manager");
            profileManager = profileManagerGO.AddComponent<AIProfileManager>();
            
            // Assign the database if we have one
            if (databaseCreator?.createdDatabase != null)
            {
                profileManager.profileDatabase = databaseCreator.createdDatabase;
                Debug.Log("Assigned database to AI Profile Manager");
            }
            
            Debug.Log("Created AI Profile Manager");
        }
    }
    
    private void SetupSinglePlayerMode()
    {
        Debug.Log("🎯 Configuring single player mode...");
        
        // Configure for single player testing
        CharacterSelect.is_singleplayer = true;
        CharacterSelect.p2_is_cpu = true;
        CharacterSelect.cpu_difficulty = defaultDifficulty;
        
        // Use currently selected P1 character for CPU, or find a default
        if (string.IsNullOrEmpty(CharacterSelect.p1_character))
        {
            // No P1 character selected, find a default from spawner
            if (player1Spawner?.characterPrefabs?.Length > 0)
            {
                var defaultCharacter = player1Spawner.characterPrefabs[0].characterName;
                if (!string.IsNullOrEmpty(defaultCharacter))
                {
                    CharacterSelect.p1_character = defaultCharacter;
                }
            }
        }
        
        // Always set P2 character to match P1 for CPU mode
        CharacterSelect.p2_character = CharacterSelect.p1_character;
        
        if (!string.IsNullOrEmpty(CharacterSelect.p1_character))
        {
            Debug.Log($"✅ Set characters - P1: '{CharacterSelect.p1_character}', P2 (CPU): '{CharacterSelect.p2_character}'");
        }
        else
        {
            Debug.LogWarning("❌ Could not determine character selection - CPU may not spawn correctly");
            Debug.LogWarning($"Available characters in P1 spawner: {(player1Spawner?.characterPrefabs?.Length ?? 0)}");
        }
        
        Debug.Log($"Single player mode configured: CPU Difficulty = {defaultDifficulty}");
    }
    
    /// <summary>
    /// Quick test to spawn a CPU player and verify everything works
    /// </summary>
    [ContextMenu("🧪 Test CPU Player")]
    public void TestCPUPlayer()
    {
        if (!isSetupComplete)
        {
            Debug.LogWarning("Please run 'Setup CPU Support' first!");
            return;
        }
        
        Debug.Log("🧪 Testing CPU player functionality...");
        
        // Force single player mode
        CharacterSelect.is_singleplayer = true;
        CharacterSelect.p2_is_cpu = true;
        CharacterSelect.cpu_difficulty = defaultDifficulty;
        
        Debug.Log("✅ CPU player test configuration applied!");
        Debug.Log($"   - Single player: {CharacterSelect.is_singleplayer}");
        Debug.Log($"   - P2 is CPU: {CharacterSelect.p2_is_cpu}");
        Debug.Log($"   - CPU difficulty: {CharacterSelect.cpu_difficulty}");
        
        // Test serve system
        TestCPUServeSystem();
    }
    
    /// <summary>
    /// Show what characters are available in spawners
    /// </summary>
    [ContextMenu("📋 Show Available Characters")]
    public void ShowAvailableCharacters()
    {
        Debug.Log("📋 Available Characters in Spawners:");
        
        if (player1Spawner != null)
        {
            Debug.Log($"P1 Spawner ({player1Spawner.name}):");
            if (player1Spawner.characterPrefabs?.Length > 0)
            {
                for (int i = 0; i < player1Spawner.characterPrefabs.Length; i++)
                {
                    var character = player1Spawner.characterPrefabs[i];
                    Debug.Log($"  [{i}] '{character.characterName}' - Up: {character.upPrefabWithPlayerInput != null}, Down: {character.downPrefabWithPlayerInput != null}, CPU Up: {character.upCPUPrefab != null}, CPU Down: {character.downCPUPrefab != null}");
                }
            }
            else
            {
                Debug.LogWarning("  No characters configured!");
            }
        }
        
        if (player2Spawner != null)
        {
            Debug.Log($"P2 Spawner ({player2Spawner.name}):");
            if (player2Spawner.characterPrefabs?.Length > 0)
            {
                for (int i = 0; i < player2Spawner.characterPrefabs.Length; i++)
                {
                    var character = player2Spawner.characterPrefabs[i];
                    Debug.Log($"  [{i}] '{character.characterName}' - Up: {character.upPrefabWithPlayerInput != null}, Down: {character.downPrefabWithPlayerInput != null}, CPU Up: {character.upCPUPrefab != null}, CPU Down: {character.downCPUPrefab != null}");
                }
            }
            else
            {
                Debug.LogWarning("  No characters configured!");
            }
        }
        
        Debug.Log($"Current Selection - P1: '{CharacterSelect.p1_character}', P2: '{CharacterSelect.p2_character}', P2 is CPU: {CharacterSelect.p2_is_cpu}");
    }
    
    /// <summary>
    /// Test that CPU players can serve properly
    /// </summary>
    [ContextMenu("🏓 Test CPU Serve System")]
    public void TestCPUServeSystem()
    {
        Debug.Log("🏓 Testing CPU serve system...");
        
        var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
        if (cpuPlayers.Length == 0)
        {
            Debug.LogWarning("No CPU players found in scene!");
            return;
        }
        
        foreach (var cpu in cpuPlayers)
        {
            if (cpu.IsCPUPlayer)
            {
                Debug.Log($"Found CPU Player: {cpu.name} (Player ID: {cpu.player_owner?.player_id})");
                
                // Test serve request
                Debug.Log("Testing serve request...");
                cpu.RequestServe();
            }
        }
        
        var pingPongLoop = FindFirstObjectByType<PingPongLoop>();
        if (pingPongLoop != null)
        {
            Debug.Log($"Game Controller found: Current server = {pingPongLoop.GetCurrentServer()}");
        }
        else
        {
            Debug.LogWarning("No PingPongLoop found - CPU serves may not work properly!");
        }
    }
    
    private void CleanupCPUComponents()
    {
        Debug.Log("🧹 Cleaning up CPU components...");
        
        // Find all CPU players in the scene and ensure their PingPongMovement is disabled
        var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
        foreach (var cpuPlayer in cpuPlayers)
        {
            var pingPongMovement = cpuPlayer.GetComponent<PingPongMovement>();
            if (pingPongMovement != null && pingPongMovement.enabled)
            {
                pingPongMovement.enabled = false;
                Debug.Log($"Disabled PingPongMovement on CPU player: {cpuPlayer.gameObject.name}");
            }
            
            var playerMovement = cpuPlayer.GetComponent<PlayerMovement>();
            if (playerMovement != null && playerMovement.enabled)
            {
                playerMovement.enabled = false;
                Debug.Log($"Disabled PlayerMovement on CPU player: {cpuPlayer.gameObject.name}");
            }
            
            var playerInput = cpuPlayer.GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.enabled)
            {
                playerInput.enabled = false;
                Debug.Log($"Disabled PlayerInput on CPU player: {cpuPlayer.gameObject.name}");
            }
        }
        
        // Clean up any duplicate or problematic CPU players
        RemoveDuplicateCPUPlayers();
        
        // Find any PingPongMovement components that should be CPU players but aren't properly configured
        var allPingPongMovements = FindObjectsByType<PingPongMovement>(FindObjectsSortMode.None);
        foreach (var pingPongMovement in allPingPongMovements)
        {
            // Check if this object should be a CPU player based on naming or spawner slot
            if (ShouldBeCPUPlayer(pingPongMovement.gameObject))
            {
                var cpuPlayer = pingPongMovement.GetComponent<CPUPlayer>();
                if (cpuPlayer == null)
                {
                    // Convert this to a CPU player
                    Debug.Log($"Converting {pingPongMovement.gameObject.name} to CPU player");
                    ConvertObjectToCPUPlayer(pingPongMovement.gameObject);
                }
                
                // Disable the PingPongMovement
                if (pingPongMovement.enabled)
                {
                    pingPongMovement.enabled = false;
                    Debug.Log($"Disabled PingPongMovement on converted CPU player: {pingPongMovement.gameObject.name}");
                }
            }
        }
        
        Debug.Log($"Cleaned up {cpuPlayers.Length} existing CPU player(s) and converted any missing ones");
        
        // Final safeguard: ensure P1 objects are not CPU players
        RestoreP1PlayerFunctionality();
        
        // Ensure all CPU players have proper setup
        EnsureAllCPUPlayersHaveRequiredComponents();
    }
    
    private void RestoreP1PlayerFunctionality()
    {
        // Find any objects that should be P1 players but were incorrectly converted to CPU
        var allCpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
        foreach (var cpuPlayer in allCpuPlayers)
        {
            string name = cpuPlayer.gameObject.name.ToLower();
            if (name.Contains("p1") || name.Contains("targetp1"))
            {
                // This should be a human P1 player, not a CPU
                Debug.Log($"Restoring P1 functionality to incorrectly converted object: {cpuPlayer.gameObject.name}");
                
                // Remove CPU components
                DestroyImmediate(cpuPlayer.GetComponent<CPUPlayer>());
                var cpuMovement = cpuPlayer.GetComponent<CPUMovement>();
                if (cpuMovement != null)
                    DestroyImmediate(cpuMovement);
                
                // Re-enable player components
                var playerInput = cpuPlayer.GetComponent<PlayerInput>();
                if (playerInput != null)
                    playerInput.enabled = true;
                    
                var playerMovement = cpuPlayer.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                    playerMovement.enabled = true;
                    
                var pingPongMovement = cpuPlayer.GetComponent<PingPongMovement>();
                if (pingPongMovement != null)
                    pingPongMovement.enabled = true;
                    
                Debug.Log($"Restored P1 player functionality for: {cpuPlayer.gameObject.name}");
            }
        }
    }
    
    private void RemoveDuplicateCPUPlayers()
    {
        Debug.Log("🔄 Removing duplicate CPU players...");
        
        var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
        var p2Players = new System.Collections.Generic.List<CPUPlayer>();
        
        // Find all P2 CPU players
        foreach (var cpu in cpuPlayers)
        {
            if (cpu.player_owner != null && cpu.player_owner.player_id == PlayerId.P2)
            {
                p2Players.Add(cpu);
            }
        }
        
        if (p2Players.Count <= 1)
        {
            Debug.Log($"Found {p2Players.Count} P2 CPU player(s) - no duplicates to remove");
            return;
        }
        
        Debug.Log($"Found {p2Players.Count} P2 CPU players - removing duplicates");
        
        // Keep the one with the most components (likely the properly spawned one)
        CPUPlayer bestCPU = null;
        int bestScore = -1;
        
        foreach (var cpu in p2Players)
        {
            int score = 0;
            if (cpu.GetComponentInChildren<PaddleKinematics>() != null) score += 10;
            if (cpu.GetComponentsInChildren<SurfaceZone>().Length > 0) score += 5;
            if (cpu.gameObject.name.Contains("Clone")) score += 3; // Spawned objects have "Clone"
            if (!cpu.gameObject.name.ToLower().Contains("target")) score += 2; // Avoid target objects
            
            Debug.Log($"CPU Player '{cpu.gameObject.name}' score: {score}");
            
            if (score > bestScore)
            {
                bestScore = score;
                bestCPU = cpu;
            }
        }
        
        // Remove all others
        foreach (var cpu in p2Players)
        {
            if (cpu != bestCPU)
            {
                Debug.Log($"Removing duplicate CPU player: {cpu.gameObject.name}");
                DestroyImmediate(cpu.GetComponent<CPUPlayer>());
                var cpuMovement = cpu.GetComponent<CPUMovement>();
                if (cpuMovement != null)
                    DestroyImmediate(cpuMovement);
            }
        }
        
        Debug.Log($"Kept CPU player: {bestCPU?.gameObject.name}");
    }
    
    private bool ShouldBeCPUPlayer(GameObject obj)
    {
        // Only convert objects that are clearly P2 or CPU-related, not P1
        string name = obj.name.ToLower();
        
        // Explicit P2 indicators
        if (name.Contains("p2") || name.Contains("cpu"))
            return true;
            
        // Avoid converting P1 or generic target objects
        if (name.Contains("p1") || name.Contains("targetp1") || name.Contains("target"))
            return false;
            
        // Generic "2" only if in single player mode and it's not clearly P1
        if (CharacterSelect.is_singleplayer && CharacterSelect.p2_is_cpu && 
            name.EndsWith("2") && !name.Contains("p1"))
            return true;
            
        return false;
    }
    
    private void ConvertObjectToCPUPlayer(GameObject playerInstance)
    {
        // Remove or disable PlayerInput if present
        var playerInput = playerInstance.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }

        // Remove or disable PlayerMovement if present
        var playerMovement = playerInstance.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        // Remove or disable PingPongMovement if present
        var pingPongMovement = playerInstance.GetComponent<PingPongMovement>();
        if (pingPongMovement != null)
        {
            pingPongMovement.enabled = false;
        }

        // Add CPU components if not already present
        if (playerInstance.GetComponent<CPUPlayer>() == null)
        {
            var cpuPlayer = playerInstance.AddComponent<CPUPlayer>();
            cpuPlayer.difficulty = defaultDifficulty;
            
            // Set up required references
            cpuPlayer.ball = ballPhysics;
            cpuPlayer.cpu_movement = playerInstance.GetComponent<CPUMovement>();
            if (cpuPlayer.cpu_movement == null)
            {
                cpuPlayer.cpu_movement = playerInstance.AddComponent<CPUMovement>();
            }
            
            // Set up PlayerOwner - create if it doesn't exist
            var playerOwner = playerInstance.GetComponent<PlayerOwner>();
            if (playerOwner == null)
            {
                playerOwner = playerInstance.AddComponent<PlayerOwner>();
                Debug.Log($"Created PlayerOwner component for {playerInstance.name}");
            }
            
            // Set the correct player ID based on the object name
            string name = playerInstance.name.ToLower();
            if (name.Contains("p2") || name.Contains("targetp2") || name.Contains("2"))
            {
                playerOwner.player_id = PlayerId.P2;
            }
            else if (name.Contains("p1") || name.Contains("targetp1") || name.Contains("1"))
            {
                playerOwner.player_id = PlayerId.P1;
            }
            else
            {
                // Default to P2 for CPU players if unclear
                playerOwner.player_id = PlayerId.P2;
                Debug.Log($"Defaulting {playerInstance.name} to P2 since naming is unclear");
            }
            
            cpuPlayer.player_owner = playerOwner;
            Debug.Log($"Connected CPU player to PlayerOwner with ID: {playerOwner.player_id}");
        }

        if (playerInstance.GetComponent<CPUMovement>() == null)
        {
            playerInstance.AddComponent<CPUMovement>();
        }
        
        Debug.Log($"Successfully converted {playerInstance.name} to CPU player");
    }
    
    private void EnsureAllCPUPlayersHaveRequiredComponents()
    {
        Debug.Log("🔧 Ensuring all CPU players have required components...");
        
        var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
        foreach (var cpu in cpuPlayers)
        {
            // Ensure PlayerOwner exists
            var playerOwner = cpu.GetComponent<PlayerOwner>();
            if (playerOwner == null)
            {
                playerOwner = cpu.gameObject.AddComponent<PlayerOwner>();
                playerOwner.player_id = PlayerId.P2; // Default CPU players to P2
                cpu.player_owner = playerOwner;
                Debug.Log($"Added PlayerOwner to CPU player: {cpu.gameObject.name}");
            }
            
            // Ensure ball reference
            if (cpu.ball == null)
            {
                cpu.ball = ballPhysics;
                Debug.Log($"Connected ball reference for CPU player: {cpu.gameObject.name}");
            }
            
            // Ensure CPU movement reference
            if (cpu.cpu_movement == null)
            {
                var cpuMovement = cpu.GetComponent<CPUMovement>();
                if (cpuMovement == null)
                {
                    cpuMovement = cpu.gameObject.AddComponent<CPUMovement>();
                    Debug.Log($"Added CPUMovement to CPU player: {cpu.gameObject.name}");
                }
                cpu.cpu_movement = cpuMovement;
            }
        }
    }
    
    private void TestCPUServingConnection()
    {
        Debug.Log("🏓 Testing CPU serving connection...");
        
        var cpuPlayers = FindObjectsByType<CPUPlayer>(FindObjectsSortMode.None);
        foreach (var cpu in cpuPlayers)
        {
            if (cpu.player_owner != null)
            {
                Debug.Log($"✓ CPU Player '{cpu.gameObject.name}' with PlayerOwner ID: {cpu.player_owner.player_id}, Ball ref: {cpu.ball != null}, Movement ref: {cpu.cpu_movement != null}");
                
                // Test if the CPU has the IsCPUPlayer method (should always be true)
                if (cpu.IsCPUPlayer)
                {
                    Debug.Log($"✓ CPU Player '{cpu.gameObject.name}' ({cpu.player_owner.player_id}) is properly configured for serving");
                }
                
                // Check for ball hitting components
                var paddleKinematics = cpu.GetComponentInChildren<PaddleKinematics>();
                var surfaceZones = cpu.GetComponentsInChildren<SurfaceZone>();
                var paddleSurfaceZones = System.Array.FindAll(surfaceZones, sz => sz.zone_type == SurfaceZone.ZoneType.Paddle);
                
                Debug.Log($"🎯 CPU Player '{cpu.gameObject.name}' ({cpu.player_owner.player_id}) hitting components: PaddleKinematics={paddleKinematics != null}, Paddle Zones={paddleSurfaceZones.Length}");
                
                if (paddleKinematics == null)
                {
                    Debug.LogWarning($"❌ CPU Player {cpu.player_owner.player_id} missing PaddleKinematics - cannot hit ball!");
                }
                if (paddleSurfaceZones.Length == 0)
                {
                    Debug.LogWarning($"❌ CPU Player {cpu.player_owner.player_id} missing Paddle SurfaceZones - cannot hit ball!");
                }
            }
            else
            {
                Debug.LogWarning($"❌ CPU Player on {cpu.gameObject.name} has no PlayerOwner - serving will not work!");
            }
        }
    }
    
    /// <summary>
    /// Reset CPU setup and clear configuration
    /// </summary>
    [ContextMenu("🔄 Reset CPU Setup")]
    public void ResetSetup()
    {
        isSetupComplete = false;
        lastSetupResult = "Reset - ready for new setup";
        
        CharacterSelect.is_singleplayer = false;
        CharacterSelect.p2_is_cpu = false;
        
        Debug.Log("🔄 CPU setup reset. Run 'Setup CPU Support' to reconfigure.");
    }

    void Start()
    {
        // Auto-setup if requested
        if (autoSetupOnPlay && !isSetupComplete)
        {
            SetupCPUSupport();
        }
    }
}