using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DeviceRegistry : MonoBehaviour
{
    public static DeviceRegistry Instance { get; private set; }

    // Unity Control Schemes to handle rebinding controllers across scenes
    private string unity_p1Scheme, unity_p2Scheme;
    private InputDevice[] _p1Devices = System.Array.Empty<InputDevice>();
    private InputDevice[] _p2Devices = System.Array.Empty<InputDevice>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void SetDevices(int playerIndex, string controlScheme, params InputDevice[] devices)
    {
        if (Instance == null) return;
        if (playerIndex == 0)
        {
            Instance.unity_p1Scheme = controlScheme; Instance._p1Devices = devices;
        }
        else if (playerIndex == 1)
        {
            Instance.unity_p2Scheme = controlScheme; Instance._p2Devices = devices;
        }
    }

    public static bool TryGetDevices(int playerIndex, out string controlScheme, out InputDevice[] devices)
    {
        controlScheme = null; devices = null;
        if (Instance == null) return false;

        if (playerIndex == 0)
        {
            controlScheme = Instance.unity_p1Scheme; devices = Instance._p1Devices; return devices != null && devices.Length > 0;
        }
        if (playerIndex == 1)
        {
            controlScheme = Instance.unity_p2Scheme; devices = Instance._p2Devices; return devices != null && devices.Length > 0;
        }
        return false;
    }
}
