using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject gameplayPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Gameplay UI Elements")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI bountyLevelText;
    [SerializeField] private TextMeshProUGUI bountyDescriptionText;
    [SerializeField] private TextMeshProUGUI treasureCountText;
    
    [Header("Effects")]
    [SerializeField] private bool flashHealthOnDamage = true;
    [SerializeField] private Color damagedHealthColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private bool animateBountyLevelChange = true;
    [SerializeField] private float bountyLevelChangeAnimationDuration = 0.5f;
    
    [Header("Component References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TreasureManager treasureManager;

    // Cached original colors
    private Color healthTextOriginalColor;
    private Color bountyTextOriginalColor;
    
    // Control variables
    private bool isPaused = false;
    private bool isGameOver = false;
    private int displayedBountyLevel = 1;
    
    // Bounty level descriptions
    private string[] bountyDescriptions = new string[] {
        "Unwanted", // Level 1
        "Troublemaker", // Level 2
        "Nuisance", // Level 3 
        "Thief", // Level 4
        "Outlaw", // Level 5
        "Legendary Pirate" // Level 6
    };

    private void Awake()
    {
        // Find necessary components if not assigned
        if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (treasureManager == null) treasureManager = FindObjectOfType<TreasureManager>();
        
        // Cache original colors
        if (healthText != null) healthTextOriginalColor = healthText.color;
        if (bountyLevelText != null) bountyTextOriginalColor = bountyLevelText.color;
        
        // Show main menu at start, hide other panels
        ShowMainMenu();
    }
    
    private void OnEnable()
    {
        // Subscribe to events
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthDisplay;
            playerHealth.OnPlayerDeath += HandlePlayerDeath;
        }
        
        if (treasureManager != null)
        {
            treasureManager.OnTreasureCollected += UpdateTreasureDisplay;
        }
        
        // Register for input
        InputManager.OnPausePressed += TogglePause;
    }
    
    private void OnDisable()
    {
        // Unsubscribe from events
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthDisplay;
            playerHealth.OnPlayerDeath -= HandlePlayerDeath;
        }
        
        if (treasureManager != null)
        {
            treasureManager.OnTreasureCollected -= UpdateTreasureDisplay;
        }
        
        // Unregister from input
        InputManager.OnPausePressed -= TogglePause;
    }

    private void Start()
    {
        // Initial UI update
        UpdateAllDisplays();
    }

    private void Update()
    {
        // Check for bounty level changes (since GameManager doesn't expose an event for this)
        if (gameManager != null && gameManager.CurrentBountyLevel != displayedBountyLevel)
        {
            UpdateBountyDisplay(gameManager.CurrentBountyLevel);
        }
        
        // Manual pause toggle (in case InputManager isn't implemented)
        if (Input.GetKeyDown(KeyCode.Escape) && !isGameOver)
        {
            TogglePause();
        }
    }

    #region Display Updates
    
    private void UpdateAllDisplays()
    {
        // Update health
        if (playerHealth != null)
        {
            UpdateHealthDisplay(playerHealth.GetHealthPercentage() * 100f);
        }
        
        // Update bounty level
        if (gameManager != null)
        {
            UpdateBountyDisplay(gameManager.CurrentBountyLevel);
        }
        
        // Update treasure count
        if (treasureManager != null)
        {
            UpdateTreasureDisplay();
        }
    }
    
    private void UpdateHealthDisplay(float currentHealth)
    {
        if (healthText != null)
        {
            // Display only the percentage value
            int healthPercentage = Mathf.RoundToInt(currentHealth);
            healthText.text = $"{healthPercentage}";
            
            // Optional: Flash health text when damaged
            if (flashHealthOnDamage)
            {
                StopCoroutine("FlashHealthText");
                StartCoroutine(FlashHealthText());
            }
        }
    }
    
    private void UpdateBountyDisplay(int level)
    {
        // Clamp level to valid range
        level = Mathf.Clamp(level, 1, bountyDescriptions.Length);
        
        // Update cached level
        displayedBountyLevel = level;
        
        // Update level text - only show the number
        if (bountyLevelText != null)
        {
            bountyLevelText.text = $"{level}";
            
            // Animate level change
            if (animateBountyLevelChange)
            {
                StopCoroutine("AnimateBountyLevelChange");
                StartCoroutine(AnimateBountyLevelChange());
            }
        }
        
        // Update description text (this remains unchanged)
        if (bountyDescriptionText != null)
        {
            bountyDescriptionText.text = bountyDescriptions[level - 1];
        }
    }
    
    private void UpdateTreasureDisplay()
    {
        if (treasureCountText != null && treasureManager != null)
        {
            // Display only the count number
            int treasureCount = treasureManager.GetCollectedTreasures();
            treasureCountText.text = $"{treasureCount}";
        }
    }
    
    private IEnumerator FlashHealthText()
    {
        healthText.color = damagedHealthColor;
        yield return new WaitForSeconds(flashDuration);
        healthText.color = healthTextOriginalColor;
    }
    
    private IEnumerator AnimateBountyLevelChange()
    {
        // Scale animation
        Vector3 originalScale = bountyLevelText.transform.localScale;
        Vector3 targetScale = originalScale * 1.3f;
        
        // Color animation
        Color targetColor = Color.yellow;
        
        // Animation up
        float elapsed = 0f;
        while (elapsed < bountyLevelChangeAnimationDuration / 2)
        {
            float t = elapsed / (bountyLevelChangeAnimationDuration / 2);
            bountyLevelText.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            bountyLevelText.color = Color.Lerp(bountyTextOriginalColor, targetColor, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Animation down
        elapsed = 0f;
        while (elapsed < bountyLevelChangeAnimationDuration / 2)
        {
            float t = elapsed / (bountyLevelChangeAnimationDuration / 2);
            bountyLevelText.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            bountyLevelText.color = Color.Lerp(targetColor, bountyTextOriginalColor, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Reset to original values
        bountyLevelText.transform.localScale = originalScale;
        bountyLevelText.color = bountyTextOriginalColor;
    }
    
    #endregion
    
    #region Game State Management
    
    public void ShowMainMenu()
    {
        // Show main menu, hide others
        gameplayPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        gameOverPanel.SetActive(false);
        pausePanel.SetActive(false);
        
        // Ensure game is paused
        Time.timeScale = 0f;
        isPaused = true;
    }
    
    public void StartGame()
    {
        // Hide main menu, show gameplay
        mainMenuPanel.SetActive(false);
        gameplayPanel.SetActive(true);
        
        // Reset game state
        isGameOver = false;
        
        // Unpause game
        Time.timeScale = 1f;
        isPaused = false;
    }
    
    public void TogglePause()
    {
        if (isGameOver) return; // Don't allow pause if game is over
        
        isPaused = !isPaused;
        
        if (isPaused)
        {
            // Pause game
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
        }
        else
        {
            // Resume game
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
        }
    }
    
    private void HandlePlayerDeath()
    {
        isGameOver = true;
        
        // Show game over panel
        gameOverPanel.SetActive(true);
        
        // Pause game
        Time.timeScale = 0f;
    }
    
    public void RestartGame()
    {
        // Reload current scene
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
    #endregion
}

// This is a simple InputManager to handle pause input
// You can replace this with your existing input system
public static class InputManager
{
    public static System.Action OnPausePressed;
    
    // Call this method from your input system when the pause button is pressed
    public static void TriggerPausePressed()
    {
        OnPausePressed?.Invoke();
    }
}