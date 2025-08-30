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
    public static bool is_singleplayer = true;
    
    // CPU player settings for single player mode
    public static bool p2_is_cpu = false;
    public static CPUDifficulty cpu_difficulty = CPUDifficulty.Medium;

    public static void play_singleplayer()
    {
        if (!is_singleplayer || string.IsNullOrEmpty(p1_character)) return;

        // In single player mode, P2 is controlled by CPU
        p2_is_cpu = true;
        
        // Set P2 character to same as P1 for CPU mode if none selected
        if (string.IsNullOrEmpty(p2_character))
        {
            p2_character = p1_character; // CPU uses same character as player
        }

        CaptureDevicesForPlayers(); 
        SceneManager.LoadScene(singleplayerScene);
    }

    public static void play_multiplayer()
    {
        if (is_singleplayer || string.IsNullOrEmpty(p1_character) || string.IsNullOrEmpty(p2_character)) return;

        // In multiplayer mode, both players are human
        p2_is_cpu = false;

        CaptureDevicesForPlayers();
        SceneManager.LoadScene(multiplayerScene);
    }

    public static bool BothSelected =>
        !string.IsNullOrEmpty(p1_character) && !string.IsNullOrEmpty(p2_character);
    
    // Helper methods for CPU functionality
    public static void SetCPUDifficulty(CPUDifficulty difficulty)
    {
        cpu_difficulty = difficulty;
    }
    
    public static bool IsP2CPU => p2_is_cpu;
    
    public static CPUDifficulty GetCPUDifficulty => cpu_difficulty;


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

        // Only capture P2 devices if P2 is human (not CPU)
        if (p2 != null && !p2_is_cpu)
        {
            var scheme = p2.currentControlScheme;
            var devices = p2.user.pairedDevices.Where(IsRelevantInputDevice).ToArray();
            DeviceRegistry.SetDevices(1, scheme, devices);
        }
        else if (p2_is_cpu)
        {
            // Clear any existing P2 devices since CPU doesn't need input devices
            DeviceRegistry.SetDevices(1, "CPU", new InputDevice[0]);
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
