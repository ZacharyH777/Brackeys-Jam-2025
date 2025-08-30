using UnityEngine;

public class PlaySound : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public AudioSource audioSource;  // Drag in AudioSource from Inspector
    public AudioClip sfx_hit;
    public AudioClip sfx_bounce;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            audioSource.PlayOneShot(sfx_hit);
        }
    }
}
