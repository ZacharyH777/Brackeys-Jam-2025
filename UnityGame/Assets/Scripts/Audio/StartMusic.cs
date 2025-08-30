using UnityEngine;

public class StartMusic : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public AudioSource audioSource_char1;
    public AudioSource audioSource_char2;
    public AudioSource audioSource_char3;
    public AudioSource audioSource_char4;
    void Start()
    {

        if (CharacterSelect.p2_character == "JohnPong")
        {
            audioSource_char1.loop = true;
            audioSource_char1.Play();
        }
        else if (CharacterSelect.p2_character == "Carmen Dynamo")
        {
            audioSource_char2.loop = true;
            audioSource_char2.Play();
        }
        else if (CharacterSelect.p2_character == "DKLA")
        {
            audioSource_char3.loop = true;
            audioSource_char3.Play();
        }
        else if (CharacterSelect.p2_character == "Chargo")
        {
            audioSource_char4.loop = true;
            audioSource_char4.Play();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
