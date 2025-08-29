using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class FindParent : MonoBehaviour
{
    [Header("Parent Target")]
    [SerializeField] private GameObject parent_object;

    [Header("Player Colors")]
    [Tooltip("Color for player zero")]
    [SerializeField] private Color index_zero_color = new Color(1f, 0.4f, 0.8f, 1f);
    [Tooltip("Color for player one")]
    [SerializeField] private Color index_one_color = Color.blue;

    private PlayerInput player_input;
    private SpriteRenderer sprite_renderer;
    private Light2D light_2d;

    private bool is_parented;
    private bool is_colored;

    /*
    Get required components and warn if any are missing.
    */
    void Awake()
    {
        player_input = GetComponent<PlayerInput>();
        sprite_renderer = GetComponent<SpriteRenderer>();

        light_2d = GetComponent<Light2D>();
        if (light_2d == null)
        {
            // Try children as a fallback so the effect still works when the light is nested
            light_2d = GetComponentInChildren<Light2D>(true);
        }
    }

    /*
    Apply color and attempt to parent at start.
    */
    void Start()
    {
        TryApplyColor();
        TryParentOnce();
    }

    /*
    Retry color and parenting until both succeed.
    */
    void Update()
    {
        if (!is_colored)
        {
            TryApplyColor();
        }

        if (!is_parented)
        {
            TryParentOnce();
        }
    }

    /*
    Set the tint based on player index.
    Uses simple slots zero and one.
    */
    private void TryApplyColor()
    {
        if (player_input == null) return;

        int player_index = player_input.playerIndex;
        if (player_index == 0)
        {
            SetTint(index_zero_color);
        }
        else if (player_index == 1)
        {
            SetTint(index_one_color);
        }
        else
        {
            // Unknown player index. Leave default color
            return;
        }

        is_colored = true;
    }

    /*
    Apply a color to the sprite and light when present.
    @param tint_color Color to apply.
    */
    private void SetTint(Color tint_color)
    {
        if (sprite_renderer != null)
        {
            sprite_renderer.color = tint_color;
        }

        if (light_2d != null)
        {
            light_2d.color = tint_color;
        }
    }

    /*
    Parent this object under a target found by name and reset local transform.
    Uses a simple search that prefers an explicit field but falls back to scene names.
    */
    private void TryParentOnce()
    {
        if (parent_object == null)
        {
            // First try a known root object by name
            GameObject root = GameObject.Find("TempBackground");
            if (root != null)
            {
                // Prefer a specific child when present
                Transform found_child = root.transform.Find("Selector(Clone)");
                if (found_child != null)
                {
                    parent_object = found_child.gameObject;
                }
            }

            // Fallback to a direct path lookup when the structured search failed
            if (parent_object == null)
            {
                parent_object = GameObject.Find("TempBackground/Selector(Clone)");
            }

            if (parent_object == null)
            {
                // Could not find a target yet. Try again in a later frame
                return;
            }
        }

        Transform target = parent_object.transform;

        // If the selector spawns a container then use its first child as the real anchor
        if (target.childCount > 0)
        {
            target = target.GetChild(0);
        }

        transform.SetParent(target, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;

        is_parented = true;
    }

    /*
    Reapply the color using the current player index.
    */
    [ContextMenu("Reapply Color By Player Index")]
    private void ReapplyColorByPlayerIndex()
    {
        is_colored = false;
        TryApplyColor();
    }
}
