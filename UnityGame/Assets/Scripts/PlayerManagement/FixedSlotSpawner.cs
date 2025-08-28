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
    }

    [Header("Slot")]
    public Slot slot = Slot.P1;

    [Header("Characters (name -> UP/DOWN prefabs)")]
    public CharacterPrefab[] characterPrefabs;

    [Header("Input (override)")]
    [Tooltip("Optional override; if null, prefab's PlayerInput.actions are cloned anyway so each player has an independent asset.")]
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

    [Header("Spawn")]
    public Transform spawnPoint;

    /* --- shared across both spawners this scene to avoid double pairing --- */
    private static readonly HashSet<InputDevice> s_claimed = new HashSet<InputDevice>();

    void Start()
    {
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

        // 2) Get desired devices from DeviceRegistry
        string scheme;
        InputDevice[] devices;
        bool haveBundle = DeviceRegistry.TryGetDevices((int)slot, out scheme, out devices);

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

        // 3) Remove devices already claimed by the other player
        if (haveBundle)
        {
            devices = devices.Where(d => !s_claimed.Contains(d)).Distinct().ToArray();
            if (devices.Length == 0) haveBundle = false; // nothing left, will fallback
        }

        // 4) Fallbacks (make sure P1/P2 don’t share)
        if (!haveBundle)
        {
            if (preferGamepadsOnFallback && Gamepad.all.Count > 0)
            {
                var freePad = Gamepad.all.FirstOrDefault(p => !s_claimed.Contains(p));
                if (freePad != null)
                {
                    scheme  = gamepadScheme;
                    devices = new InputDevice[] { freePad };
                    haveBundle = true;
                }
            }

            if (!haveBundle && Keyboard.current != null)
            {
                var list = new List<InputDevice> { Keyboard.current };
                if (autoAddMouseForKeyboardSchemes && Mouse.current != null && !s_claimed.Contains(Mouse.current))
                    list.Add(Mouse.current);

                // If mouse is already claimed, still allow keyboard-only
                scheme  = (list.Any(d => d is Mouse)) ? keyboardMouseScheme : (string.IsNullOrEmpty(keyboardOnlyScheme) ? keyboardMouseScheme : keyboardOnlyScheme);
                devices = list.Where(d => !s_claimed.Contains(d)).ToArray();
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

        if ((int)slot == 0) pi.camera.rect = new Rect(0f, 0f, 0.5f, 1f);
        if ((int)slot == 1) pi.camera.rect = new Rect(0.5f, 0f, 0.5f, 1f);

        // --- Ensure each PlayerInput gets its OWN cloned action asset ---
        // Source: override 'actions' if provided; otherwise clone the prefab's PlayerInput.actions
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
        // -----------------------------------------------------------------

        if (spawnPoint) pi.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // hard-enforce: only our devices
        pi.user.UnpairDevices();
        foreach (var d in devices) InputUser.PerformPairingWithDevice(d, pi.user);
        pi.SwitchCurrentControlScheme(scheme, devices);
        pi.neverAutoSwitchControlSchemes = true;

        // claim them so the other spawner cannot reuse
        foreach (var d in devices) s_claimed.Add(d);

        // Optional safety: remove any stray not in our list
        foreach (var d in pi.user.pairedDevices.ToArray())
            if (!devices.Contains(d)) pi.user.UnpairDevice(d);

        Debug.Log($"[{pi.name}] {slot} -> '{scheme}' | devices: {string.Join(", ", pi.user.pairedDevices.Select(d => d.displayName))}");
    }
}
