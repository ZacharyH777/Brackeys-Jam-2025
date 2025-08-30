using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public sealed class FixedSlotSpawner : MonoBehaviour
{
    public enum Slot { P1 = 0, P2 = 1 }

    [System.Serializable]
    public struct CharacterPrefab
    {
        public string characterName;
        public GameObject upPrefabWithPlayerInput;
        public GameObject downPrefabWithPlayerInput;
        public GameObject upCPUPrefab;
        public GameObject downCPUPrefab;
    }

    [Header("Slot")]
    public Slot slot = Slot.P1;

    [Header("Characters (name -> UP/DOWN prefabs)")]
    public CharacterPrefab[] characterPrefabs;

    [Header("Input (override)")]
    [Tooltip("Optional override; if null, prefab's PlayerInput.actions are cloned so each player has an independent asset.")]
    public InputActionAsset actions;

    [Header("Scheme names (must match your InputActionAsset)")]
    public string gamepadScheme       = "Gamepad";
    public string keyboardMouseScheme = "Keyboard&Mouse";
    public string keyboardOnlyScheme  = "Keyboard";

    [Header("Behavior")]
    [Tooltip("Auto-add Mouse if using a keyboard scheme but Mouse wasn't captured.")]
    public bool autoAddMouseForKeyboardSchemes = true;

    [Tooltip("Prefer pads on fallback when registry is empty.")]
    public bool preferGamepadsOnFallback = true;

    [Header("Preference / Testing")]
    [Tooltip("Give P2 first dibs on a single available gamepad during fallback.")]
    public bool forcePadForP2 = true;
    [Tooltip("Allow both players to reuse the same keyboard (testing only).")]
    public bool allowKeyboardReuse = true;
    [Tooltip("Allow both players to reuse the same mouse (testing only).")]
    public bool allowMouseReuse = true;
    [Tooltip("Prefer using DeviceRegistry mapping if present and valid.")]
    public bool preferRegistryDevices = true;
    [Tooltip("Log current devices found at spawn time.")]
    public bool logDeviceSurvey = false;

    [Header("Spawn")]
    public Transform spawnPoint;

    /* --- shared across both spawners this scene to avoid double pairing --- */
    private static readonly HashSet<InputDevice> s_claimed = new HashSet<InputDevice>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ClearClaimsOnSceneLoad()
    {
        s_claimed.Clear();
    }

    void Start()
    {
        if (logDeviceSurvey)
            Debug.Log($"[{name}] Survey: pads={Gamepad.all.Count} kb={(Keyboard.current!=null)} mouse={(Mouse.current!=null)}");

        // Check if this slot should be CPU controlled
        bool isCPUPlayer = (slot == Slot.P2) && CharacterSelect.IsP2CPU;
        
        if (isCPUPlayer)
        {
            SpawnCPUPlayer();
            return;
        }

        // 1) Pick the prefab
        string chosen = (slot == Slot.P1) ? CharacterSelect.p1_character : CharacterSelect.p2_character;
        if (string.IsNullOrEmpty(chosen))
        {
            Debug.LogWarning($"[{name}] No character chosen for {slot}. Skipping spawn.");
            return;
        }

        var entry = characterPrefabs.FirstOrDefault(p => p.characterName == chosen);
        bool spawnerIsTop = transform.position.y > 0f;
        GameObject prefab = spawnerIsTop ? entry.downPrefabWithPlayerInput : entry.upPrefabWithPlayerInput;
        if (!prefab) prefab = entry.upPrefabWithPlayerInput ?? entry.downPrefabWithPlayerInput;
        if (!prefab) { Debug.LogError($"[{name}] No prefab mapped for '{chosen}'"); return; }

        // 2) Get desired devices from DeviceRegistry (validate first)
        if (DeviceRegistry.Instance != null)
            DeviceRegistry.Instance.ValidateAndPrune();

        string scheme = null;
        InputDevice[] devices = null;
        bool haveBundle = preferRegistryDevices && DeviceRegistry.TryGetDevices((int)slot, out scheme, out devices);

        // Keep relevant + alive devices
        if (haveBundle)
        {
            devices = devices
                .Where(d => d != null && d.added && (d is Gamepad || d is Keyboard || d is Mouse))
                .ToArray();

            // If keyboard scheme but no mouse, optionally add it
            if (devices.Any(d => d is Keyboard) &&
                autoAddMouseForKeyboardSchemes &&
                devices.All(d => d is not Mouse) &&
                Mouse.current != null)
            {
                devices = devices.Concat(new InputDevice[] { Mouse.current }).ToArray();
            }
        }

        // 3) Remove devices already claimed by the other player (unless reuse toggles say it's OK)
        if (haveBundle)
        {
            devices = devices.Where(d =>
                !s_claimed.Contains(d) ||
                (allowKeyboardReuse && d is Keyboard) ||
                (allowMouseReuse    && d is Mouse)
            ).Distinct().ToArray();

            if (devices.Length == 0) haveBundle = false; // nothing left, will fallback
        }

        // 4) Fallbacks (make sure P1/P2 don’t share)
        if (!haveBundle)
        {
            // (Optional) P2 gets the pad first when only one is present
            if (slot == Slot.P2 && forcePadForP2 && Gamepad.all.Count > 0)
            {
                var freePad = Gamepad.all.FirstOrDefault(p => !s_claimed.Contains(p));
                if (freePad != null)
                {
                    scheme  = gamepadScheme;
                    devices = new InputDevice[] { freePad };
                    haveBundle = true;
                }
            }

            // General gamepad preference
            if (!haveBundle && preferGamepadsOnFallback && Gamepad.all.Count > 0)
            {
                var freePad = Gamepad.all.FirstOrDefault(p => !s_claimed.Contains(p));
                if (freePad != null)
                {
                    scheme  = gamepadScheme;
                    devices = new InputDevice[] { freePad };
                    haveBundle = true;
                }
            }

            // Keyboard / Keyboard+Mouse
            if (!haveBundle && Keyboard.current != null)
            {
                var list = new List<InputDevice> { Keyboard.current };
                if (autoAddMouseForKeyboardSchemes && Mouse.current != null &&
                    (!s_claimed.Contains(Mouse.current) || allowMouseReuse))
                {
                    list.Add(Mouse.current);
                }

                // If mouse is already claimed and we don't allow reuse, still allow keyboard-only
                scheme = (list.Any(d => d is Mouse))
                         ? keyboardMouseScheme
                         : (string.IsNullOrEmpty(keyboardOnlyScheme) ? keyboardMouseScheme : keyboardOnlyScheme);

                devices = list.Where(d =>
                    !s_claimed.Contains(d) ||
                    (allowKeyboardReuse && d is Keyboard) ||
                    (allowMouseReuse    && d is Mouse)
                ).ToArray();

                haveBundle = devices.Length > 0;
            }

            if (!haveBundle)
            {
                Debug.LogError($"[{name}] No free input devices found for {slot}.");
                return;
            }
        }

        // 5) Instantiate and enforce pairing
        var single = (devices.Length == 1) ? devices[0] : null;
        var pi = PlayerInput.Instantiate(
            prefab,
            playerIndex: (int)slot,
            controlScheme: scheme,
            splitScreenIndex: -1,
            pairWithDevice: single
        );

        if ((int)slot == 0 && pi.camera != null) pi.camera.rect = new Rect(0f, 0f, 0.5f, 1f);
        if ((int)slot == 1 && pi.camera != null) pi.camera.rect = new Rect(0.5f, 0f, 0.5f, 1f);

        // --- Ensure each PlayerInput gets its OWN cloned action asset ---
        var sourceAsset = (actions != null) ? actions : pi.actions;
        if (sourceAsset != null)
        {
            var clone = Object.Instantiate(sourceAsset);
            pi.actions = clone;

            // Associate the cloned actions with this user (important for multi-user filtering)
            pi.user.AssociateActionsWithUser(pi.actions);

            // Make sure it's enabled
            if (!pi.actions.enabled) pi.actions.Enable();
        }
        // ----------------------------------------------------------------

        if (spawnPoint) pi.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // hard-enforce: only our devices
        pi.user.UnpairDevices();
        foreach (var d in devices) InputUser.PerformPairingWithDevice(d, pi.user);
        pi.SwitchCurrentControlScheme(scheme, devices);
        pi.neverAutoSwitchControlSchemes = true;

        // Save mapping for future scenes
        DeviceRegistry.SetDevices((int)slot, scheme, devices);

        // claim them so the other spawner cannot reuse (respect testing toggles)
        foreach (var d in devices)
        {
            if (d is Gamepad) { s_claimed.Add(d); continue; }
            if (d is Keyboard) { if (!allowKeyboardReuse) s_claimed.Add(d); continue; }
            if (d is Mouse)    { if (!allowMouseReuse)    s_claimed.Add(d); continue; }
        }

        // Optional safety: remove any stray not in our list
        foreach (var d in pi.user.pairedDevices.ToArray())
            if (!devices.Contains(d)) pi.user.UnpairDevice(d);

        // Attach cleanup so if this player is destroyed, devices are unpaired and claims freed
        var cleaner = pi.gameObject.AddComponent<InputCleanup>();
        cleaner.Initialize(pi, devices);

        Debug.Log($"[{pi.name}] {slot} -> '{scheme}' | devices: {string.Join(", ", pi.user.pairedDevices.Select(d => d.displayName))}");
    }

    private void SpawnCPUPlayer()
    {
        // 1) Pick the CPU prefab
        string chosen = CharacterSelect.p2_character;
        if (string.IsNullOrEmpty(chosen))
        {
            Debug.LogWarning($"[{name}] No character chosen for CPU {slot}. Skipping spawn.");
            return;
        }

        var entry = characterPrefabs.FirstOrDefault(p => p.characterName == chosen);
        bool spawnerIsTop = transform.position.y > 0f;
        
        // Try to get CPU-specific prefabs first, fallback to regular prefabs
        GameObject prefab = spawnerIsTop ? entry.downCPUPrefab : entry.upCPUPrefab;
        if (!prefab) prefab = entry.upCPUPrefab ?? entry.downCPUPrefab;
        
        // If no CPU prefabs, use regular prefabs and convert them
        if (!prefab)
        {
            prefab = spawnerIsTop ? entry.downPrefabWithPlayerInput : entry.upPrefabWithPlayerInput;
            if (!prefab) prefab = entry.upPrefabWithPlayerInput ?? entry.downPrefabWithPlayerInput;
        }
        
        if (!prefab) 
        { 
            Debug.LogError($"[{name}] No prefab mapped for CPU '{chosen}'"); 
            return; 
        }

        // 2) Instantiate the CPU player
        GameObject cpuInstance = Instantiate(prefab);
        
        if (spawnPoint) 
            cpuInstance.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // 3) Convert to CPU if it's a regular player prefab
        ConvertToCPUPlayer(cpuInstance);

        // 4) Set up CPU-specific components
        SetupCPUComponents(cpuInstance);

        Debug.Log($"[{cpuInstance.name}] CPU {slot} spawned for character '{chosen}'");
    }

    private void ConvertToCPUPlayer(GameObject playerInstance)
    {
        // Remove or disable PlayerInput if present
        var playerInput = playerInstance.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
            // Don't destroy it completely as other components might reference it
        }

        // Remove or disable PlayerMovement if present
        var playerMovement = playerInstance.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        // Remove or disable PingPongMovement if present (fixes null move action warnings)
        var pingPongMovement = playerInstance.GetComponent<PingPongMovement>();
        if (pingPongMovement != null)
        {
            pingPongMovement.enabled = false;
        }

        // Add CPU components if not already present
        if (playerInstance.GetComponent<CPUPlayer>() == null)
        {
            playerInstance.AddComponent<CPUPlayer>();
        }

        if (playerInstance.GetComponent<CPUMovement>() == null)
        {
            playerInstance.AddComponent<CPUMovement>();
        }
    }

    private void SetupCPUComponents(GameObject playerInstance)
    {
        // Configure CPUPlayer
        var cpuPlayer = playerInstance.GetComponent<CPUPlayer>();
        if (cpuPlayer != null)
        {
            cpuPlayer.difficulty = CharacterSelect.GetCPUDifficulty;
            
            // Auto-find references
            cpuPlayer.ball = FindFirstObjectByType<BallPhysics2D>();
            cpuPlayer.cpu_movement = playerInstance.GetComponent<CPUMovement>();
            cpuPlayer.player_owner = playerInstance.GetComponent<PlayerOwner>();

            // Ensure PlayerOwner is set correctly for P2
            if (cpuPlayer.player_owner != null)
            {
                cpuPlayer.player_owner.player_id = PlayerId.P2;
            }
        }

        // Configure CPUMovement 
        var cpuMovement = playerInstance.GetComponent<CPUMovement>();
        if (cpuMovement != null)
        {
            // Copy movement parameters from PlayerMovement if available
            var originalMovement = playerInstance.GetComponent<PlayerMovement>();
            if (originalMovement != null)
            {
                cpuMovement.max_speed = originalMovement.max_speed;
                cpuMovement.acceleration = originalMovement.acceleration;
                cpuMovement.deceleration = originalMovement.deceleration;
                cpuMovement.ping_pong_target = originalMovement.ping_pong_target;
            }
        }
    }

    public sealed class InputCleanup : MonoBehaviour
    {
        private PlayerInput _pi;
        private InputDevice[] _devices;
        private bool _unpaired;

        public void Initialize(PlayerInput pi, InputDevice[] devices)
        {
            _pi = pi;
            _devices = devices ?? System.Array.Empty<InputDevice>();
            _unpaired = false;
        }

        /// <summary>Safe to call multiple times; guards against invalid user and double-unpair.</summary>
        public void UnpairNow()
        {
            if (_unpaired) return;

            try
            {
                if (_pi != null)
                {
                    var user = _pi.user;
                    // Guard: InputUser throws if invalid; use valid flag
                    if (user.valid)
                    {
                        user.UnpairDevices();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[InputCleanup] Unpair skipped: {e.Message}");
            }
            finally
            {
                _unpaired = true;
            }
        }

        void OnDestroy()
        {
            // Unpair this user's devices so they can be reclaimed next match/scene
            UnpairNow();

            // Free claims (claims are per-scene; remove regardless)
            foreach (var d in _devices)
            {
                if (d == null) continue;
                s_claimed.Remove(d);
            }
        }
    }
}
