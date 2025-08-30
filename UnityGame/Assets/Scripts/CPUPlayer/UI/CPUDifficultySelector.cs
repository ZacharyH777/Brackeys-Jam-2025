using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CPUDifficultySelector : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Button to decrease difficulty")]
    public Button decreaseButton;
    [Tooltip("Button to increase difficulty")]  
    public Button increaseButton;
    [Tooltip("Text display for current difficulty")]
    public TextMeshProUGUI difficultyText;
    [Tooltip("Text display for difficulty description")]
    public TextMeshProUGUI descriptionText;

    [Header("Difficulty Descriptions")]
    public string easyDescription = "Easy - Slower reactions, less accurate";
    public string mediumDescription = "Medium - Balanced gameplay";
    public string hardDescription = "Hard - Fast reactions, high accuracy";

    private CPUDifficulty currentDifficulty;

    void Start()
    {
        // Initialize with current selection
        currentDifficulty = CharacterSelect.GetCPUDifficulty;
        
        // Set up button listeners
        if (decreaseButton != null)
            decreaseButton.onClick.AddListener(DecreaseDifficulty);
        
        if (increaseButton != null)
            increaseButton.onClick.AddListener(IncreaseDifficulty);
        
        // Update display
        UpdateDisplay();
    }

    void OnDestroy()
    {
        // Clean up button listeners
        if (decreaseButton != null)
            decreaseButton.onClick.RemoveListener(DecreaseDifficulty);
        
        if (increaseButton != null)
            increaseButton.onClick.RemoveListener(IncreaseDifficulty);
    }

    private void DecreaseDifficulty()
    {
        int currentValue = (int)currentDifficulty;
        currentValue--;
        
        if (currentValue < 0)
            currentValue = System.Enum.GetValues(typeof(CPUDifficulty)).Length - 1;
        
        currentDifficulty = (CPUDifficulty)currentValue;
        CharacterSelect.SetCPUDifficulty(currentDifficulty);
        UpdateDisplay();
    }

    private void IncreaseDifficulty()
    {
        int currentValue = (int)currentDifficulty;
        currentValue++;
        
        if (currentValue >= System.Enum.GetValues(typeof(CPUDifficulty)).Length)
            currentValue = 0;
        
        currentDifficulty = (CPUDifficulty)currentValue;
        CharacterSelect.SetCPUDifficulty(currentDifficulty);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        // Update difficulty text
        if (difficultyText != null)
        {
            difficultyText.text = currentDifficulty.ToString();
        }

        // Update description text
        if (descriptionText != null)
        {
            descriptionText.text = GetDifficultyDescription(currentDifficulty);
        }

        // Update button interactability (optional visual feedback)
        UpdateButtonStates();
    }

    private string GetDifficultyDescription(CPUDifficulty difficulty)
    {
        switch (difficulty)
        {
            case CPUDifficulty.Easy:
                return easyDescription;
            case CPUDifficulty.Medium:
                return mediumDescription;
            case CPUDifficulty.Hard:
                return hardDescription;
            default:
                return mediumDescription;
        }
    }

    private void UpdateButtonStates()
    {
        // Enable/disable buttons based on difficulty bounds (optional)
        // For now, we'll keep them always enabled since we wrap around
        if (decreaseButton != null)
            decreaseButton.interactable = true;
        
        if (increaseButton != null)
            increaseButton.interactable = true;
    }

    // Public methods for external UI systems
    public void SetDifficulty(CPUDifficulty difficulty)
    {
        currentDifficulty = difficulty;
        CharacterSelect.SetCPUDifficulty(currentDifficulty);
        UpdateDisplay();
    }

    public CPUDifficulty GetDifficulty()
    {
        return currentDifficulty;
    }

    // Method to be called from UI dropdown if preferred
    public void OnDifficultyChanged(int difficultyIndex)
    {
        if (difficultyIndex >= 0 && difficultyIndex < System.Enum.GetValues(typeof(CPUDifficulty)).Length)
        {
            SetDifficulty((CPUDifficulty)difficultyIndex);
        }
    }
}