using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }

    [System.Serializable]
    public class GameState {
        public bool isGameStarted = false;
        public int score;
        public int treasuresCollected;
        public float health;
        public bool isGameOver;
        public bool isPaused;
    }

    [Header("Game Systems")]
    public TreasureManager treasureManager;
    public BountyManager bountyManager;
    public GameObject playerObject; // Reference to the player GameObject

    [Header("Game Environment")]
    [Tooltip("Game objects to enable/disable in sequence when game starts. Order matters!")]
    public GameObject[] gameEnvironmentObjects;
    [Tooltip("Time to wait between enabling consecutive environment objects")]
    public float environmentInitDelay = 0.1f;

    [Header("Game Settings")]
    public float maxHealth = 100f;
    public float startingHealth = 100f;
    public bool enableCheats = false;

    // Events for UI
    public UnityEvent<int> onScoreChanged;
    public UnityEvent<float> onHealthChanged;
    public UnityEvent<int> onTreasureCountChanged;
    public UnityEvent onGameOver;
    public UnityEvent onGamePaused;
    public UnityEvent onGameResumed;
    public UnityEvent onGameRestarted;
    public UnityEvent onGameInitialized; // New event for when environment is fully loaded

    // Game state
    private GameState gameState = new GameState();

    void Awake() {
        // Singleton pattern
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEvents();
        }
        else {
            Destroy(gameObject);
            return;
        }
    }

    private void InitializeEvents() {
        onScoreChanged = onScoreChanged ?? new UnityEvent<int>();
        onHealthChanged = onHealthChanged ?? new UnityEvent<float>();
        onTreasureCountChanged = onTreasureCountChanged ?? new UnityEvent<int>();
        onGameOver = onGameOver ?? new UnityEvent();
        onGamePaused = onGamePaused ?? new UnityEvent();
        onGameResumed = onGameResumed ?? new UnityEvent();
        onGameRestarted = onGameRestarted ?? new UnityEvent();
        onGameInitialized = onGameInitialized ?? new UnityEvent();
    }

    void Start() {
        // Disable all game environment objects at start
        DisableGameEnvironment();
        
        // Initialize game state
        InitializeGameState();
        
        Debug.Log("GameManager initialized and ready");
    }

    private void InitializeGameState() {
        // Initialize game state without starting the game
        gameState.isGameStarted = false;
        gameState.score = 0;
        gameState.treasuresCollected = 0;
        gameState.health = startingHealth;
        gameState.isGameOver = false;
        gameState.isPaused = false;

        // Initialize UI values
        onScoreChanged?.Invoke(gameState.score);
        onHealthChanged?.Invoke(gameState.health);
        onTreasureCountChanged?.Invoke(gameState.treasuresCollected);

        // Ensure normal time scale
        Time.timeScale = 1f;
    }

    public void StartGame() {
        if (!gameState.isGameStarted) {
            // Start enabling game environment objects
            StartCoroutine(EnableGameEnvironmentSequence());
        }
    }

    // Enable game objects in sequence
    private IEnumerator EnableGameEnvironmentSequence() {
        Debug.Log("Starting game environment initialization");
        
        // Enable each environment object in order
        if (gameEnvironmentObjects != null && gameEnvironmentObjects.Length > 0) {
            for (int i = 0; i < gameEnvironmentObjects.Length; i++) {
                if (gameEnvironmentObjects[i] != null) {
                    gameEnvironmentObjects[i].SetActive(true);
                    Debug.Log($"Enabled environment object: {gameEnvironmentObjects[i].name}");
                    
                    // Wait before enabling the next object
                    yield return new WaitForSeconds(environmentInitDelay);
                }
            }
        }
        
        // Now initialize the player and treasure system
        gameState.isGameStarted = true;
        
        // Subscribe to treasure collection events
        if (treasureManager != null) {
            treasureManager.OnTreasureCollected -= HandleTreasureCollection;
            treasureManager.OnTreasureCollected += HandleTreasureCollection;
            treasureManager.Initialize();
            Debug.Log("Treasure manager initialized");
        }
        else {
            Debug.LogError("TreasureManager reference missing in GameManager!");
        }

        // Initialize the bounty system - make sure this works
        if (bountyManager != null) {
            bountyManager.Initialize(); // Added this line - call Initialize() explicitly
            Debug.Log("Bounty manager initialized");
        }
        else {
            Debug.LogWarning("BountyManager reference missing in GameManager!");
        }
        
        // Notify that game initialization is complete
        onGameInitialized?.Invoke();
        Debug.Log("Game fully initialized and started");
    }

    private void DisableGameEnvironment() {
        // Disable player first
        if (playerObject != null) {
            playerObject.SetActive(false);
        }
        
        // Disable all environment objects
        if (gameEnvironmentObjects != null) {
            foreach (var obj in gameEnvironmentObjects) {
                if (obj != null) {
                    obj.SetActive(false);
                }
            }
        }
    }

    private void HandleTreasureCollection(int treasureValue) {
        if (gameState.isGameOver || !gameState.isGameStarted) return;

        gameState.score += treasureValue;
        gameState.treasuresCollected++;

        onScoreChanged?.Invoke(gameState.score);
        onTreasureCountChanged?.Invoke(gameState.treasuresCollected);
    }

    #region Game State Management
    public void AddScore(int points) {
        if (gameState.isGameOver || !gameState.isGameStarted) return;

        gameState.score += points;
        onScoreChanged?.Invoke(gameState.score);
    }

    public void UpdateHealth(float healthChange) {
        if (gameState.isGameOver || !gameState.isGameStarted) return;

        float oldHealth = gameState.health;
        gameState.health = Mathf.Clamp(gameState.health + healthChange, 0f, maxHealth);
        
        // Debug health changes
        if (healthChange < 0) {
            Debug.LogWarning($"Player took {-healthChange} damage! Health: {oldHealth} → {gameState.health}");
        } else if (healthChange > 0) {
            Debug.Log($"Player healed {healthChange}! Health: {oldHealth} → {gameState.health}");
        }
        
        onHealthChanged?.Invoke(gameState.health);

        if (gameState.health <= 0 && !enableCheats) {
            Debug.LogError("Player died - calling GameOver()");
            GameOver();
        }
    }

    public void SetHealth(float newHealth) {
        if (gameState.isGameOver || !gameState.isGameStarted) return;

        gameState.health = Mathf.Clamp(newHealth, 0f, maxHealth);
        onHealthChanged?.Invoke(gameState.health);

        if (gameState.health <= 0 && !enableCheats) {
            GameOver();
        }
    }

    public void PauseGame() {
        if (gameState.isGameOver || !gameState.isGameStarted) return;

        if (!gameState.isPaused) {
            gameState.isPaused = true;
            Time.timeScale = 0f;

            // Disable player input and movement
            if (playerObject != null) {
                var playerComponents = playerObject.GetComponents<MonoBehaviour>();
                foreach (var component in playerComponents) {
                    if (component != this) {
                        component.enabled = false;
                    }
                }
            }

            onGamePaused?.Invoke();
        }
    }

    public void ResumeGame() {
        if (gameState.isPaused) {
            gameState.isPaused = false;
            Time.timeScale = 1f;

            // Re-enable player input and movement
            if (playerObject != null) {
                var playerComponents = playerObject.GetComponents<MonoBehaviour>();
                foreach (var component in playerComponents) {
                    if (component != this) {
                        component.enabled = true;
                    }
                }
            }

            onGameResumed?.Invoke();
        }
    }

    public void RestartGame() {
        // First disable everything
        StopAllCoroutines();
        DisableGameEnvironment();
        
        // Reset game state
        InitializeGameState();

        // Reset treasure system
        if (treasureManager != null) {
            treasureManager.OnTreasureCollected -= HandleTreasureCollection;
        }
        
        // Start enabling environment again
        StartCoroutine(EnableGameEnvironmentSequence());
        
        // Trigger restart event
        onGameRestarted?.Invoke();
    }

    public void QuitToMainMenu() {
        // Stop all running coroutines
        StopAllCoroutines();
        
        // Reset game state
        gameState.isGameStarted = false;
        gameState.isGameOver = false;
        gameState.isPaused = false;
        
        // Disable all game objects
        DisableGameEnvironment();
        
        // Reset time scale
        Time.timeScale = 1f;
        
        // Unsubscribe from events
        if (treasureManager != null) {
            treasureManager.OnTreasureCollected -= HandleTreasureCollection;
        }
    }

    public void GameOver() {
        if (gameState.isGameOver) return;

        gameState.isGameOver = true;
        Time.timeScale = 0f;

        // Disable player
        if (playerObject != null) {
            playerObject.SetActive(false);
        }

        onGameOver?.Invoke();
    }
    #endregion

    #region Getters
    public int GetScore() => gameState.score;
    public float GetHealth() => gameState.health;
    public int GetTreasureCount() => gameState.treasuresCollected;
    public bool IsGameOver() => gameState.isGameOver;
    public bool IsPaused() => gameState.isPaused;
    public bool IsGameStarted() => gameState.isGameStarted;
    #endregion

    void Update() {
        // Only handle pause input if game is started
        if (gameState.isGameStarted && Input.GetKeyDown(KeyCode.Escape)) {
            if (gameState.isPaused) {
                ResumeGame();
            }
            else {
                PauseGame();
            }
        }

        // Cheat codes for testing (only in debug builds)
        if (enableCheats && Debug.isDebugBuild && gameState.isGameStarted) {
            if (Input.GetKeyDown(KeyCode.H)) // Health restore
            {
                SetHealth(maxHealth);
            }
            if (Input.GetKeyDown(KeyCode.K)) // Kill player
            {
                SetHealth(0);
            }
        }
    }

    private void OnDestroy() {
        // Clear all event listeners
        onScoreChanged?.RemoveAllListeners();
        onHealthChanged?.RemoveAllListeners();
        onTreasureCountChanged?.RemoveAllListeners();
        onGameOver?.RemoveAllListeners();
        onGamePaused?.RemoveAllListeners();
        onGameResumed?.RemoveAllListeners();
        onGameRestarted?.RemoveAllListeners();
        onGameInitialized?.RemoveAllListeners();

        // Unsubscribe from TreasureManager events
        if (treasureManager != null) {
            treasureManager.OnTreasureCollected -= HandleTreasureCollection;
        }

        // Clear singleton instance
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable() {
        // Only clean up if this is the instance
        if (Instance == this) {
            OnDestroy();
        }
    }
}