using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BountyManager : MonoBehaviour
{
    [System.Serializable]
    public class BountyLevel
    {
        public int requiredTreasures;
        public int airChaseCount;
        public int groundChaseCount;
        public int groundHopCount;
        [TextArea]
        public string description;
    }

    [Header("Bounty Settings")]
    [SerializeField] private int currentBountyLevel = 1;
    [SerializeField] private BountyLevel[] bountyLevels;

    [Header("Enemy References")]
    [SerializeField] private GameObject[] airChaseEnemies;
    [SerializeField] private GameObject[] groundChaseEnemies;
    [SerializeField] private GameObject[] groundHopEnemies;

    [Header("UI Events")]
    public UnityEvent<int> onBountyLevelChanged;
    
    private int previousTreasureCount = 0;
    private bool isInitialized = false;

    private void Awake()
    {
        // Initialize events
        if (onBountyLevelChanged == null)
            onBountyLevelChanged = new UnityEvent<int>();
            
        // Configure default bounty levels if not set in inspector
        if (bountyLevels == null || bountyLevels.Length == 0)
        {
            ConfigureDefaultBountyLevels();
        }
    }

    void OnEnable()
    {
        Debug.Log("BountyManager OnEnable - attempting to subscribe to events");
        SubscribeToEvents();
    }

    void OnDisable()
    {
        Debug.Log("BountyManager OnDisable - unsubscribing from events");
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onTreasureCountChanged.AddListener(UpdateBountyLevel);
            GameManager.Instance.onGameRestarted.AddListener(ResetBounty);
            Debug.Log("BountyManager successfully subscribed to GameManager events");
        }
        else
        {
            Debug.LogWarning("GameManager instance not found - couldn't subscribe to events");
            // Try again in a moment
            StartCoroutine(RetrySubscription());
        }
    }

    private IEnumerator RetrySubscription()
    {
        yield return new WaitForSeconds(0.5f);
        SubscribeToEvents();
    }

    private void UnsubscribeFromEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onTreasureCountChanged.RemoveListener(UpdateBountyLevel);
            GameManager.Instance.onGameRestarted.RemoveListener(ResetBounty);
        }
    }

    public void Initialize() {
        if (!isInitialized) {
            DisableAllEnemies();
            ApplyBountyLevel(1); // Start at level 1
            
            // Make sure we're explicitly firing the event
            Debug.Log("BountyManager initializing - firing onBountyLevelChanged with level 1");
            if (onBountyLevelChanged == null) {
                onBountyLevelChanged = new UnityEvent<int>();
            }
            onBountyLevelChanged.Invoke(1);
            
            isInitialized = true;
        }
    }

    public void ResetBounty()
    {
        previousTreasureCount = 0;
        currentBountyLevel = 1;
        DisableAllEnemies();
        ApplyBountyLevel(currentBountyLevel);
        
        // Important: Make sure we're firing the event!
        onBountyLevelChanged?.Invoke(currentBountyLevel);
        Debug.Log($"BountyManager reset to level {currentBountyLevel}");
    }

    public void UpdateBountyLevel(int treasureCount) {
        // Debug log to check if this method is being called
        Debug.Log($"UpdateBountyLevel called with treasureCount: {treasureCount}, previous: {previousTreasureCount}");
        
        // Only proceed if treasure count has increased
        if (treasureCount <= previousTreasureCount)
            return;
            
        previousTreasureCount = treasureCount;
        
        // Determine new bounty level based on treasure count
        int newLevel = 1; // Default level
        
        if (treasureCount >= 75) {
            newLevel = 6;
        } else if (treasureCount >= 50) {
            newLevel = 5;
        } else if (treasureCount >= 35) {
            newLevel = 4;
        } else if (treasureCount >= 20) {
            newLevel = 3;
        } else if (treasureCount >= 10) {
            newLevel = 2;
        }
        
        Debug.Log($"Calculated new bounty level: {newLevel} for treasure count: {treasureCount}");
        
        // Only increase level, never decrease (as per requirement)
        if (newLevel > currentBountyLevel) {
            Debug.Log($"Setting bounty level from {currentBountyLevel} to {newLevel}");
            SetBountyLevel(newLevel);
        }
    }

    public void SetBountyLevel(int level)
    {
        if (level < 1 || level > bountyLevels.Length)
        {
            Debug.LogWarning($"Invalid bounty level: {level}. Must be between 1 and {bountyLevels.Length}.");
            return;
        }

        currentBountyLevel = level;
        Debug.Log($"Setting bounty level to {currentBountyLevel}");
        
        // Apply the new bounty level
        ApplyBountyLevel(currentBountyLevel);
        
        // Notify UI or other systems - CRITICAL! 
        onBountyLevelChanged?.Invoke(currentBountyLevel);
    }

    private void ApplyBountyLevel(int level)
    {
        if (level < 1 || level > bountyLevels.Length)
            return;
            
        // Get the bounty configuration (adjust for 0-based array)
        BountyLevel config = bountyLevels[level - 1];
        
        // Configure AirChase enemies
        ConfigureEnemies(airChaseEnemies, config.airChaseCount);
        
        // Configure GroundChase enemies
        ConfigureEnemies(groundChaseEnemies, config.groundChaseCount);
        
        // Configure GroundHop enemies
        ConfigureEnemies(groundHopEnemies, config.groundHopCount);
        
        Debug.Log($"Applied bounty level {level}: {config.description}");
        Debug.Log($"Activated: {config.airChaseCount} air chase, {config.groundChaseCount} ground chase, {config.groundHopCount} ground hop enemies");
    }

    private void ConfigureEnemies(GameObject[] enemies, int activeCount)
    {
        if (enemies == null || enemies.Length == 0)
        {
            Debug.LogWarning("Enemy array is null or empty!");
            return;
        }
            
        // Never disable enemies that were already active (only enable more)
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                // Only activate enemies that should be active now
                // Never deactivate already active enemies (as per your requirement)
                if (i < activeCount && !enemies[i].activeSelf) {
                    enemies[i].SetActive(true);
                    Debug.Log($"Activated enemy: {enemies[i].name}");
                }
            }
            else
            {
                Debug.LogWarning($"Enemy at index {i} is null!");
            }
        }
    }

    private void DisableAllEnemies()
    {
        // This is only used at game start/restart
        if (airChaseEnemies != null) {
            foreach (var enemy in airChaseEnemies) {
                if (enemy != null) enemy.SetActive(false);
            }
        }
        
        if (groundChaseEnemies != null) {
            foreach (var enemy in groundChaseEnemies) {
                if (enemy != null) enemy.SetActive(false);
            }
        }
        
        if (groundHopEnemies != null) {
            foreach (var enemy in groundHopEnemies) {
                if (enemy != null) enemy.SetActive(false);
            }
        }
        
        Debug.Log("All enemies disabled for new game/restart");
    }

    private void ConfigureDefaultBountyLevels()
    {
        // Create default bounty level configuration
        bountyLevels = new BountyLevel[6];
        
        // Level 1 (Starting level): 1 of each type
        bountyLevels[0] = new BountyLevel
        {
            requiredTreasures = 0,
            airChaseCount = 1,
            groundChaseCount = 1,
            groundHopCount = 1,
            description = "Bounty Level 1: Minimal enemy presence."
        };
        
        // Level 2 (10-19 treasures): 1 more air chase enemy
        bountyLevels[1] = new BountyLevel
        {
            requiredTreasures = 10,
            airChaseCount = 2,
            groundChaseCount = 1,
            groundHopCount = 1,
            description = "Bounty Level 2: Air patrols increased."
        };
        
        // Level 3 (20-34 treasures): 1 more ground hop enemy
        bountyLevels[2] = new BountyLevel
        {
            requiredTreasures = 20,
            airChaseCount = 2,
            groundChaseCount = 1,
            groundHopCount = 2,
            description = "Bounty Level 3: Additional ground units deployed."
        };
        
        // Level 4 (35-49 treasures): 1 more of each type
        bountyLevels[3] = new BountyLevel
        {
            requiredTreasures = 35,
            airChaseCount = 3,
            groundChaseCount = 2,
            groundHopCount = 3,
            description = "Bounty Level 4: Security forces mobilizing."
        };
        
        // Level 5 (50-74 treasures): All remaining enemies
        bountyLevels[4] = new BountyLevel
        {
            requiredTreasures = 50,
            airChaseCount = 3,
            groundChaseCount = 3,
            groundHopCount = 5,
            description = "Bounty Level 5: Maximum security alert!"
        };
        
        // Level 6 (75+ treasures): Keep the same as level 5 for now
        bountyLevels[5] = new BountyLevel
        {
            requiredTreasures = 75,
            airChaseCount = 3,
            groundChaseCount = 3,
            groundHopCount = 5,
            description = "Bounty Level 6: EXTREME - Special implementation pending."
        };
    }

    // For UI display
    public int GetCurrentBountyLevel()
    {
        return currentBountyLevel;
    }
    
    public string GetCurrentBountyDescription()
    {
        if (currentBountyLevel < 1 || currentBountyLevel > bountyLevels.Length)
            return "Unknown bounty level";
            
        return bountyLevels[currentBountyLevel - 1].description;
    }
    
    public float GetBountyProgress()
    {
        // Return a value between 0 and 1 for UI slider
        return (currentBountyLevel - 1) / 5f;
    }
    
    public int GetNextBountyThreshold()
    {
        if (currentBountyLevel >= bountyLevels.Length)
            return -1; // Max level reached
            
        return bountyLevels[currentBountyLevel].requiredTreasures;
    }

    public void ForceUIUpdate() {
        Debug.Log($"Forcing UI update with current bounty level: {currentBountyLevel}");
        if (onBountyLevelChanged == null) {
            onBountyLevelChanged = new UnityEvent<int>();
        }
        onBountyLevelChanged.Invoke(currentBountyLevel);
    }
}