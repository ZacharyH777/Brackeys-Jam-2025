using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/*
* Index switcher for a menu row with highlight.
* - Reparents the selector under the chosen child of the grandparent.
* - Activates current parent object while selected.
* - Deactivates the previous parent object when leaving.
* - Left is -1 and Right is +1 with wrap support and deadzone re arm.
* - Adds auto repeat with first delay and repeat interval while held.
* - Select calls RunSceneChange on parent.
* - Cancel loads the main menu scene.
*/
[RequireComponent(typeof(PlayerInput))]
[AddComponentMenu("Utils/Index Switcher")]
public sealed class ExtrasUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Uses this transform if null")]
    public Transform connected;

    [Header("Input")]
    [Tooltip("Assign UI Navigate")]
    public InputActionReference navigate_action_reference;

    [Tooltip("Assign UI Submit")]
    public InputActionReference select_action_reference;

    [Tooltip("Assign UI Cancel")]
    public InputActionReference cancel_action_reference;

    [Header("Settings")]
    [Tooltip("Wrap around child list")]
    public bool wrap_around = true;

    [Tooltip("Deadzone for horizontal axis")]
    [Range(0f, 1f)] public float horizontal_deadzone = 0.5f;

    [Tooltip("Reset rotation and scale")]
    public bool reset_rotation_and_scale = false;

    [Header("Repeat")]
    [Tooltip("First repeat delay seconds")]
    public float repeat_first_delay = 0.5f;

    [Tooltip("Repeat interval seconds")]
    public float repeat_interval = 0.1f;

    [Header("Main Menu")]
    [Tooltip("Scene name to load")]
    public string main_menu_scene_name = "Main Menu";

    private InputAction navigate_action;
    private InputAction select_action;
    private InputAction cancel_action;

    private int current_index;
    private int held_direction; /* -1 left, 0 none, +1 right */
    private float next_repeat_time;

    private Transform last_parent_highlighted;
    private PlaySound play_sound;

    /*
    * Return the active source transform.
    * @param none
    */
    private Transform Source()
    {
        if (connected != null)
        {
            return connected;
        }
        return transform;
    }

    /*
    * Resolve navigate, select, and cancel actions from reference or PlayerInput.
    * @param none
    */
    private void ResolveActions()
    {
        PlayerInput player_input = GetComponent<PlayerInput>();

        if (navigate_action_reference != null)
        {
            if (navigate_action_reference.action != null)
            {
                if (player_input != null)
                {
                    if (player_input.actions != null)
                    {
                        InputAction by_id = player_input.actions.FindAction(navigate_action_reference.action.id);
                        if (by_id != null)
                        {
                            navigate_action = by_id;
                        }
                    }
                }
                if (navigate_action == null)
                {
                    navigate_action = navigate_action_reference.action;
                }
            }
        }
        if (navigate_action == null)
        {
            if (player_input != null)
            {
                if (player_input.actions != null)
                {
                    navigate_action = player_input.actions.FindAction("UI/Navigate", false);
                }
            }
        }

        if (select_action_reference != null)
        {
            if (select_action_reference.action != null)
            {
                if (player_input != null)
                {
                    if (player_input.actions != null)
                    {
                        InputAction by_id_select = player_input.actions.FindAction(select_action_reference.action.id);
                        if (by_id_select != null)
                        {
                            select_action = by_id_select;
                        }
                    }
                }
                if (select_action == null)
                {
                    select_action = select_action_reference.action;
                }
            }
        }
        if (select_action == null)
        {
            if (player_input != null)
            {
                if (player_input.actions != null)
                {
                    select_action = player_input.actions.FindAction("UI/Submit", false);
                }
            }
        }

        if (cancel_action_reference != null)
        {
            if (cancel_action_reference.action != null)
            {
                if (player_input != null)
                {
                    if (player_input.actions != null)
                    {
                        InputAction by_id_cancel = player_input.actions.FindAction(cancel_action_reference.action.id);
                        if (by_id_cancel != null)
                        {
                            cancel_action = by_id_cancel;
                        }
                    }
                }
                if (cancel_action == null)
                {
                    cancel_action = cancel_action_reference.action;
                }
            }
        }
        if (cancel_action == null)
        {
            if (player_input != null)
            {
                if (player_input.actions != null)
                {
                    cancel_action = player_input.actions.FindAction("UI/Cancel", false);
                }
            }
        }
    }

    /*
    * Initialize index, parent under UIStart tag, resolve actions, and apply initial highlight.
    * @param none
    */
    void Awake()
    {
        current_index = 0;

        GameObject audio_go = GameObject.FindGameObjectWithTag("Audio");
        if (audio_go != null)
        {
            play_sound = audio_go.GetComponent<PlaySound>();
        }
        else
        {
            Debug.LogWarning("Audio object was not found");
        }

        GameObject ui_start = GameObject.FindGameObjectWithTag("UIStart");
        if (ui_start != null)
        {
            Transform first_child = ui_start.transform.GetChild(0);
            transform.SetParent(first_child, false);
        }
        else
        {
            Debug.LogWarning("UIStart was not found");
        }

        ResolveActions();

        Transform s = Source();
        if (s != null)
        {
            if (s.parent != null)
            {
                current_index = s.parent.GetSiblingIndex();
                last_parent_highlighted = s.parent;
                ApplyHighlightToParent(last_parent_highlighted);
            }
            else
            {
                current_index = 0;
                last_parent_highlighted = null;
            }
        }
        else
        {
            current_index = 0;
            last_parent_highlighted = null;
        }

        held_direction = 0;
        next_repeat_time = 0f;
    }

    /*
    * Enable callbacks for navigate, select, and cancel.
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
        }

        if (select_action != null)
        {
            if (!select_action.enabled)
            {
                select_action.Enable();
            }
            select_action.performed += OnSelectPerformed;
        }

        if (cancel_action != null)
        {
            if (!cancel_action.enabled)
            {
                cancel_action.Enable();
            }
            cancel_action.performed += OnCancelPerformed;
        }
    }

    /*
    * Disable callbacks, clear state, and clear highlight.
    * @param none
    */
    void OnDisable()
    {
        if (navigate_action != null)
        {
            navigate_action.performed -= OnNavigatePerformed;
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

        if (cancel_action != null)
        {
            cancel_action.performed -= OnCancelPerformed;
            if (cancel_action.enabled)
            {
                cancel_action.Disable();
            }
        }

        held_direction = 0;

        if (last_parent_highlighted != null)
        {
            ClearHighlightOnParent(last_parent_highlighted);
            last_parent_highlighted = null;
        }
    }

    /*
    * Handle navigate input with deadzone re arm and schedule repeat.
    * Steps once when crossing threshold in a direction.
    * Arms first repeat and subsequent interval while held.
    * @param context Input callback context
    */
    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        Vector2 v = context.ReadValue<Vector2>();
        float ax = Mathf.Abs(v.x);

        if (ax <= horizontal_deadzone)
        {
            if (held_direction != 0)
            {
                held_direction = 0;
                next_repeat_time = 0f;
            }
            return;
        }

        int dir_code = 0;
        if (v.x > 0f)
        {
            dir_code = 1;
        }
        else
        {
            dir_code = -1;
        }

        if (dir_code == held_direction)
        {
            return;
        }

        held_direction = dir_code;
        StepIndex(dir_code);
        ReparentToCurrent();

        next_repeat_time = Time.unscaledTime + repeat_first_delay;
    }

    /*
    * Repeat step while held on the same direction beyond delays.
    * @param none
    */
    void Update()
    {
        if (held_direction == 0)
        {
            return;
        }

        if (navigate_action == null)
        {
            return;
        }

        Vector2 v = navigate_action.ReadValue<Vector2>();
        float ax = Mathf.Abs(v.x);

        if (ax <= horizontal_deadzone)
        {
            held_direction = 0;
            next_repeat_time = 0f;
            return;
        }

        if (v.x > 0f && held_direction < 0)
        {
            held_direction = 0;
            next_repeat_time = 0f;
            return;
        }

        if (v.x < 0f && held_direction > 0)
        {
            held_direction = 0;
            next_repeat_time = 0f;
            return;
        }

        if (Time.unscaledTime >= next_repeat_time)
        {
            StepIndex(held_direction);
            ReparentToCurrent();
            next_repeat_time = Time.unscaledTime + repeat_interval;
        }
    }

    /*
    * Call RunSceneChange on the parent after restoring its state.
    * @param context Input callback context
    */
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        return;
    }

    /*
    * Load the main menu scene when cancel is pressed.
    * @param context Input callback context
    */
    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (play_sound != null)
        {
            play_sound.sfx_menu_select();
        }

        if (string.IsNullOrEmpty(main_menu_scene_name))
        {
            Debug.LogWarning("Main menu scene name is empty");
            return;
        }

        SceneManager.LoadScene(main_menu_scene_name, LoadSceneMode.Single);
    }

    /*
    * Advance current index across grandparent children.
    * @param delta Signed step
    */
    public void StepIndex(int delta)
    {
        Transform s = Source();
        if (s == null)
        {
            Debug.LogWarning("No source found");
            return;
        }
        if (s.parent == null)
        {
            Debug.LogWarning("No parent found");
            return;
        }
        if (s.parent.parent == null)
        {
            Debug.LogWarning("No grandparent found");
            return;
        }

        Transform gp = s.parent.parent;
        int count = gp.childCount;
        if (count <= 0)
        {
            Debug.LogWarning("Grandparent has no children");
            return;
        }

        if (play_sound != null)
        {
            play_sound.sfx_menu_move();
        }

        int next = current_index + delta;

        if (wrap_around)
        {
            int m = next % count;
            if (m < 0)
            {
                m = m + count;
            }
            next = m;
        }
        else
        {
            if (next < 0)
            {
                next = 0;
            }
            if (next > count - 1)
            {
                next = count - 1;
            }
        }

        current_index = next;
    }

    /*
    * Reparent the selector under the current child and toggle parents safely.
    * Enables the target first, moves the selector, then disables the previous.
    * @param none
    */
    public void ReparentToCurrent()
    {
        Transform s = Source();
        if (s == null)
        {
            Debug.LogWarning("No source found");
            return;
        }
        if (s.parent == null)
        {
            Debug.LogWarning("No parent found");
            return;
        }
        if (s.parent.parent == null)
        {
            Debug.LogWarning("No grandparent found");
            return;
        }

        Transform gp = s.parent.parent;
        int count = gp.childCount;
        if (count <= 0)
        {
            Debug.LogWarning("Grandparent has no children");
            return;
        }

        if (current_index < 0)
        {
            current_index = 0;
        }
        if (current_index > count - 1)
        {
            current_index = count - 1;
        }

        Transform previous_parent = s.parent;                /* cache before move */
        Transform target = gp.GetChild(current_index);

        /* 1) ensure target is active BEFORE moving */
        GameObject target_go = target.gameObject;
        if (target_go != null)
        {
            if (target_go.activeSelf == false)
            {
                target_go.SetActive(true);
            }
        }

        /* 2) move under the target so disabling previous will not disable us */
        s.SetParent(target, false);
        s.localPosition = Vector3.zero;

        if (reset_rotation_and_scale == true)
        {
            s.localRotation = Quaternion.identity;
            s.localScale = Vector3.one;
        }

        /* 3) now it is safe to disable the previous parent */
        if (previous_parent != null)
        {
            if (previous_parent != target)
            {
                GameObject prev_go = previous_parent.gameObject;
                if (prev_go != null)
                {
                    if (prev_go.activeSelf == true)
                    {
                        prev_go.SetActive(false);
                    }
                }
            }
        }

        last_parent_highlighted = target;
    }

    /*
    * Activate the parent GameObject while selected.
    * @param parent_target Parent to activate
    */
    private void ApplyHighlightToParent(Transform parent_target)
    {
        if (parent_target == null)
        {
            return;
        }

        GameObject go = parent_target.gameObject;
        if (go != null)
        {
            go.SetActive(true);
            return;
        }
    }

    /*
    * Deactivate the parent GameObject when leaving.
    * @param parent_target Parent to deactivate
    */
    private void ClearHighlightOnParent(Transform parent_target)
    {
        if (parent_target == null)
        {
            return;
        }

        GameObject go = parent_target.gameObject;
        if (go != null)
        {
            go.SetActive(false);
            return;
        }
    }
}
