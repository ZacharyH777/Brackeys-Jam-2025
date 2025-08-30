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
    [Tooltip("Wrap around child list when true else clamp")]
    public bool wrap_around = true;

    [Header("UI Input")]
    [Tooltip("Assign UI navigate vector2")]
    public InputActionReference navigate_action_reference;

    [Tooltip("Assign UI submit or select")]
    public InputActionReference select_action_reference;

    [Tooltip("Assign start action for game start")]
    public InputActionReference start_game_action_reference;

    [Tooltip("Assign UI cancel")]
    public InputActionReference cancel_action_reference;

    [Header("Cancel Hold")]
    [Tooltip("Seconds to hold cancel to go back")]
    public float cancel_hold_seconds = 3f;

    [Tooltip("Optional scene to load on long cancel")]
    public string back_out_scene_name = "";

    [Tooltip("Invoked on long cancel when no scene is set")]
    public UnityEvent on_long_cancel;

    [Header("Navigate Gate")]
    [Tooltip("Deadzone for horizontal navigate decision")]
    [Range(0f, 1f)] public float horizontal_deadzone = 0.5f;

    [Tooltip("Delay before repeating while held seconds")]
    public float repeat_delay = 0.45f;

    [Tooltip("Interval between repeats seconds")]
    public float repeat_rate = 0.10f;

    [Tooltip("Reset local rotation and scale when reparenting")]
    public bool reset_rotation_and_scale = false;

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

    /*
    Return the transform to control. Uses connected when present else this transform.
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
    Return the parent of the source transform.
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
    Return the grandparent of the source transform.
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
    Initialize current index and resolve input actions.
    * @param none
    */
    void Awake()
    {
        current_index = Mathf.Max(0, start_index);
        player_input = GetComponent<PlayerInput>();

        navigate_action = ResolveActionFromReference(navigate_action_reference, player_input);
        if (navigate_action == null && player_input != null && player_input.actions != null)
        {
            navigate_action = player_input.actions.FindAction("UI/Navigate", false);
        }

        select_action = ResolveActionFromReference(select_action_reference, player_input);
        if (select_action == null && player_input != null && player_input.actions != null)
        {
            select_action = player_input.actions.FindAction("UI/Submit", false);
        }

        start_action = ResolveActionFromReference(start_game_action_reference, player_input);
        if (start_action == null && player_input != null && player_input.actions != null)
        {
            start_action = player_input.actions.FindAction("UI/Start", false);
        }

        cancel_action = ResolveActionFromReference(cancel_action_reference, player_input);
        if (cancel_action == null && player_input != null && player_input.actions != null)
        {
            cancel_action = player_input.actions.FindAction("UI/Cancel", false);
        }
    }

    /*
    Enable actions and seed the current index from the grandparent count.
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
            navigate_action.performed += OnNavigatePerformed;
            navigate_action.canceled += OnNavigateCanceled;
        }

        if (select_action != null)
        {
            if (!select_action.enabled)
            {
                select_action.Enable();
            }
            select_action.performed += OnSelectPerformed;
        }

        if (start_action != null)
        {
            if (!start_action.enabled)
            {
                start_action.Enable();
            }
            start_action.performed += OnStartPerformed;
        }

        if (cancel_action != null)
        {
            if (!cancel_action.enabled)
            {
                cancel_action.Enable();
            }
            cancel_action.started += OnCancelStarted;
            cancel_action.canceled += OnCancelCanceled;
        }

        Transform gp = GrandparentTransform;
        if (gp != null)
        {
            if (gp.childCount > 0)
            {
                current_index = Mathf.Clamp(start_index, 0, gp.childCount - 1);
            }
        }
    }

    /*
    Disable actions and clear held state.
    * @param none
    */
    void OnDisable()
    {
        if (navigate_action != null)
        {
            navigate_action.performed -= OnNavigatePerformed;
            navigate_action.canceled -= OnNavigateCanceled;
            if (navigate_action.enabled)
            {
                navigate_action.Disable();
            }
        }

        if (select_action != null)
        {
            select_action.performed -= OnSelectPerformed;
            if (select_action.enabled)
            {
                select_action.Disable();
            }
        }

        if (start_action != null)
        {
            start_action.performed -= OnStartPerformed;
            if (start_action.enabled)
            {
                start_action.Disable();
            }
        }

        if (cancel_action != null)
        {
            cancel_action.started -= OnCancelStarted;
            cancel_action.canceled -= OnCancelCanceled;
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
    Drive repeat navigation when a direction is held.
    * @param none
    */
    void Update()
    {
        if (is_locked)
        {
            return;
        }
        if (!InputAllowedForThisInstance())
        {
            return;
        }

        if (held_direction != 0)
        {
            if (Time.unscaledTime >= next_repeat_time)
            {
                StepIndex(held_direction);
                ReparentConnectedToCurrent();
                next_repeat_time = Time.unscaledTime + repeat_rate;
            }
        }
    }

    /*
    Handle navigate input and start the repeat timer.
    * @param context Input context with a 2D vector
    */
    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        if (is_locked)
        {
            return;
        }
        if (!InputAllowedForThisInstance())
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
                StepIndex(held_direction);
                ReparentConnectedToCurrent();
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
    Stop repeating when navigate is released.
    * @param context Input context
    */
    private void OnNavigateCanceled(InputAction.CallbackContext context)
    {
        held_direction = 0;
        next_repeat_time = float.PositiveInfinity;
    }

    /*
    Assign the selected child name to the global character slots and lock input.
    * @param context Input context
    */
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        if (is_locked)
        {
            return;
        }
        if (!InputAllowedForThisInstance())
        {
            return;
        }

        Transform pick = GetCurrentGrandparentChild();
        if (pick == null)
        {
            return;
        }

        string chosen_name = pick.name;

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

        is_locked = true;
        last_selected_name = chosen_name;
        ReparentConnectedToCurrent();

        int player_index_log = -1;
        if (player_input != null)
        {
            player_index_log = player_input.playerIndex;
        }
        Debug.Log("Player " + player_index_log + " selected " + chosen_name);
    }

    /*
    Start the game in the selected mode.
    * @param context Input context
    */
    private void OnStartPerformed(InputAction.CallbackContext context)
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
    Begin measuring cancel hold duration.
    * @param context Input context
    */
    private void OnCancelStarted(InputAction.CallbackContext context)
    {
        if (!InputAllowedForThisInstance())
        {
            return;
        }
        cancel_hold_active = true;
        cancel_hold_start_time = Time.unscaledTime;
    }

    /*
    Complete cancel hold. Short tap clears selection when locked. Long hold backs out.
    * @param context Input context
    */
    private void OnCancelCanceled(InputAction.CallbackContext context)
    {
        if (!InputAllowedForThisInstance())
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
            TriggerBackOut();
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
    Perform back out behavior for long cancel.
    * @param none
    */
    private void TriggerBackOut()
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
    Move current index by delta with wrap or clamp.
    * @param delta Signed step amount
    */
    public void StepIndex(int delta)
    {
        int count = 0;
        Transform gp = GrandparentTransform;
        if (gp != null)
        {
            count = gp.childCount;
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
    Get the current child under the grandparent using the current index.
    * @param none
    */
    public Transform GetCurrentGrandparentChild()
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
    Reparent the controlled transform to the current selection and reset local values.
    * @param none
    */
    public Transform ReparentConnectedToCurrent()
    {
        Transform target = GetCurrentGrandparentChild();
        Transform src = SourceTransform;
        if (target == null)
        {
            return null;
        }
        if (src == null)
        {
            return null;
        }

        src.SetParent(target, worldPositionStays: false);
        src.localPosition = Vector3.zero;

        if (reset_rotation_and_scale)
        {
            src.localRotation = Quaternion.identity;
            src.localScale = Vector3.one;
        }
        return target;
    }

    /*
    Find an action in the owner asset by reference.
    * @param reference Input action reference
    * @param owner Player input owning the asset
    */
    private static InputAction ResolveActionFromReference(InputActionReference reference, PlayerInput owner)
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
    Only allow input from player one in singleplayer.
    * @param none
    */
    private bool InputAllowedForThisInstance()
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
}
