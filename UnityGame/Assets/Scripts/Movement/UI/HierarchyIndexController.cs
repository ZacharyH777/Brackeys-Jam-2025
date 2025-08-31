using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerInput))]
[AddComponentMenu("Utils/Hierarchy Index Controller UI Navigate Select Start")]
public sealed class HierarchyIndexController : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("If null uses this transform")]
    public Transform connected;

    [Header("Indexing")]
    [Min(0)] public int start_index = 0;
    [Tooltip("Wrap around child list")]
    public bool wrap_around = true;

    [Header("UI Input")]
    [Tooltip("Assign UI navigate")]
    public InputActionReference navigate_action_reference;
    [Tooltip("Assign UI submit")]
    public InputActionReference select_action_reference;
    [Tooltip("Assign game start")]
    public InputActionReference start_game_action_reference;
    [Tooltip("Assign UI cancel")]
    public InputActionReference cancel_action_reference;

    [Header("Cancel Hold")]
    [Tooltip("Seconds to hold cancel")]
    public float cancel_hold_seconds = 3f;
    [Tooltip("Scene to load on cancel")]
    public string back_out_scene_name = "";
    [Tooltip("Invoked on long cancel")]
    public UnityEvent on_long_cancel;

    [Header("Navigate Gate")]
    [Tooltip("Horizontal deadzone")]
    [Range(0f, 1f)] public float horizontal_deadzone = 0.5f;
    [Tooltip("Delay before repeat")]
    public float repeat_delay = 0.45f;
    [Tooltip("Interval between repeats")]
    public float repeat_rate = 0.10f;
    [Tooltip("Reset rotation and scale")]
    public bool reset_rotation_and_scale = false;

    [Header("Sprite Target")]
    [Tooltip("Tag for player one")]
    public string p1_spawn_tag = "P1_Spawn";
    [Tooltip("Tag for player two")]
    public string p2_spawn_tag = "P2_Spawn";
    [Tooltip("Optional explicit target")]
    public SpriteRenderer selection_sprite_target;

    [Header("Preview Sprites")]
    [Tooltip("Sprite for index 0")]
    public Sprite sprite_index_0;
    [Tooltip("Sprite for index 1")]
    public Sprite sprite_index_1;
    [Tooltip("Sprite for index 2")]
    public Sprite sprite_index_2;
    [Tooltip("Sprite for index 3")]
    public Sprite sprite_index_3;

    private int current_index;
    private PlayerInput player_input;
    private InputAction navigate_action;
    private InputAction select_action;
    private InputAction start_action;
    private InputAction cancel_action;

    private int held_direction;
    private float next_repeat_time = float.PositiveInfinity;
    private bool is_locked;
    private string last_selected_name = "";

    private bool cancel_hold_active;
    private float cancel_hold_start_time = -1f;

    private PlaySound play_sound;

    /*
    * Return the transform to control.
    * Uses connected when present else this transform.
    * @param none
    */
    private Transform SourceTransform
    {
        get
        {
            if (connected != null)
            {
                return connected;
            }
            return transform;
        }
    }

    /*
    * Return the parent of the source transform.
    * @param none
    */
    public Transform ParentTransform
    {
        get
        {
            Transform src = SourceTransform;
            if (src != null)
            {
                return src.parent;
            }
            return null;
        }
    }

    /*
    * Return the grandparent of the source transform.
    * @param none
    */
    public Transform GrandparentTransform
    {
        get
        {
            Transform p = ParentTransform;
            if (p != null)
            {
                return p.parent;
            }
            return null;
        }
    }

    /*
    * Initialize index, audio, actions, and sprite target.
    * @param none
    */
    void Awake()
    {
        current_index = Mathf.Max(0, start_index);
        player_input = GetComponent<PlayerInput>();

        GameObject audio_go = GameObject.FindGameObjectWithTag("Audio");
        if (audio_go != null)
        {
            play_sound = audio_go.GetComponent<PlaySound>();
        }

        Resolve_selection_sprite_target_by_tag();

        navigate_action = Resolve_action_from_reference(navigate_action_reference, player_input);
        if (navigate_action == null && player_input != null && player_input.actions != null)
        {
            navigate_action = player_input.actions.FindAction("UI/Navigate", false);
        }

        select_action = Resolve_action_from_reference(select_action_reference, player_input);
        if (select_action == null && player_input != null && player_input.actions != null)
        {
            select_action = player_input.actions.FindAction("UI/Submit", false);
        }

        start_action = Resolve_action_from_reference(start_game_action_reference, player_input);
        if (start_action == null && player_input != null && player_input.actions != null)
        {
            start_action = player_input.actions.FindAction("UI/Start", false);
        }

        cancel_action = Resolve_action_from_reference(cancel_action_reference, player_input);
        if (cancel_action == null && player_input != null && player_input.actions != null)
        {
            cancel_action = player_input.actions.FindAction("UI/Cancel", false);
        }
    }

    /*
    * Safety re-resolve sprite target after PlayerInput is ready.
    * @param none
    */
    void Start()
    {
        if (selection_sprite_target == null)
        {
            Resolve_selection_sprite_target_by_tag();
        }
    }

    /*
    * Enable actions and seed current index.
    * @param none
    */
    void OnEnable()
    {
        if (navigate_action != null)
        {
            if (!navigate_action.enabled)
            {
                navigate_action.Enable();
            }
            navigate_action.performed += On_navigate_performed;
            navigate_action.canceled += On_navigate_canceled;
        }

        if (select_action != null)
        {
            if (!select_action.enabled)
            {
                select_action.Enable();
            }
            select_action.performed += On_select_performed;
        }

        if (start_action != null)
        {
            if (!start_action.enabled)
            {
                start_action.Enable();
            }
            start_action.performed += On_start_performed;
        }

        if (cancel_action != null)
        {
            if (!cancel_action.enabled)
            {
                cancel_action.Enable();
            }
            cancel_action.started += On_cancel_started;
            cancel_action.canceled += On_cancel_canceled;
        }

        Transform gp = GrandparentTransform;
        if (gp != null)
        {
            if (gp.childCount > 0)
            {
                current_index = Mathf.Clamp(start_index, 0, gp.childCount - 1);
            }
        }

        Update_sprite_preview_from_current();
    }

    /*
    * Disable actions and clear held state.
    * @param none
    */
    void OnDisable()
    {
        if (navigate_action != null)
        {
            navigate_action.performed -= On_navigate_performed;
            navigate_action.canceled -= On_navigate_canceled;
            if (navigate_action.enabled)
            {
                navigate_action.Disable();
            }
        }

        if (select_action != null)
        {
            select_action.performed -= On_select_performed;
            if (select_action.enabled)
            {
                select_action.Disable();
            }
        }

        if (start_action != null)
        {
            start_action.performed -= On_start_performed;
            if (start_action.enabled)
            {
                start_action.Disable();
            }
        }

        if (cancel_action != null)
        {
            cancel_action.started -= On_cancel_started;
            cancel_action.canceled -= On_cancel_canceled;
            if (cancel_action.enabled)
            {
                cancel_action.Disable();
            }
        }

        held_direction = 0;
        next_repeat_time = float.PositiveInfinity;
        cancel_hold_active = false;
        cancel_hold_start_time = -1f;
    }

    /*
    * Drive repeat navigation while a direction is held.
    * @param none
    */
    void Update()
    {
        if (is_locked)
        {
            return;
        }

        if (!Input_allowed_for_this_instance())
        {
            return;
        }

        if (held_direction != 0)
        {
            if (Time.unscaledTime >= next_repeat_time)
            {
                Step_index(held_direction);
                Reparent_connected_to_current();
                next_repeat_time = Time.unscaledTime + repeat_rate;
            }
        }
    }

    /*
    * Handle navigate input and start repeat timer.
    * @param context Input context
    */
    private void On_navigate_performed(InputAction.CallbackContext context)
    {
        if (is_locked)
        {
            return;
        }

        if (!Input_allowed_for_this_instance())
        {
            return;
        }

        Vector2 v = context.ReadValue<Vector2>();
        int direction = 0;

        if (v.x > horizontal_deadzone)
        {
            direction = 1;
        }
        else
        {
            if (v.x < -horizontal_deadzone)
            {
                direction = -1;
            }
        }

        if (direction != 0)
        {
            if (direction != held_direction)
            {
                held_direction = direction;
                Step_index(held_direction);
                Reparent_connected_to_current();
                next_repeat_time = Time.unscaledTime + repeat_delay;
            }
        }
        else
        {
            held_direction = 0;
            next_repeat_time = float.PositiveInfinity;
        }
    }

    /*
    * Stop repeating on navigate release.
    * @param context Input context
    */
    private void On_navigate_canceled(InputAction.CallbackContext context)
    {
        held_direction = 0;
        next_repeat_time = float.PositiveInfinity;
    }

    /*
    * Select current option, set preview, and lock input.
    * @param context Input context
    */
    private void On_select_performed(InputAction.CallbackContext context)
    {
        if (is_locked)
        {
            return;
        }

        if (!Input_allowed_for_this_instance())
        {
            return;
        }

        Transform pick = Get_current_grandparent_child();
        if (pick == null)
        {
            return;
        }

        string chosen_name = pick.name;

        if (play_sound != null)
        {
            play_sound.sfx_menu_select();
        }

        if (CharacterSelect.is_singleplayer)
        {
            CharacterSelect.p1_character = chosen_name;
        }
        else
        {
            int idx = -1;
            if (player_input != null)
            {
                idx = player_input.playerIndex;
            }

            if (idx == 0)
            {
                CharacterSelect.p1_character = chosen_name;
            }
            else
            {
                if (idx == 1)
                {
                    CharacterSelect.p2_character = chosen_name;
                }
                else
                {
                    if (string.IsNullOrEmpty(CharacterSelect.p1_character))
                    {
                        CharacterSelect.p1_character = chosen_name;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(CharacterSelect.p2_character))
                        {
                            CharacterSelect.p2_character = chosen_name;
                        }
                    }
                }
            }
        }

        Update_sprite_preview_from_current();

        is_locked = true;
        last_selected_name = chosen_name;
        Reparent_connected_to_current();

        int player_index_log = -1;
        if (player_input != null)
        {
            player_index_log = player_input.playerIndex;
        }

        Debug.Log("Player " + player_index_log + " selected " + chosen_name);
    }

    /*
    * Start the game in selected mode.
    * @param context Input context
    */
    private void On_start_performed(InputAction.CallbackContext context)
    {
        if (CharacterSelect.is_singleplayer)
        {
            CharacterSelect.play_singleplayer();
        }
        else
        {
            CharacterSelect.play_multiplayer();
        }
    }

    /*
    * Begin measuring cancel hold time.
    * @param context Input context
    */
    private void On_cancel_started(InputAction.CallbackContext context)
    {
        if (!Input_allowed_for_this_instance())
        {
            return;
        }

        cancel_hold_active = true;
        cancel_hold_start_time = Time.unscaledTime;

        if (play_sound != null)
        {
            play_sound.sfx_menu_cancel();
        }
    }

    /*
    * Finish cancel. Short tap clears selection. Long hold backs out.
    * @param context Input context
    */
    private void On_cancel_canceled(InputAction.CallbackContext context)
    {
        if (!Input_allowed_for_this_instance())
        {
            cancel_hold_active = false;
            cancel_hold_start_time = -1f;
            return;
        }

        float held = 0f;
        if (cancel_hold_active)
        {
            held = Time.unscaledTime - cancel_hold_start_time;
        }

        cancel_hold_active = false;
        cancel_hold_start_time = -1f;

        if (held >= cancel_hold_seconds)
        {
            Trigger_back_out();
            return;
        }

        if (is_locked)
        {
            if (CharacterSelect.is_singleplayer)
            {
                CharacterSelect.p1_character = "";
            }
            else
            {
                int idx = -1;
                if (player_input != null)
                {
                    idx = player_input.playerIndex;
                }

                if (idx == 0)
                {
                    CharacterSelect.p1_character = "";
                }
                else
                {
                    if (idx == 1)
                    {
                        CharacterSelect.p2_character = "";
                    }
                    else
                    {
                        if (CharacterSelect.p1_character == last_selected_name)
                        {
                            CharacterSelect.p1_character = "";
                        }

                        if (CharacterSelect.p2_character == last_selected_name)
                        {
                            CharacterSelect.p2_character = "";
                        }
                    }
                }
            }

            is_locked = false;
            held_direction = 0;
            next_repeat_time = float.PositiveInfinity;
            last_selected_name = "";
            Debug.Log("Selection canceled and navigation unlocked");
        }
    }

    /*
    * Perform back out on long cancel.
    * @param none
    */
    private void Trigger_back_out()
    {
        if (!string.IsNullOrEmpty(back_out_scene_name))
        {
            SceneManager.LoadScene(back_out_scene_name);
            return;
        }

        if (on_long_cancel != null)
        {
            on_long_cancel.Invoke();
            return;
        }

        Debug.LogWarning("Back out requested but no scene or event set");
    }

    /*
    * Move current index by delta with wrap or clamp.
    * @param delta Signed step amount
    */
    public void Step_index(int delta)
    {
        int count = 0;
        Transform gp = GrandparentTransform;
        if (gp != null)
        {
            count = gp.childCount;
        }

        if (play_sound != null)
        {
            play_sound.sfx_menu_move();
        }

        if (count <= 0)
        {
            current_index = Mathf.Max(0, current_index + delta);
            return;
        }

        int next = current_index + delta;
        if (wrap_around)
        {
            next = next % count;
            if (next < 0)
            {
                next = next + count;
            }
        }
        else
        {
            next = Mathf.Clamp(next, 0, count - 1);
        }

        current_index = next;
    }

    /*
    * Get the current child under the grandparent.
    * @param none
    */
    public Transform Get_current_grandparent_child()
    {
        Transform gp = GrandparentTransform;
        if (gp == null)
        {
            Debug.LogWarning("No grandparent for source");
            return null;
        }

        int count = gp.childCount;
        if (count == 0)
        {
            Debug.LogWarning("Grandparent has no children");
            return null;
        }

        int pick = Mathf.Clamp(current_index, 0, count - 1);
        return gp.GetChild(pick);
    }

    /*
    * Reparent the controlled transform to the current selection.
    * Optionally reset local rotation and scale.
    * Also refresh preview sprite from index.
    * @param none
    */
    public Transform Reparent_connected_to_current()
    {
        Transform target = Get_current_grandparent_child();
        Transform src = SourceTransform;

        if (target == null)
        {
            return null;
        }

        if (src == null)
        {
            return null;
        }

        src.SetParent(target, false);
        src.localPosition = Vector3.zero;

        if (reset_rotation_and_scale)
        {
            src.localRotation = Quaternion.identity;
            src.localScale = Vector3.one;
        }

        Update_sprite_preview_from_current();
        return target;
    }

    /*
    * Resolve an action from a reference using the owner asset.
    * @param reference Input action reference
    * @param owner Player input
    */
    private static InputAction Resolve_action_from_reference(InputActionReference reference, PlayerInput owner)
    {
        if (reference == null)
        {
            return null;
        }

        if (reference.action == null)
        {
            return null;
        }

        InputActionAsset asset = null;
        if (owner != null)
        {
            asset = owner.actions;
        }

        if (asset != null)
        {
            InputAction by_id = asset.FindAction(reference.action.id);
            if (by_id != null)
            {
                return by_id;
            }
        }

        return reference.action;
    }

    /*
    * Only allow input from player one in singleplayer.
    * @param none
    */
    private bool Input_allowed_for_this_instance()
    {
        if (!CharacterSelect.is_singleplayer)
        {
            return true;
        }

        int idx = 0;
        if (player_input != null)
        {
            idx = player_input.playerIndex;
        }

        if (idx == 0)
        {
            return true;
        }
        return false;
    }

    /*
    * Resolve the selection sprite target by tag using player index.
    * @param none
    */
    private void Resolve_selection_sprite_target_by_tag()
    {
        if (selection_sprite_target != null)
        {
            return;
        }

        int idx = -1;
        if (player_input != null)
        {
            idx = player_input.playerIndex;
        }

        string lookup_tag = "";
        if (idx == 0)
        {
            lookup_tag = p1_spawn_tag;
        }
        else
        {
            lookup_tag = p2_spawn_tag;
        }

        if (string.IsNullOrEmpty(lookup_tag))
        {
            return;
        }

        GameObject found = GameObject.FindGameObjectWithTag(lookup_tag);
        if (found == null)
        {
            Debug.LogWarning("Selection sprite target was not found");
            return;
        }

        selection_sprite_target = found.GetComponentInChildren<SpriteRenderer>(true);
        if (selection_sprite_target == null)
        {
            Debug.LogWarning("Selection sprite target is missing SpriteRenderer");
        }
    }

    /*
    * Update preview sprite using the current index.
    * Uses four public sprites 0 to 3.
    * @param none
    */
    private void Update_sprite_preview_from_current()
    {
        if (selection_sprite_target == null)
        {
            return;
        }

        Sprite s = Get_preview_sprite_for_index(current_index);
        if (s == null)
        {
            return;
        }

        selection_sprite_target.sprite = s;
    }

    /*
    * Map an index to one of four public sprites.
    * Clamps to 0..3.
    * @param index Zero based index
    */
    private Sprite Get_preview_sprite_for_index(int index)
    {
        int clamped = index;
        if (clamped < 0)
        {
            clamped = 0;
        }
        else
        {
            Transform gp = GrandparentTransform;
            if (gp != null)
            {
                int count = gp.childCount;
                if (count > 0)
                {
                    clamped = Mathf.Clamp(clamped, 0, count - 1);
                }
            }
        }

        if (clamped == 0)
        {
            return sprite_index_0;
        }
        else
        {
            if (clamped == 1)
            {
                return sprite_index_1;
            }
            else
            {
                if (clamped == 2)
                {
                    return sprite_index_2;
                }
                else
                {
                    return sprite_index_3;
                }
            }
        }
    }
}
