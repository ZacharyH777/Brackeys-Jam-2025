using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputManager : MonoBehaviour
{
    public Transform[] spawnPoints;

    void OnEnable() => PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
    void OnDisable()
    {
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput pi)
    {
        int i = pi.playerIndex;
        if (spawnPoints != null && i < spawnPoints.Length && spawnPoints[i])
            pi.transform.position = spawnPoints[i].position;

        pi.gameObject.name = $"Player{i + 1} ({pi.currentControlScheme})";
        Debug.Log($"Joined: Player{i + 1} on {pi.devices[0].displayName}");
    }
}
