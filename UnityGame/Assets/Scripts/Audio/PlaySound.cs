using UnityEngine;

public class PlaySound : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public AudioSource audioSource;  // Drag in AudioSource from Inspector
    public AudioClip sfx_hit1;
    public AudioClip sfx_hit2;
    public AudioClip sfx_hit3;
    public AudioClip sfx_hit4;
    public AudioClip sfx_bounce1;
    public AudioClip sfx_bounce2;
    public AudioClip sfx_bounce3;
    public AudioClip sfx_bounce4;
    public AudioClip sfx_menu_move1;
    public AudioClip sfx_menu_select1;
    public AudioClip sfx_point_score1;

    public void sfx_hit()
    {
        // Play ping pong hit sfx
        int index = UnityEngine.Random.Range(0, 4);
        if (index == 1)
        {
            audioSource.PlayOneShot(sfx_hit1);
        }
        else if (index == 2)
        {
            audioSource.PlayOneShot(sfx_hit2);
        }
        else if (index == 3)
        {
            audioSource.PlayOneShot(sfx_hit3);
        }
        else if (index == 4)
        {
            audioSource.PlayOneShot(sfx_hit4);
        }

    }

    public void sfx_bounce()
    {
        // Play ping pong bounce sfx
        int index = UnityEngine.Random.Range(0, 4);
        if (index == 1)
        {
            audioSource.PlayOneShot(sfx_bounce1);
        }
        else if (index == 2)
        {
            audioSource.PlayOneShot(sfx_bounce2);
        }
        else if (index == 3)
        {
            audioSource.PlayOneShot(sfx_bounce3);
        }
        else if (index == 4)
        {
            audioSource.PlayOneShot(sfx_bounce4);
        }

    }
    public void sfx_menu_move()
    {
        audioSource.PlayOneShot(sfx_menu_move1);
    }

    public void sfx_menu_select()
    {
        audioSource.PlayOneShot(sfx_menu_select1);
    }

    public void sfx_point_score()
    {
        audioSource.PlayOneShot(sfx_menu_select1);
    }
}
