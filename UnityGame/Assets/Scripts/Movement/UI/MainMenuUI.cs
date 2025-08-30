using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Settings")]
    [Tooltip("Wrap around child list")]
    public bool wrap_around = true;

    [Tooltip("Horizontal deadzone")]
    [Range(0f, 1f)] public float horizontal_deadzone = 0.5f;

    [Tooltip("Reset rotation and scale")]
    public bool reset_rotation_and_scale = false;

    private InputAction navigate_action;
    private int current_index;
    private int held_direction;
    
 
    /* Get source transform. */
    private Transform Source()
    {
        if (connected != null) { return connected; }
        return transform;
    }

    /* Resolve navigate action from reference or PlayerInput. */
    private void ResolveAction()
    {
        PlayerInput pi = GetComponent<PlayerInput>();
        if (navigate_action_reference != null && navigate_action_reference.action != null)
        {
            if (pi != null && pi.actions != null)
            {
                InputAction by_id = pi.actions.FindAction(navigate_action_reference.action.id);
                if (by_id != null) { navigate_action = by_id; return; }
            }
            navigate_action = navigate_action_reference.action;
            return;
        }
        if (pi != null && pi.actions != null)
        {
            navigate_action = pi.actions.FindAction("UI/Navigate", false);
        }
    }

    /* Initialize index and input. */
    void Awake()
    {
        current_index = 0;
        GameObject _thingy = GameObject.FindGameObjectWithTag("UIStart");
        this.transform.parent = _thingy.transform.GetChild(0);
        ResolveAction();
        Transform s = Source();
        if (s != null && s.parent != null)
        {
            current_index = s.parent.GetSiblingIndex();
        }
        else
        {
            current_index = 0;
        }
    }

    /* Enable input callbacks. */
    void OnEnable()
    {
        if (navigate_action == null) { return; }
        if (!navigate_action.enabled) { navigate_action.Enable(); }
        navigate_action.performed += OnNavigatePerformed;
        navigate_action.canceled += OnNavigateCanceled;
    }

    /* Disable input callbacks. */
    void OnDisable()
    {
        if (navigate_action == null) { return; }
        navigate_action.performed -= OnNavigatePerformed;
        navigate_action.canceled -= OnNavigateCanceled;
        if (navigate_action.enabled) { navigate_action.Disable(); }
        held_direction = 0;
    }

    /* Handle navigate input and step once.
     * @param context Input callback context
     */
    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        Vector2 v = context.ReadValue<Vector2>();
        int dir = 0;
        if (v.x > horizontal_deadzone) { dir = 1; }
        else { if (v.x < -horizontal_deadzone) { dir = -1; } }
        if (dir == 0) { return; }
        if (dir == held_direction) { return; }
        held_direction = dir;
        StepIndex(dir);
        ReparentToCurrent();
    }

    /* Clear held state.
     * @param context Input callback context
     */
    private void OnNavigateCanceled(InputAction.CallbackContext context)
    {
        held_direction = 0;
    }

    /* Advance current index across grandparent children.
     * @param delta Signed step
     */
    public void StepIndex(int delta)
    {
        Transform s = Source();
        if (s == null) { Debug.LogWarning("No source found"); return; }
        if (s.parent == null || s.parent.parent == null) { Debug.LogWarning("No grandparent found"); return; }

        Transform gp = s.parent.parent;
        int count = gp.childCount;
        if (count <= 0) { Debug.LogWarning("Grandparent has no children"); return; }

        int next = current_index + delta;
        if (wrap_around)
        {
            int m = next % count;
            if (m < 0) { m = m + count; }
            next = m;
        }
        else
        {
            if (next < 0) { next = 0; }
            if (next > count - 1) { next = count - 1; }
        }
        current_index = next;
    }

    /* Reparent under child at current index. */
    public void ReparentToCurrent()
    {
        Transform s = Source();
        if (s == null) { Debug.LogWarning("No source found"); return; }
        if (s.parent == null || s.parent.parent == null) { Debug.LogWarning("No grandparent found"); return; }

        Transform gp = s.parent.parent;
        int count = gp.childCount;
        if (count <= 0) { Debug.LogWarning("Grandparent has no children"); return; }
        if (current_index < 0) { current_index = 0; }
        if (current_index > count - 1) { current_index = count - 1; }

        Transform target = gp.GetChild(current_index);
        s.SetParent(target, false);
        s.localPosition = Vector3.zero;
        if (reset_rotation_and_scale)
        {
            s.localRotation = Quaternion.identity;
            s.localScale = Vector3.one;
        }
    }
}
