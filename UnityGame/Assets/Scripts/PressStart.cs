using UnityEngine;

public class PressStart : MonoBehaviour
{
    private float p_time = 0;
    bool enab = false;
    public SpriteRenderer gameObject;
    void Update()
    {
        if (CharacterSelect.p1_character != "" && CharacterSelect.p2_character != "")
        {
            p_time += Time.deltaTime;

            if (p_time > 1)
            {
                p_time = 0;

                if (enab)
                {
                    gameObject.enabled = false;
                    enab = false;
                }
                else
                {
                    gameObject.enabled = true;
                    enab = true;
                }
            }
        }
    }
}
