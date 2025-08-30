using UnityEngine;
using UnityEngine.InputSystem;

/*
* Index switcher for a menu row with highlight.
* - Reparents the selector under the chosen child of the grandparent.
* - Highlights the current parent SpriteRenderer to a yellowish color.
* - Restores the previous parent color to white when leaving.
* - One-step navigation per direction press (no auto-repeat).
* - Right=+1, Left=-1, Down=+2, Up=-2 with wrap support.
* - Select action calls RunSceneChange on parent.
*/
[RequireComponent(typeof(PlayerInput))]
[AddComponentMenu("Utils/Index Switcher")]
public sealed class MainMenuUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("If null uses this transform")]
    public Transform connected;

    [Header("Input")]
    [Tooltip("Assign UI Navigate")]
    public InputActionReference navigate_action_reference;

    [Tooltip("Assign UI Submit")]
    public InputActionReference select_action_reference;

    [Header("Settings")]
    [Tooltip("Wrap around child list")]
    public bool wrap_around = true;

    [Tooltip("Deadzone for both axes")]
    [Range(0f, 1f)] public float horizontal_deadzone = 0.5f;

    [Tooltip("Reset rotation and scale")]
    public bool reset_rotation_and_scale = false;

    [Header("Highlight")]
    [Tooltip("Color used while selected")]
    public Color highlight_color = new Color(1f, 0.92f, 0.35f, 1f);

    private InputAction navigate_action;
    private InputAction select_action;

    private int current_index;
    private int held_direction; /* -2 up, -1 left, 0 none, +1 right, +2 down */

    private Transform last_parent_highlighted;

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
    * Resolve navigate and select actions from reference or PlayerInput.
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
    }

    /*
    * Initialize index, parent under UIStart tag, resolve actions, and apply initial highlight.
    * @param none
    */
    void Awake()
    {
        current_index = 0;

        GameObject ui_start = GameObject.FindGameObjectWithTag("UIStart");
        if (ui_start != null)
        {
            Transform first_child = ui_start.transform.GetChild(0);
            transform.SetParent(first_child, false);
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
    }

    /*
    * Enable callbacks for navigate and select.
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

        held_direction = 0;

        if (last_parent_highlighted != null)
        {
            ClearHighlightOnParent(last_parent_highlighted);
            last_parent_highlighted = null;
        }
    }

    /*
    * Handle navigate input: single step only when direction changes.
    * Right=+1, Left=-1, Down=+2, Up=-2. Chooses dominant axis on diagonals.
    * @param context Input callback context
    */
    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        Vector2 v = context.ReadValue<Vector2>();

        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        int dir_code = 0;

        if (ax >= ay)
        {
            if (v.x > horizontal_deadzone)
            {
                dir_code = 1;
            }
            else
            {
                if (v.x < -horizontal_deadzone)
                {
                    dir_code = -1;
                }
            }
        }
        else
        {
            if (v.y > horizontal_deadzone)
            {
                dir_code = 2;  /* down */
            }
            else
            {
                if (v.y < -horizontal_deadzone)
                {
                    dir_code = -2; /* up */
                }
            }
        }

        if (dir_code == 0)
        {
            return;
        }

        if (dir_code == held_direction)
        {
            return;
        }

        held_direction = dir_code;

        StepIndex(dir_code);
        ReparentToCurrent();
    }

    /*
    * Reset direction so the next press in the same direction can step again.
    * @param context Input callback context
    */
    private void OnNavigateCanceled(InputAction.CallbackContext context)
    {
        held_direction = 0;
    }

    /*
    * Call RunSceneChange on the parent after restoring its color to white.
    * @param context Input callback context
    */
    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        Transform s = Source();
        if (s == null)
        {
            Debug.LogWarning("No source found");
            return;
        }

        Transform p = s.parent;
        if (p == null)
        {
            Debug.LogWarning("No parent found");
            return;
        }

        ClearHighlightOnParent(p);
        last_parent_highlighted = null;

        p.SendMessageUpwards("RunSceneChange", SendMessageOptions.DontRequireReceiver);
    }

    /*
    * Advance current index across grandparent children.
    * @param delta Signed step (use -2,-1,+1,+2)
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
    * Reparent the selector under the current child and update highlights.
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

        Transform target = gp.GetChild(current_index);

        if (last_parent_highlighted != null)
        {
            ClearHighlightOnParent(last_parent_highlighted);
        }

        s.SetParent(target, false);
        s.localPosition = Vector3.zero;

        if (reset_rotation_and_scale)
        {
            s.localRotation = Quaternion.identity;
            s.localScale = Vector3.one;
        }

        ApplyHighlightToParent(target);
        last_parent_highlighted = target;
    }

    /*
    * Set parent SpriteRenderer to highlight color.
    * @param parent_target Parent to colorize
    */
    private void ApplyHighlightToParent(Transform parent_target)
    {
        if (parent_target == null)
        {
            return;
        }

        SpriteRenderer sr = parent_target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = highlight_color;
            return;
        }
    }

    /*
    * Restore parent SpriteRenderer to white.
    * @param parent_target Parent to restore
    */
    private void ClearHighlightOnParent(Transform parent_target)
    {
        if (parent_target == null)
        {
            return;
        }

        SpriteRenderer sr = parent_target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
            return;
        }
    }
}
