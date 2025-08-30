using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

[DisallowMultipleComponent]
public sealed class PlayerInputCleanup : MonoBehaviour
{
    private PlayerInput _pi;
    private bool _unpaired;

    public void Initialize(PlayerInput pi)
    {
        _pi = pi;
        _unpaired = false;
    }

    public void UnpairNow()
    {
        if (_unpaired) return;
        try
        {
            if (_pi != null && _pi.user.valid)
                _pi.user.UnpairDevices();
        }
        catch (System.Exception e)
        {
            Debug.Log($"[PlayerInputCleanup] Unpair skipped: {e.Message}");
        }
        finally { _unpaired = true; }
    }

    void OnDestroy() => UnpairNow();
}
