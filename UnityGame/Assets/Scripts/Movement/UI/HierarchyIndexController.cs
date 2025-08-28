using UnityEngine;
using UnityEngine.InputSystem;

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

    private int held_direction; // -1 0 +1
    private float next_repeat_time = float.PositiveInfinity;
    private bool is_locked;

    /*
    Return the transform to control. Uses connected when present else this transform.
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
    */
    void Awake()
    {
        current_index = Mathf.Max(0, start_index);
        player_input = GetComponent<PlayerInput>();

        navigate_action = ResolveActionFromReference(navigate_action_reference, player_input);
        if (navigate_action == null && player_input != null && player_input.actions != null)
        {
            navigate_action = player_input.actions.FindAction("UI/Navigate", throwIfNotFound: false);
        }

        select_action = ResolveActionFromReference(select_action_reference, player_input);
        if (select_action == null && player_input != null && player_input.actions != null)
        {
            select_action = player_input.actions.FindAction("UI/Submit", throwIfNotFound: false);
        }

        start_action = ResolveActionFromReference(start_game_action_reference, player_input);
        if (start_action == null && player_input != null && player_input.actions != null)
        {
            start_action = player_input.actions.FindAction("UI/Start", throwIfNotFound: false);
        }
    }

    /*
    Enable actions and seed the current index from the grandparent count.
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

        held_direction = 0;
        next_repeat_time = float.PositiveInfinity;
    }

    /*
    Drive repeat navigation when a direction is held.
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

        // When a direction is held we step the index after a delay and then at a fixed rate.
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
    @param context Input context with a 2D vector.
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

        // Simple horizontal gate with a deadzone so small stick noise does not move selection
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

                // Start the initial delay. After this the repeat uses repeat_rate
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
    */
    private void OnNavigateCanceled(InputAction.CallbackContext context)
    {
        held_direction = 0;
        next_repeat_time = float.PositiveInfinity;
    }

    /*
    Assign the selected child name to the global character slots and lock input.
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

        // Map to slots depending on singleplayer or multiplayer
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
                    // Fallback for unexpected indices. Fill the first empty slot
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

        is_locked = true; // lock navigation after choosing
        ReparentConnectedToCurrent();

        // Simple selection log for debugging
        int player_index_log = -1;
        if (player_input != null)
        {
            player_index_log = player_input.playerIndex;
        }
        Debug.Log("Player " + player_index_log + " selected " + chosen_name);
    }

    /*
    Start the game in the selected mode.
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
    Move current index by delta with wrap or clamp.
    @param delta Signed step to move selection.
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
            // Use modulo then fix negative values so index stays in 0..count-1
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
    Find an action in the owner asset by reference. Falls back to the reference action when no asset match is found.
    @param reference Input action reference to resolve.
    @param owner Player input that owns an action asset.
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

        // Only player one can navigate select and start when singleplayer is enabled
        if (idx == 0)
        {
            return true;
        }
        return false;
    }
}
