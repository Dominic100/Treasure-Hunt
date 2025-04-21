using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private TreasureManager treasureManager;

    [Header("Enemies - Air Chase")]
    [SerializeField] private List<AirEnemyChase> airEnemies = new List<AirEnemyChase>();

    [Header("Enemies - Ground Fixed Chase")]
    [SerializeField] private List<GroundEnemyFixedChase> groundFixedEnemies = new List<GroundEnemyFixedChase>();

    [Header("Enemies - Ground Hop Chase")]
    [SerializeField] private List<GroundHopEnemyChase> groundHopEnemies = new List<GroundHopEnemyChase>();

    [Header("Bounty Level Thresholds")]
    [SerializeField] private int level2Threshold = 10;
    [SerializeField] private int level3Threshold = 20;
    [SerializeField] private int level4Threshold = 35;
    [SerializeField] private int level5Threshold = 50;
    [SerializeField] private int level6Threshold = 80;

    [Header("Level 6 Enemy Properties")]
    [SerializeField] private float airChaseSpeed = 5f;
    [SerializeField] private float airRotationSpeed = 5f;
    [SerializeField] private float groundHopMoveSpeed = 6f;
    [SerializeField] private float groundHopDuration = 1.5f;
    [SerializeField] private float groundFixedMoveSpeed = 7f;
    [SerializeField] private float groundFixedRandomTeleportMaxHeight = 3f;
    [SerializeField] private float groundFixedRandomTeleportMinHeight = -3f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private int currentBountyLevel = 1;
    private int previousTreasureCount = 0;

    private void Start()
    {
        // Validate manager references
        if (treasureManager == null)
        {
            treasureManager = FindObjectOfType<TreasureManager>();
            if (treasureManager == null)
            {
                Debug.LogError("No TreasureManager found in scene!");
                enabled = false;
                return;
            }
        }

        // Subscribe to treasure collection events
        treasureManager.OnTreasureCollected += CheckBountyLevel;

        // Configure initial enemy state
        ConfigureEnemiesForLevel1();
    }

    public int CurrentBountyLevel => currentBountyLevel;

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (treasureManager != null)
        {
            treasureManager.OnTreasureCollected -= CheckBountyLevel;
        }
    }

    private void CheckBountyLevel()
    {
        int treasureCount = treasureManager.GetCollectedTreasures();
        
        // Check if count has actually increased (shouldn't be needed but just in case)
        if (treasureCount <= previousTreasureCount)
        {
            return;
        }
        
        previousTreasureCount = treasureCount;

        // Determine new bounty level
        int newBountyLevel = 1;

        if (treasureCount >= level6Threshold)
            newBountyLevel = 6;
        else if (treasureCount >= level5Threshold)
            newBountyLevel = 5;
        else if (treasureCount >= level4Threshold)
            newBountyLevel = 4;
        else if (treasureCount >= level3Threshold)
            newBountyLevel = 3;
        else if (treasureCount >= level2Threshold)
            newBountyLevel = 2;

        // Update bounty level if changed
        if (newBountyLevel > currentBountyLevel)
        {
            currentBountyLevel = newBountyLevel;
            LogBountyChange(currentBountyLevel, treasureCount);
            UpdateEnemiesForCurrentLevel();
        }
    }

    private void LogBountyChange(int level, int treasureCount)
    {
        if (debugMode)
        {
            Debug.Log($"Bounty increased to Level {level} at {treasureCount} treasures collected!");
        }
    }

    private void ConfigureEnemiesForLevel1()
    {
        // Disable all enemies first
        DisableAllEnemies();

        // Enable 1 of each type
        if (airEnemies.Count > 0)
            EnableEnemy(airEnemies[0]);

        if (groundFixedEnemies.Count > 0)
            EnableEnemy(groundFixedEnemies[0]);

        if (groundHopEnemies.Count > 0)
            EnableEnemy(groundHopEnemies[0]);

        currentBountyLevel = 1;
        if (debugMode)
            Debug.Log("Configured enemies for Level 1 bounty");
    }

    private void UpdateEnemiesForCurrentLevel()
    {
        switch (currentBountyLevel)
        {
            case 2:
                // Enable one more air enemy
                if (airEnemies.Count > 1)
                    EnableEnemy(airEnemies[1]);
                break;

            case 3:
                // Enable one more ground hop enemy
                if (groundHopEnemies.Count > 1)
                    EnableEnemy(groundHopEnemies[1]);
                break;

            case 4:
                // Enable one more of each type
                if (airEnemies.Count > 2)
                    EnableEnemy(airEnemies[2]);
                if (groundFixedEnemies.Count > 1)
                    EnableEnemy(groundFixedEnemies[1]);
                if (groundHopEnemies.Count > 2)
                    EnableEnemy(groundHopEnemies[2]);
                break;

            case 5:
                // Enable all remaining enemies
                for (int i = 0; i < airEnemies.Count; i++)
                    EnableEnemy(airEnemies[i]);
                for (int i = 0; i < groundFixedEnemies.Count; i++)
                    EnableEnemy(groundFixedEnemies[i]);
                for (int i = 0; i < groundHopEnemies.Count; i++)
                    EnableEnemy(groundHopEnemies[i]);
                break;

            case 6:
                // Increase difficulty of all active enemies
                SetLevel6Difficulty();
                break;
        }
    }

    private void SetLevel6Difficulty()
    {
        // Increase difficulty parameters for all active enemies
        foreach (var enemy in airEnemies)
        {
            if (enemy.gameObject.activeSelf)
            {
                enemy.ChaseSpeed = airChaseSpeed;
                enemy.RotationSpeed = airRotationSpeed;
                
                // Optional: Additional randomization - this is fine-tuned for this enemy type
                enemy.SetProperties(airChaseSpeed, airRotationSpeed, 0.5f);
            }
        }

        foreach (var enemy in groundHopEnemies)
        {
            if (enemy.gameObject.activeSelf)
            {
                enemy.MoveSpeed = groundHopMoveSpeed;
                enemy.HopDuration = groundHopDuration;
                
                // Use the built-in method to set both properties (and others)
                enemy.SetProperties(groundHopMoveSpeed, groundHopDuration, 0.5f, 1.5f);
            }
        }

        foreach (var enemy in groundFixedEnemies)
        {
            if (enemy.gameObject.activeSelf)
            {
                enemy.MoveSpeed = groundFixedMoveSpeed;
                enemy.RandomTeleportMaxHeight = groundFixedRandomTeleportMaxHeight;
                enemy.RandomTeleportMinHeight = groundFixedRandomTeleportMinHeight;
                
                // Also set UseRandomTeleport to true if you want that behavior
                enemy.UseRandomTeleport = true;
                
                // Use the built-in method to set all properties at once
                enemy.SetProperties(
                    groundFixedMoveSpeed,
                    4f, // maxHeightDifference
                    -6f, // minHeightDifference
                    1.0f, // teleportCooldown
                    true, // useRandomTeleport
                    groundFixedRandomTeleportMaxHeight,
                    groundFixedRandomTeleportMinHeight
                );
            }
        }

        if (debugMode)
            Debug.Log("Level 6 bounty: All enemies set to maximum difficulty!");
    }

    private void DisableAllEnemies()
    {
        foreach (var enemy in airEnemies)
            enemy.gameObject.SetActive(false);

        foreach (var enemy in groundFixedEnemies)
            enemy.gameObject.SetActive(false);

        foreach (var enemy in groundHopEnemies)
            enemy.gameObject.SetActive(false);
    }

    private void ConfigureEnemyDamage(GameObject enemyObject, bool enableDamage)
    {
        EnemyDamageDealer damageDealer = enemyObject.GetComponent<EnemyDamageDealer>();
        
        // If the enemy doesn't have a damage dealer component, add one
        if (damageDealer == null && enableDamage)
        {
            // Only add to ground enemies, not air enemies
            if (enemyObject.GetComponent<AirEnemyChase>() == null)
            {
                damageDealer = enemyObject.AddComponent<EnemyDamageDealer>();
                
                if (debugMode)
                    Debug.Log($"Added damage dealer to {enemyObject.name}");
            }
        }
        
        // Configure the damage dealer
        if (damageDealer != null)
        {
            damageDealer.SetDamageEnabled(enableDamage);
        }
    }

    private void EnableEnemy(MonoBehaviour enemy)
    {
        if (!enemy.gameObject.activeSelf)
        {
            enemy.gameObject.SetActive(true);
            
            // Configure damage for this enemy type
            bool enableDamage = !(enemy is AirEnemyChase); // Only enable damage for ground enemies
            ConfigureEnemyDamage(enemy.gameObject, enableDamage);
            
            if (debugMode)
                Debug.Log($"Enabled enemy: {enemy.gameObject.name}");
        }
    }

    // Public method to manually set bounty level (for testing or events)
    public void SetBountyLevel(int level)
    {
        level = Mathf.Clamp(level, 1, 6);
        
        if (level != currentBountyLevel)
        {
            currentBountyLevel = level;
            LogBountyChange(currentBountyLevel, treasureManager.GetCollectedTreasures());
            
            // Reset to level 1 first, then build up to the desired level
            ConfigureEnemiesForLevel1();
            
            // Apply each level sequentially to ensure proper enemy activation
            for (int i = 2; i <= level; i++)
            {
                currentBountyLevel = i;
                UpdateEnemiesForCurrentLevel();
            }
        }
    }

    // For debugging/testing in the inspector
    [ContextMenu("Test Level 1")]
    private void TestLevel1() => SetBountyLevel(1);

    [ContextMenu("Test Level 2")]
    private void TestLevel2() => SetBountyLevel(2);

    [ContextMenu("Test Level 3")]
    private void TestLevel3() => SetBountyLevel(3);

    [ContextMenu("Test Level 4")]
    private void TestLevel4() => SetBountyLevel(4);

    [ContextMenu("Test Level 5")]
    private void TestLevel5() => SetBountyLevel(5);

    [ContextMenu("Test Level 6")]
    private void TestLevel6() => SetBountyLevel(6);
}