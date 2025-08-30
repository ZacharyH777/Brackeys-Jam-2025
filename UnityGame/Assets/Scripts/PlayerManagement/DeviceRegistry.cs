using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DeviceRegistry : MonoBehaviour
{
    public static DeviceRegistry Instance { get; private set; }

    // Persisted mapping (across scenes)
    private string unity_p1Scheme, unity_p2Scheme;
    private InputDevice[] _p1Devices = System.Array.Empty<InputDevice>();
    private InputDevice[] _p2Devices = System.Array.Empty<InputDevice>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        // Treat any “gone/unavailable” change the same: prune from our sets.
        switch (change)
        {
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Disabled:
                PruneDevice(device);
                break;

            // You can ignore other changes (Added/Reconnected/Enabled/etc.)
        }
    }

    private void PruneDevice(InputDevice device)
    {
        if (device == null) return;

        _p1Devices = (_p1Devices ?? System.Array.Empty<InputDevice>())
            .Where(d => d != null && d.added && d != device)
            .ToArray();

        _p2Devices = (_p2Devices ?? System.Array.Empty<InputDevice>())
            .Where(d => d != null && d.added && d != device)
            .ToArray();

        if (_p1Devices.Length == 0) unity_p1Scheme = null;
        if (_p2Devices.Length == 0) unity_p2Scheme = null;
    }

    // ---- Public API (unchanged) ----
    public static void SetDevices(int playerIndex, string controlScheme, params InputDevice[] devices)
    {
        if (Instance == null) return;
        if (playerIndex == 0)
        {
            Instance.unity_p1Scheme = controlScheme;
            Instance._p1Devices = devices ?? System.Array.Empty<InputDevice>();
        }
        else if (playerIndex == 1)
        {
            Instance.unity_p2Scheme = controlScheme;
            Instance._p2Devices = devices ?? System.Array.Empty<InputDevice>();
        }
    }

    public static bool TryGetDevices(int playerIndex, out string controlScheme, out InputDevice[] devices)
    {
        controlScheme = null; devices = null;
        if (Instance == null) return false;

        if (playerIndex == 0)
        {
            controlScheme = Instance.unity_p1Scheme;
            devices = Instance._p1Devices ?? System.Array.Empty<InputDevice>();
            return devices.Length > 0;
        }
        if (playerIndex == 1)
        {
            controlScheme = Instance.unity_p2Scheme;
            devices = Instance._p2Devices ?? System.Array.Empty<InputDevice>();
            return devices.Length > 0;
        }
        return false;
    }

    // ---- Helpers you’re already calling from the spawner/loop ----
    public static void ResetAll()
    {
        if (Instance == null) return;
        Instance.unity_p1Scheme = null;
        Instance.unity_p2Scheme = null;
        Instance._p1Devices = System.Array.Empty<InputDevice>();
        Instance._p2Devices = System.Array.Empty<InputDevice>();
    }

    public void ValidateAndPrune()
    {
        _p1Devices = (_p1Devices ?? System.Array.Empty<InputDevice>())
            .Where(d => d != null && d.added)
            .ToArray();
        _p2Devices = (_p2Devices ?? System.Array.Empty<InputDevice>())
            .Where(d => d != null && d.added)
            .ToArray();

        if (_p1Devices.Length == 0) unity_p1Scheme = null;
        if (_p2Devices.Length == 0) unity_p2Scheme = null;
    }
}
