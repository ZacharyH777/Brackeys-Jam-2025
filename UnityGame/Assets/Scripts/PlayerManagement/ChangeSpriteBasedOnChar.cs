using UnityEngine;

public class ChangeSpriteBasedOnChar : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private SpriteRenderer spriteRenderer; // Drag in your SpriteRenderer
    public Sprite newSprite1;
    public Sprite newSprite2;
    public Sprite newSprite3;
    public Sprite newSprite4;
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (CharacterSelect.p1_character == "JohnPong")
        {
            spriteRenderer.sprite = newSprite1;
        }
        else if (CharacterSelect.p1_character == "Carmen Dynamo")
        {
            spriteRenderer.sprite = newSprite2;
        }
        else if (CharacterSelect.p1_character == "DKLA")
        {
            spriteRenderer.sprite = newSprite3;
        }
        else if (CharacterSelect.p1_character == "Chargo")
        {
            spriteRenderer.sprite = newSprite4;
        }
    }

}
