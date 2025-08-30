using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Score Sprite")]
public sealed class UiScore : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Which player to watch")]
    public PlayerId watch_player = PlayerId.P1;

    [Header("Score Sprites")]
    [Tooltip("Sprites for scores zero to seven")]
    public Sprite[] score_sprites = new Sprite[8];

    [Header("Render Targets")]
    [Tooltip("Optional UI image target")]
    public Image ui_image;
    [Tooltip("Optional sprite renderer target")]
    public SpriteRenderer sprite_renderer;

    [Header("Debug")]
    [Tooltip("Log warnings")]
    public bool verbose_logging = true;

    private PingPongLoop ping_pong_loop;
    private int last_shown_score = -1;

    /*
    * Resolve PingPongLoop and target renderers.
    * @param none
    */
    void Awake()
    {
        TryResolvePingPongLoop();

        if (ui_image == null)
        {
            ui_image = GetComponent<Image>();
        }

        if (sprite_renderer == null)
        {
            sprite_renderer = GetComponent<SpriteRenderer>();
        }

        if (ui_image == null && sprite_renderer == null && verbose_logging)
        {
            Debug.LogWarning("UiScore has no Image or SpriteRenderer");
        }

        if (score_sprites == null || score_sprites.Length < 8)
        {
            if (verbose_logging)
            {
                Debug.LogWarning("UiScore requires eight sprites");
            }
        }
    }

    /*
    * Initialize the visual.
    * @param none
    */
    void Start()
    {
        RefreshVisual(true);
    }

    /*
    * Update the visual each frame after gameplay updates.
    * @param none
    */
    void LateUpdate()
    {
        if (ping_pong_loop == null)
        {
            TryResolvePingPongLoop();
        }

        RefreshVisual(false);
    }

    /*
    * Attempt to find the PongController object and get PingPongLoop.
    * @param none
    */
    private void TryResolvePingPongLoop()
    {
        if (ping_pong_loop != null)
        {
            return;
        }

        GameObject obj = GameObject.Find("PongController");
        if (obj == null)
        {
            if (verbose_logging)
            {
                Debug.LogWarning("PongController object was not found");
            }
            return;
        }

        ping_pong_loop = obj.GetComponent<PingPongLoop>();
        if (ping_pong_loop == null && verbose_logging)
        {
            Debug.LogWarning("PingPongLoop component was not found on PongController");
        }
    }

    /*
    * Read the watched player score.
    * @param none
    * @returns int
    */
    private int GetWatchedScore()
    {
        if (ping_pong_loop == null)
        {
            return 0;
        }

        int p1;
        int p2;
        ping_pong_loop.GetScores(out p1, out p2);

        if (watch_player == PlayerId.P1)
        {
            return p1;
        }
        else
        {
            return p2;
        }
    }

    /*
    * Apply the correct sprite when score changes or on first run.
    * @param force_update Apply even if unchanged
    */
    private void RefreshVisual(bool force_update)
    {
        int score = GetWatchedScore();

        if (score < 0)
        {
            score = 0;
        }

        int max_index = 0;
        if (score_sprites != null)
        {
            max_index = score_sprites.Length - 1;
        }

        if (max_index < 0)
        {
            max_index = 0;
        }

        if (score > max_index)
        {
            score = max_index;
        }

        if (!force_update)
        {
            if (score == last_shown_score)
            {
                return;
            }
        }

        last_shown_score = score;

        Sprite s = null;
        if (score_sprites != null)
        {
            if (score >= 0)
            {
                if (score < score_sprites.Length)
                {
                    s = score_sprites[score];
                }
            }
        }

        if (ui_image != null)
        {
            ui_image.sprite = s;
        }

        if (sprite_renderer != null)
        {
            sprite_renderer.sprite = s;
        }
    }
}
