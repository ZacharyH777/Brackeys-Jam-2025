using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;

/*
 * Global 2-player manager that persists across scenes.
 * First device to press joins as P1, second as P2.
 * Keeps player indices, device pairing, and character selection across scenes.
 * Disables joining once both slots are filled.
 */
[RequireComponent(typeof(PlayerInputManager))]
public class GameInputManager : MonoBehaviour
{
    [Header("Persist")]
    [Tooltip("Keep this across scenes.")]
    public bool persist_across_scenes = true;

    [Tooltip("Mark players DontDestroyOnLoad.")]
    public bool persist_players = true;

    [Header("Spawns")]
    [Tooltip("Reposition on scene load.")]
    public bool move_on_scene_load = true;

    [Tooltip("Tag for P1 spawn.")]
    public string p1_spawn_tag = "P1_Spawn";

    [Tooltip("Tag for P2 spawn.")]
    public string p2_spawn_tag = "P2_Spawn";

    [Header("Optional")]
    [Tooltip("Override player prefab.")]
    public GameObject player_prefab_override;

    [Tooltip("Fallback P1 spawn.")]
    public Transform p1_spawn;

    [Tooltip("Fallback P2 spawn.")]
    public Transform p2_spawn;

    [System.Serializable]
    public class player_selection_data
    {
        [Tooltip("Character prefab.")]
        public GameObject character_prefab;
    }

    [Header("Selection")]
    [Tooltip("P1 selection.")]
    public player_selection_data p1_selection;

    [Tooltip("P2 selection.")]
    public player_selection_data p2_selection;

    private static GameInputManager instance;
    private PlayerInputManager player_input_manager;
    private PlayerInput player1;
    private PlayerInput player2;
    private bool keyboard_taken = false;
    private int gamepad_count = 0;

    /* Unity */
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (persist_across_scenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        player_input_manager = GetComponent<PlayerInputManager>();

        var existing_players = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (int i = 0; i < existing_players.Length; i++)
        {
            kick(existing_players[i]);
        }

        if (player_prefab_override != null)
        {
            player_input_manager.playerPrefab = player_prefab_override;
        }

        player_input_manager.onPlayerJoined += handle_player_joined;
        player_input_manager.onPlayerLeft += handle_player_left;

        SceneManager.sceneLoaded += handle_scene_loaded;

        player_input_manager.EnableJoining();
    }

    /* Unity */
    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (player_input_manager != null)
        {
            player_input_manager.onPlayerJoined -= handle_player_joined;
            player_input_manager.onPlayerLeft -= handle_player_left;
        }

        SceneManager.sceneLoaded -= handle_scene_loaded;
    }

    /* Events */
    private void handle_scene_loaded(Scene scene, LoadSceneMode mode)
    {
        if (move_on_scene_load)
        {
            reposition_players_by_tags();
        }

        apply_selection(player1, p1_selection);
        apply_selection(player2, p2_selection);
    }

    /* Events */
    private void handle_player_joined(PlayerInput player_input)
    {
        bool uses_keyboard = false;
        bool uses_gamepad = false;

        var devices = player_input.devices;
        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i] is Keyboard) uses_keyboard = true;
            if (devices[i] is Gamepad) uses_gamepad = true;
        }

        if (uses_keyboard && keyboard_taken)
        {
            kick(player_input);
            return;
        }

        int current_count = 0;
        if (player1 != null) current_count += 1;
        if (player2 != null) current_count += 1;
        if (current_count >= 2)
        {
            kick(player_input);
            player_input_manager.DisableJoining();
            return;
        }

        if (player1 == null)
        {
            player1 = player_input;
            apply_spawn(player_input, p1_spawn);
            set_nice_name(player_input, "P1");
            if (persist_players) mark_persistent(player_input);
            apply_selection(player1, p1_selection);
        }
        else
        {
            player2 = player_input;
            apply_spawn(player_input, p2_spawn);
            set_nice_name(player_input, "P2");
            if (persist_players) mark_persistent(player_input);
            apply_selection(player2, p2_selection);
        }

        if (uses_keyboard) keyboard_taken = true;
        if (uses_gamepad) gamepad_count += 1;

        int after_count = 0;
        if (player1 != null) after_count += 1;
        if (player2 != null) after_count += 1;
        if (after_count >= 2) player_input_manager.DisableJoining();
    }

    /* Events */
    private void handle_player_left(PlayerInput player_input)
    {
        bool was_keyboard = false;
        bool was_gamepad = false;

        var devices = player_input.devices;
        for (int i = 0; i < devices.Count; i++)
        {
            if (devices[i] is Keyboard) was_keyboard = true;
            if (devices[i] is Gamepad) was_gamepad = true;
        }

        if (player1 == player_input) player1 = null;
        if (player2 == player_input) player2 = null;

        if (was_keyboard) keyboard_taken = false;
        if (was_gamepad)
        {
            gamepad_count -= 1;
            if (gamepad_count < 0) gamepad_count = 0;
        }

        int count = 0;
        if (player1 != null) count += 1;
        if (player2 != null) count += 1;
        if (count < 2) player_input_manager.EnableJoining();
    }

    /* API */
    public static GameInputManager get()
    {
        return instance;
    }

    /* API */
    public void set_player_character(int player_index, GameObject character_prefab)
    {
        if (player_index == 0)
        {
            if (p1_selection == null) p1_selection = new player_selection_data();
            p1_selection.character_prefab = character_prefab;
            apply_selection(player1, p1_selection);
        }
        else if (player_index == 1)
        {
            if (p2_selection == null) p2_selection = new player_selection_data();
            p2_selection.character_prefab = character_prefab;
            apply_selection(player2, p2_selection);
        }
    }

    /* Util */
    private void apply_selection(PlayerInput player_input, player_selection_data selection)
    {
        if (player_input == null) return;
        if (selection == null) return;

        if (selection.character_prefab != null)
        {
            var root = player_input.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                Destroy(child.gameObject);
            }
            var spawned = Instantiate(selection.character_prefab, root);
            spawned.transform.localPosition = Vector3.zero;
            spawned.transform.localRotation = Quaternion.identity;
            spawned.transform.localScale = Vector3.one;
        }
    }

    /* Util */
    private void mark_persistent(PlayerInput player_input)
    {
        if (player_input == null) return;
        DontDestroyOnLoad(player_input.gameObject);
    }

    /* Util */
    private void reposition_players_by_tags()
    {
        Transform p1_target = null;
        Transform p2_target = null;

        var p1_go = GameObject.FindWithTag(p1_spawn_tag);
        if (p1_go != null) p1_target = p1_go.transform;

        var p2_go = GameObject.FindWithTag(p2_spawn_tag);
        if (p2_go != null) p2_target = p2_go.transform;

        if (player1 != null) apply_spawn(player1, p1_target);
        if (player2 != null) apply_spawn(player2, p2_target);
    }

    /* Util */
    private void kick(PlayerInput player_input)
    {
        if (player_input != null)
        {
            if (player_input.user.valid)
            {
                player_input.user.UnpairDevicesAndRemoveUser();
            }
            Destroy(player_input.gameObject);
        }
    }

    /* Util */
    private static void apply_spawn(PlayerInput player_input, Transform spawn)
    {
        if (player_input == null) return;
        if (spawn != null)
        {
            player_input.transform.position = spawn.position;
            player_input.transform.rotation = spawn.rotation;
        }
    }

    /* Util */
    private static void set_nice_name(PlayerInput player_input, string label)
    {
        if (player_input == null) return;

        string devices = "";
        var list = player_input.devices;
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) devices += ",";
            devices += list[i].displayName;
        }

        player_input.gameObject.name = label + " (" + devices + ")";

        /* playerIndex is read-only; assigned by join order. */
    }
}
