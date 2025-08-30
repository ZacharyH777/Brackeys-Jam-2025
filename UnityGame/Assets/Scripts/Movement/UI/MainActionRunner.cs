using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[AddComponentMenu("Utils/Menu Action Runner")]
public sealed class MenuActionRunner : MonoBehaviour
{
    public enum ActionType { Arcade, Versus, Help, Extras }

    [Header("Action")]
    [Tooltip("Action for this item")]
    public ActionType action_type = ActionType.Arcade;

    [Header("Scenes")]
    [Tooltip("Scene for play")]
    public string play_scene_name = "CharatcerSelect";

    [Tooltip("Scene for options")]
    public string options_scene_name = "";

    [Tooltip("Scene for credits")]
    public string credits_scene_name = "";

    public string extras_scene_name = "";

    [Header("Events")]
    [Tooltip("Event for play")]
    public UnityEvent on_play;

    [Tooltip("Event for options")]
    public UnityEvent on_options;

    [Tooltip("Event for credits")]
    public UnityEvent on_credits;

    /*
    * Entry point called by MainMenuUI.
    * Routes to a private handler based on action_type.
    * @param none
    */
    public void RunSceneChange()
    {
        if (action_type == ActionType.Arcade)
        {
            DoArcade();
            return;
        }

        if (action_type == ActionType.Versus)
        {
            DoVersus();
            return;
        }

        if (action_type == ActionType.Help)
        {
            DoHelp();
            return;
        }

        if (action_type == ActionType.Extras)
        {
            DoExtras();
            return;
        }
    }

    /*
    * Load play scene or invoke event.
    * @param none
    */
    private void DoArcade()
    {
        if (!string.IsNullOrEmpty(play_scene_name))
        {
            CharacterSelect.is_singleplayer = true;
            SceneManager.LoadScene(play_scene_name);
            return;
        }

        Debug.LogWarning("No play scene or event set");
    }

    /*
    * Load options scene or invoke event.
    * @param none
    */
    private void DoVersus()
    {
        if (!string.IsNullOrEmpty(play_scene_name))
        {
            CharacterSelect.is_singleplayer = false;
            SceneManager.LoadScene(play_scene_name);
            return;
        }

        Debug.LogWarning("No options scene or event set");
    }

    /*
    * Load credits scene or invoke event.
    * @param none
    */
    private void DoHelp()
    {
        if (!string.IsNullOrEmpty(credits_scene_name))
        {
            SceneManager.LoadScene(credits_scene_name);
            return;
        }

        Debug.LogWarning("No credits scene or event set");
    }

    /*
    * Load Extras scene or invoke event.
    * @param none
    */
    private void DoExtras()
    {
        if (!string.IsNullOrEmpty(extras_scene_name))
        {
            SceneManager.LoadScene(extras_scene_name);
            return;
        }
    }
}
