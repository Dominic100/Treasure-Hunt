using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour {
    [Header("Game Panels")]
    public GameObject mainMenuPanel;
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public GameObject loadingPanel;

    [Header("Score Panel")]
    public GameObject scorePanel;
    public TextMeshProUGUI treasureCountText;
    public TextMeshProUGUI scoreText;

    [Header("Status Panel")]
    public GameObject statusPanel;
    public Slider healthSlider;
    public Slider bountySlider;
    public TextMeshProUGUI bountyLevelText;
    public TextMeshProUGUI bountyDescriptionText;
    public Image bountyIcon;
    public Sprite[] bountyIcons;

    [Header("Main Menu Elements")]
    public Button startGameButton;
    public Button optionsButton;
    public Button quitButton;

    [Header("Pause Menu Elements")]
    public Button resumeButton;
    public Button pauseRestartButton;
    public Button pauseMainMenuButton;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Game Over Elements")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI highScoreText;
    public Button restartButton;
    public Button mainMenuButton;

    [Header("Optional Animation")]
    public Animator scoreAnimator;
    public Animator bountyAnimator;
    
    [Header("Loading Screen")]
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;

    [Header("Debug Options")]
    [SerializeField] private bool debugBountyUpdates = true;
    [SerializeField] private float bountyCheckInterval = 0.5f;

    // Manager references
    private BountyManager bountyManager;
    private TreasureManager treasureManager;
    private int lastKnownTreasureCount = -1;
    
    private void Start() {
        Debug.Log("UIManager initializing...");
        
        // Force immediate update of health display
        if (healthSlider != null && GameManager.Instance != null) {
            float currentHealth = GameManager.Instance.GetHealth();
            Debug.Log($"Initializing health display: {currentHealth}/{GameManager.Instance.maxHealth}");
            UpdateHealth(currentHealth);
        }

        // Initialize UI elements
        InitializeUI();
        SetupButtonListeners();
        
        // Find essential managers
        FindBountyManager();
        FindTreasureManager();
        
        // Subscribe to GameManager events after a delay to ensure it's created
        StartCoroutine(DelayedSubscription());
        
        // Start direct monitoring for bounty updates
        StartCoroutine(DirectBountyMonitoring());
        
        // Show main menu at start
        ShowMainMenu();
        
        Debug.Log("UIManager initialization complete");
    }

    private void FindBountyManager() {
        if (bountyManager == null) {
            // First try to get from GameManager if available
            if (GameManager.Instance != null && GameManager.Instance.bountyManager != null) {
                bountyManager = GameManager.Instance.bountyManager;
                Debug.Log("Found BountyManager via GameManager reference");
            } else {
                // Otherwise find in scene
                bountyManager = FindObjectOfType<BountyManager>();
                Debug.Log("Searched scene for BountyManager");
            }
            
            if (bountyManager != null) {
                // Remove any existing connections first to avoid double-subscription
                bountyManager.onBountyLevelChanged.RemoveListener(UpdateBountyLevel); 
                bountyManager.onBountyLevelChanged.AddListener(UpdateBountyLevel);
                Debug.Log("Subscribed to BountyManager events");
                
                // Update UI with current bounty level immediately
                int currentLevel = bountyManager.GetCurrentBountyLevel();
                Debug.Log($"Initial bounty level from manager: {currentLevel}");
                UpdateBountyLevel(currentLevel);
            }
            else {
                Debug.LogWarning("BountyManager not found in scene - will retry later");
                StartCoroutine(RetryFindBountyManager());
            }
        }
    }
    
    private void FindTreasureManager() {
        if (treasureManager == null) {
            // First try to get from GameManager if available
            if (GameManager.Instance != null && GameManager.Instance.treasureManager != null) {
                treasureManager = GameManager.Instance.treasureManager;
                Debug.Log("Found TreasureManager via GameManager reference");
            } else {
                // Otherwise find in scene
                treasureManager = FindObjectOfType<TreasureManager>();
                Debug.Log("Searched scene for TreasureManager");
            }
            
            if (treasureManager != null) {
                Debug.Log($"Found TreasureManager with {treasureManager.GetTreasureCount()} treasures collected");
                lastKnownTreasureCount = treasureManager.GetTreasureCount();
            } else {
                Debug.LogWarning("TreasureManager not found - will retry later");
                StartCoroutine(RetryFindTreasureManager());
            }
        }
    }
    
    private IEnumerator RetryFindBountyManager() {
        yield return new WaitForSeconds(1f);
        FindBountyManager();
    }
    
    private IEnumerator RetryFindTreasureManager() {
        yield return new WaitForSeconds(1f);
        FindTreasureManager();
    }

    // Direct monitoring to ensure bounty level updates regardless of events
    private IEnumerator DirectBountyMonitoring() {
        Debug.Log("Starting direct bounty level monitoring");
        
        // Initial delay to ensure everything is properly initialized
        yield return new WaitForSeconds(2f);
        
        // Continuous monitoring
        while (true) {
            // Ensure we have references to needed managers
            if (treasureManager == null) FindTreasureManager();
            if (bountyManager == null) FindBountyManager();
            
            // If we have treasure manager, check count and update UI directly
            if (treasureManager != null) {
                int currentTreasureCount = treasureManager.GetTreasureCount();
                
                // Only update if treasure count changed
                if (currentTreasureCount != lastKnownTreasureCount) {
                    if (debugBountyUpdates) {
                        Debug.Log($"Direct monitoring: Treasure count changed from {lastKnownTreasureCount} to {currentTreasureCount}");
                    }
                    
                    lastKnownTreasureCount = currentTreasureCount;
                    
                    // Calculate bounty level directly based on treasure count
                    int bountyLevel = CalculateBountyLevel(currentTreasureCount);
                    
                    // Force UI update with new bounty level
                    UpdateBountyLevel(bountyLevel);
                    
                    if (debugBountyUpdates) {
                        Debug.Log($"Direct monitoring: Updated bounty level to {bountyLevel} based on {currentTreasureCount} treasures");
                    }
                    
                    // Also notify bounty manager of the new level to keep systems in sync
                    if (bountyManager != null) {
                        bountyManager.UpdateBountyLevel(currentTreasureCount);
                    }
                }
            }
            
            // Check in regular intervals
            yield return new WaitForSeconds(bountyCheckInterval);
        }
    }
    
    // Direct calculation of bounty level based on treasure count
    private int CalculateBountyLevel(int treasureCount) {
        if (treasureCount >= 75) return 6;
        if (treasureCount >= 50) return 5;
        if (treasureCount >= 35) return 4;
        if (treasureCount >= 20) return 3;
        if (treasureCount >= 10) return 2;
        return 1;
    }
    
    // Public method to force refresh the bounty UI - can be called from debug buttons
    public void RefreshBountyUI() {
        // Check both sources for bounty level
        if (treasureManager != null) {
            int treasureCount = treasureManager.GetTreasureCount();
            int calculatedLevel = CalculateBountyLevel(treasureCount);
            Debug.Log($"RefreshBountyUI: Based on {treasureCount} treasures, calculated level is {calculatedLevel}");
            UpdateBountyLevel(calculatedLevel);
        }
        
        if (bountyManager != null) {
            int managerLevel = bountyManager.GetCurrentBountyLevel();
            Debug.Log($"RefreshBountyUI: BountyManager reports level {managerLevel}");
            
            // Only update if different from what we calculated to avoid flicker
            if (treasureManager == null) {
                UpdateBountyLevel(managerLevel);
            }
        }
    }
    
    private IEnumerator DelayedSubscription() {
        // Wait for GameManager to be initialized
        yield return new WaitForSeconds(0.5f);
        SubscribeToEvents();
    }

    private void InitializeUI() {
        // Initialize UI elements with default values
        UpdateScore(0);
        UpdateTreasureCount(0);
        UpdateHealth(100);
        UpdateBountyLevel(1); // Default bounty level

        // Set initial panel states
        mainMenuPanel.SetActive(true);
        pausePanel.SetActive(false);
        gameOverPanel.SetActive(false);
        scorePanel.SetActive(false);
        statusPanel.SetActive(false);
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // Initialize volume sliders
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    private void SubscribeToEvents() {
        if (GameManager.Instance != null) {
            // Unsubscribe first to prevent duplicate subscriptions
            UnsubscribeFromEvents();
            
            // Subscribe to all events
            GameManager.Instance.onScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.onHealthChanged.AddListener(UpdateHealth);
            GameManager.Instance.onTreasureCountChanged.AddListener(UpdateTreasureCount);
            GameManager.Instance.onGameOver.AddListener(ShowGameOver);
            GameManager.Instance.onGamePaused.AddListener(ShowPauseMenu);
            GameManager.Instance.onGameResumed.AddListener(HidePauseMenu);
            GameManager.Instance.onGameRestarted.AddListener(HandleGameRestart);
            GameManager.Instance.onGameInitialized.AddListener(ShowGameplay);
            
            Debug.Log("Successfully subscribed to GameManager events");
            
            // Get updated references from GameManager
            if (bountyManager == null && GameManager.Instance.bountyManager != null) {
                bountyManager = GameManager.Instance.bountyManager;
                bountyManager.onBountyLevelChanged.AddListener(UpdateBountyLevel);
                Debug.Log("Found and subscribed to BountyManager via GameManager");
            }
            
            if (treasureManager == null && GameManager.Instance.treasureManager != null) {
                treasureManager = GameManager.Instance.treasureManager;
                Debug.Log("Found TreasureManager via GameManager");
            }
        }
        else {
            Debug.LogError("GameManager instance not found! Retrying in 1 second...");
            // Try again after a short delay
            StartCoroutine(RetrySubscription());
        }
    }
    
    private IEnumerator RetrySubscription() {
        yield return new WaitForSeconds(1f);
        SubscribeToEvents();
    }

    private void UnsubscribeFromEvents() {
        if (GameManager.Instance != null) {
            GameManager.Instance.onScoreChanged.RemoveListener(UpdateScore);
            GameManager.Instance.onHealthChanged.RemoveListener(UpdateHealth);
            GameManager.Instance.onTreasureCountChanged.RemoveListener(UpdateTreasureCount);
            GameManager.Instance.onGameOver.RemoveListener(ShowGameOver);
            GameManager.Instance.onGamePaused.RemoveListener(ShowPauseMenu);
            GameManager.Instance.onGameResumed.RemoveListener(HidePauseMenu);
            GameManager.Instance.onGameRestarted.RemoveListener(HandleGameRestart);
            GameManager.Instance.onGameInitialized.RemoveListener(ShowGameplay);
        }
        
        if (bountyManager != null) {
            bountyManager.onBountyLevelChanged.RemoveListener(UpdateBountyLevel);
        }
    }
    
    public void HandleGameRestart() {
        // Hide current panels
        HideAllPanels();
        
        // Reset treasure count tracking
        lastKnownTreasureCount = -1;
        
        // Show loading panel if available
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
    }

    private void SetupButtonListeners() {
        // Main Menu
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartNewGame);
        if (optionsButton != null)
            optionsButton.onClick.AddListener(ShowOptions);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // Pause Menu
        if (resumeButton != null)
            resumeButton.onClick.AddListener(() => GameManager.Instance?.ResumeGame());
        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(RestartGame);
        if (pauseMainMenuButton != null)
            pauseMainMenuButton.onClick.AddListener(ReturnToMainMenu);

        // Game Over Menu
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        // Volume Controls
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(UpdateMusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(UpdateSFXVolume);
    }

    #region UI Updates - Public for Inspector reference
    public void UpdateScore(int newScore) {
        if (scoreText != null) {
            scoreText.text = $"Score: {newScore:N0}";
            if (scoreAnimator != null)
                scoreAnimator.SetTrigger("ScoreChanged");
        }
    }

    public void UpdateTreasureCount(int count) {
        if (treasureCountText != null) {
            treasureCountText.text = $"Treasures: {count}";
        }
        
        // When treasure count updates, check if bounty level should change
        // This is a second path to ensure bounty updates work
        if (count != lastKnownTreasureCount) {
            lastKnownTreasureCount = count;
            int newBountyLevel = CalculateBountyLevel(count);
            UpdateBountyLevel(newBountyLevel);
            
            if (debugBountyUpdates) {
                Debug.Log($"TreasureCount changed to {count} - updated bounty level to {newBountyLevel}");
            }
        }
    }

    public void UpdateHealth(float healthValue) {
        if (healthSlider != null)
            healthSlider.value = healthValue / 100f; // Assuming max health is 100
    }
    
    public void UpdateBountyLevel(int level) {
        if (debugBountyUpdates) {
            Debug.Log($"UIManager.UpdateBountyLevel called with level: {level}");
        }
        
        // Update level text
        if (bountyLevelText != null) {
            bountyLevelText.text = $"BOUNTY: {level}";
        }
        
        // Update description if we have a bounty manager
        if (bountyManager != null && bountyDescriptionText != null) {
            string description = bountyManager.GetCurrentBountyDescription();
            if (!string.IsNullOrEmpty(description)) {
                bountyDescriptionText.text = description;
            } else {
                bountyDescriptionText.text = $"Level {level} Bounty";
            }
        }
        
        // Update progress bar
        if (bountySlider != null) {
            bountySlider.value = (level - 1) / 5f; // 5 is max levels range (1-6)
        }
        
        // Update icon if we have icons array and a valid index
        if (bountyIcon != null && bountyIcons != null && level > 0 && level <= bountyIcons.Length) {
            bountyIcon.sprite = bountyIcons[level - 1];
        }
        
        // Animate if available
        if (bountyAnimator != null) {
            bountyAnimator.SetTrigger("BountyChanged");
        }
    }
    
    public void HideAllPanels() {
        mainMenuPanel.SetActive(false);
        pausePanel.SetActive(false);
        gameOverPanel.SetActive(false);
        scorePanel.SetActive(false);
        statusPanel.SetActive(false);
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }
    #endregion

    #region Panel Management - Public for Inspector reference
    public void ShowMainMenu() {
        HideAllPanels();
        mainMenuPanel.SetActive(true);
        Time.timeScale = 1f;
    }

    public void ShowGameplay() {
        HideAllPanels();
        scorePanel.SetActive(true);
        statusPanel.SetActive(true);
        
        // Force UI refresh when game starts
        StartCoroutine(DelayedBountyRefresh());
    }
    
    private IEnumerator DelayedBountyRefresh() {
        yield return new WaitForSeconds(0.5f);
        RefreshBountyUI();
    }

    public void ShowPauseMenu() {
        pausePanel.SetActive(true);

        // Update volume sliders to current values
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    public void HidePauseMenu() {
        pausePanel.SetActive(false);
    }

    public void ShowGameOver() {
        HideAllPanels();
        gameOverPanel.SetActive(true);

        if (GameManager.Instance != null) {
            if (finalScoreText != null)
                finalScoreText.text = $"Final Score: {GameManager.Instance.GetScore():N0}";

            // Update high score
            int highScore = PlayerPrefs.GetInt("HighScore", 0);
            if (GameManager.Instance.GetScore() > highScore) {
                highScore = GameManager.Instance.GetScore();
                PlayerPrefs.SetInt("HighScore", highScore);
                PlayerPrefs.Save();
            }

            if (highScoreText != null)
                highScoreText.text = $"High Score: {highScore:N0}";
        }
    }
    
    public void ShowLoading() {
        HideAllPanels();
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
    }
    #endregion

    #region Button Actions
    public void StartNewGame() {
        ShowLoading();
        if (GameManager.Instance != null) {
            GameManager.Instance.StartGame();
        }
        else {
            Debug.LogError("Cannot start game: GameManager.Instance is null!");
        }
    }

    public void RestartGame() {
        ShowLoading();
        if (GameManager.Instance != null) {
            GameManager.Instance.RestartGame();
        }
    }

    public void ReturnToMainMenu() {
        if (GameManager.Instance != null) {
            GameManager.Instance.QuitToMainMenu();
        }
        ShowMainMenu();
    }

    public void ShowOptions() {
        // Implement options menu functionality
        Debug.Log("Options menu not implemented yet");
    }

    public void QuitGame() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    #endregion

    #region Volume Controls
    public void UpdateMusicVolume(float volume) {
        PlayerPrefs.SetFloat("MusicVolume", volume);
        PlayerPrefs.Save();
        // Add actual music volume control here
    }

    public void UpdateSFXVolume(float volume) {
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
        // Add actual SFX volume control here
    }
    #endregion

    private void OnDestroy() {
        UnsubscribeFromEvents();
    }
}