using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class CharacterSelect
{
    public static string p1_character = "";
    public static string p2_character = "";

    private static string singleplayerScene = "Arcade";
    private static string multiplayerScene = "Multiplayer";
    public static bool is_singleplayer = false;

    public static void play_singleplayer()
    {
        if (!is_singleplayer || string.IsNullOrEmpty(p1_character)) return;

        CaptureDevicesForPlayers(); 
        SceneManager.LoadScene(singleplayerScene);
    }

    public static void play_multiplayer()
    {
        if (is_singleplayer || string.IsNullOrEmpty(p1_character) || string.IsNullOrEmpty(p2_character)) return;

        CaptureDevicesForPlayers();
        SceneManager.LoadScene(multiplayerScene);
    }

    public static bool BothSelected =>
        !string.IsNullOrEmpty(p1_character) && !string.IsNullOrEmpty(p2_character);


    static void CaptureDevicesForPlayers()
    {
        EnsureDeviceRegistry();

        var p1 = PlayerInput.all.FirstOrDefault(p => p.playerIndex == 0);
        var p2 = PlayerInput.all.FirstOrDefault(p => p.playerIndex == 1);

        if (p1 != null)
        {
            var scheme = p1.currentControlScheme;
            var devices = p1.user.pairedDevices.Where(IsRelevantInputDevice).ToArray();
            DeviceRegistry.SetDevices(0, scheme, devices);
        }

        if (p2 != null)
        {
            var scheme = p2.currentControlScheme;
            var devices = p2.user.pairedDevices.Where(IsRelevantInputDevice).ToArray();
            DeviceRegistry.SetDevices(1, scheme, devices);
        }

    }

    static bool IsRelevantInputDevice(InputDevice d)
        => d is Gamepad || d is Keyboard || d is Mouse;

    static void EnsureDeviceRegistry()
    {
        if (DeviceRegistry.Instance != null) return;
        var go = new GameObject("DeviceRegistry");
        go.AddComponent<DeviceRegistry>(); 
    }

}
