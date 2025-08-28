// SpawnPublicObject.cs
using UnityEngine;

public class SpawnCharacterSelect : MonoBehaviour
{
    [Header("Spawn Object")]
    [Tooltip("Prefab to spawn")]
    public GameObject prefab_object;

    [Header("Spawn Where")]
    [Tooltip("Transform used for position and rotation")]
    public Transform spawn_point;
    [Tooltip("Optional parent for spawned object")]
    public Transform parent_after_spawn;

    [Header("Spawn When")]
    [Tooltip("Spawn at start of scene")]
    public bool spawn_on_start = true;

    /*
    Spawn on start when enabled.
    */
    void Start()
    {
        if (spawn_on_start)
        {
            Spawn();
        }
    }

    /*
    Instantiate the prefab at a chosen location and rotation and optionally parent it.
    @return The spawned game object or null when no prefab is assigned.
    */
    public GameObject Spawn()
    {
        if (prefab_object == null)
        {
            Debug.LogWarning("No prefab assigned on " + name);
            return null;
        }

        Vector3 spawn_position;
        Quaternion spawn_rotation;

        if (spawn_point != null)
        {
            spawn_position = spawn_point.position;
            spawn_rotation = spawn_point.rotation;
        }
        else
        {
            spawn_position = transform.position;
            spawn_rotation = transform.rotation;
        }

        GameObject spawned = Instantiate(prefab_object, spawn_position, spawn_rotation, parent_after_spawn);
        return spawned;
    }

    /*
    Spawn from the context menu for quick testing in the editor.
    */
    [ContextMenu("Spawn Now")]
    private void SpawnFromContextMenu()
    {
        Spawn();
    }
}
