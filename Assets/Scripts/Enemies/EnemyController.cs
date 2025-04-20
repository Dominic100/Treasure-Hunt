using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour {
    [System.Serializable]
    public class AirChaseSettings {
        [Header("Prefab and Spawning")]
        public GameObject enemyPrefab;
        public Transform[] spawnLocations;
        public int initialCount = 2;
        public int maxCount = 5;

        [Header("Movement Properties")]
        public float baseChaseSpeed = 2.5f;
        public float baseRotationSpeed = 2.0f;
        public float baseRandomization = 1.0f; // This affects swayMagnitude and randomDirectionChangeMagnitude
    }

    [System.Serializable]
    public class GroundChaseSettings {
        [Header("Prefab and Spawning")]
        public GameObject enemyPrefab;
        public Transform[] spawnLocations;
        public int initialCount = 2;
        public int maxCount = 5;

        [Header("Movement Properties")]
        public float baseMoveSpeed = 5.0f;

        [Header("Teleport Properties")]
        public float baseMaxHeightDifference = 4.0f;
        public float baseMinHeightDifference = -6.0f;
        public float baseTeleportCooldown = 1.5f;
        public bool useRandomTeleport = false;
        public float baseRandomTeleportMaxHeight = 7.0f;
        public float baseRandomTeleportMinHeight = -7.0f;
    }

    [System.Serializable]
    public class GroundHopSettings {
        [Header("Prefab and Spawning")]
        public GameObject enemyPrefab;
        public Transform[] spawnLocations;
        public int initialCount = 2;
        public int maxCount = 5;

        [Header("Movement Properties")]
        public float baseMoveSpeed = 3.0f;
        public float baseHopDuration = 0.75f;
        public float baseMinTimeBetweenHops = 2.0f;
        public float baseMaxTimeBetweenHops = 5.0f;
    }

    [Header("Enemy Settings")]
    [SerializeField] private AirChaseSettings airChaseSettings;
    [SerializeField] private GroundChaseSettings groundChaseSettings;
    [SerializeField] private GroundHopSettings groundHopSettings;

    [Header("Spawn Settings")]
    [SerializeField] private float initialSpawnDelay = 1.0f;
    [SerializeField] private float respawnInterval = 3.0f;
    [SerializeField] private int maxTotalEnemies = 15;

    [Header("Difficulty Scaling")]
    [SerializeField] private float speedScaleFactor = 0.2f; // % increase per difficulty level
    [SerializeField] private float countScaleFactor = 1f; // Additional enemies per difficulty level
    [SerializeField] private float randomizationReductionFactor = 0.15f; // % decrease per difficulty level

    [Header("References")]
    [SerializeField] private Transform player;

    // Internal tracking
    private List<GameObject> activeAirChaseEnemies = new List<GameObject>();
    private List<GameObject> activeGroundChaseEnemies = new List<GameObject>();
    private List<GameObject> activeGroundHopEnemies = new List<GameObject>();
    private int currentDifficultyLevel = 0;
    private bool isInitialized = false;

    // Properties for modifying enemy counts
    public int CurrentAirChaseCount { get => activeAirChaseEnemies.Count; }
    public int CurrentGroundChaseCount { get => activeGroundChaseEnemies.Count; }
    public int CurrentGroundHopCount { get => activeGroundHopEnemies.Count; }
    public int TotalEnemyCount { get => CurrentAirChaseCount + CurrentGroundChaseCount + CurrentGroundHopCount; }

    void Start() {
        if (player == null) {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) {
                Debug.LogError("Player not found! Using null reference will cause errors.");
            }
        }

        // Validate settings
        ValidateSettings();

        // Start spawning after a delay
        StartCoroutine(InitialSpawnCoroutine());
    }

    private void ValidateSettings() {
        bool hasErrors = false;

        // Check Air Chase settings
        if (airChaseSettings.enemyPrefab == null) {
            Debug.LogError("Air Chase enemy prefab not assigned!");
            hasErrors = true;
        }
        if (airChaseSettings.spawnLocations == null || airChaseSettings.spawnLocations.Length == 0) {
            Debug.LogError("No spawn locations set for Air Chase enemies!");
            hasErrors = true;
        }

        // Check Ground Chase settings
        if (groundChaseSettings.enemyPrefab == null) {
            Debug.LogError("Ground Chase enemy prefab not assigned!");
            hasErrors = true;
        }
        if (groundChaseSettings.spawnLocations == null || groundChaseSettings.spawnLocations.Length == 0) {
            Debug.LogError("No spawn locations set for Ground Chase enemies!");
            hasErrors = true;
        }

        // Check Ground Hop settings
        if (groundHopSettings.enemyPrefab == null) {
            Debug.LogError("Ground Hop enemy prefab not assigned!");
            hasErrors = true;
        }
        if (groundHopSettings.spawnLocations == null || groundHopSettings.spawnLocations.Length == 0) {
            Debug.LogError("No spawn locations set for Ground Hop enemies!");
            hasErrors = true;
        }

        if (hasErrors) {
            Debug.LogWarning("EnemyController has configuration errors. Some enemies may not spawn correctly.");
        }
    }

    private IEnumerator InitialSpawnCoroutine() {
        yield return new WaitForSeconds(initialSpawnDelay);

        // Spawn initial enemies
        SpawnInitialEnemies();

        // Start respawning coroutine
        StartCoroutine(RespawnEnemiesCoroutine());

        isInitialized = true;
    }

    private void SpawnInitialEnemies() {
        // Spawn Air Chase enemies
        for (int i = 0; i < airChaseSettings.initialCount; i++) {
            SpawnAirChaseEnemy();
        }

        // Spawn Ground Chase enemies
        for (int i = 0; i < groundChaseSettings.initialCount; i++) {
            SpawnGroundChaseEnemy();
        }

        // Spawn Ground Hop enemies
        for (int i = 0; i < groundHopSettings.initialCount; i++) {
            SpawnGroundHopEnemy();
        }

        Debug.Log($"Initial enemies spawned: {TotalEnemyCount} total");
    }

    private IEnumerator RespawnEnemiesCoroutine() {
        while (true) {
            yield return new WaitForSeconds(respawnInterval);

            // Check if we need to spawn more enemies
            TryRespawnEnemies();
        }
    }

    private void TryRespawnEnemies() {
        if (TotalEnemyCount >= maxTotalEnemies) {
            return; // We've reached the maximum total enemies
        }

        // Calculate current max counts based on difficulty
        int maxAirChase = Mathf.FloorToInt(airChaseSettings.maxCount + (currentDifficultyLevel * countScaleFactor));
        int maxGroundChase = Mathf.FloorToInt(groundChaseSettings.maxCount + (currentDifficultyLevel * countScaleFactor));
        int maxGroundHop = Mathf.FloorToInt(groundHopSettings.maxCount + (currentDifficultyLevel * countScaleFactor));

        // Cap based on total max enemies
        int remainingSlots = maxTotalEnemies - TotalEnemyCount;
        if (remainingSlots <= 0) return;

        // Try to spawn each type if below max
        bool spawned = false;

        if (CurrentAirChaseCount < maxAirChase) {
            SpawnAirChaseEnemy();
            spawned = true;
        }

        if (CurrentGroundChaseCount < maxGroundChase && TotalEnemyCount < maxTotalEnemies) {
            SpawnGroundChaseEnemy();
            spawned = true;
        }

        if (CurrentGroundHopCount < maxGroundHop && TotalEnemyCount < maxTotalEnemies) {
            SpawnGroundHopEnemy();
            spawned = true;
        }

        if (spawned) {
            Debug.Log($"Respawned enemies. Current counts - Air: {CurrentAirChaseCount}, " +
                      $"Ground Chase: {CurrentGroundChaseCount}, Ground Hop: {CurrentGroundHopCount}");
        }
    }

    private GameObject SpawnAirChaseEnemy() {
        if (airChaseSettings.enemyPrefab == null || airChaseSettings.spawnLocations.Length == 0) {
            Debug.LogError("Cannot spawn Air Chase enemy - missing prefab or spawn locations");
            return null;
        }

        // Choose a random spawn location
        Transform spawnPoint = airChaseSettings.spawnLocations[Random.Range(0, airChaseSettings.spawnLocations.Length)];

        // Instantiate the enemy
        GameObject enemy = Instantiate(airChaseSettings.enemyPrefab, spawnPoint.position, Quaternion.identity);

        // Get and configure the controller
        AirEnemyChase controller = enemy.GetComponent<AirEnemyChase>();
        if (controller != null) {
            // Calculate adjusted values based on difficulty
            float speedMultiplier = 1.0f + (currentDifficultyLevel * speedScaleFactor);
            float randomizationMultiplier = 1.0f - (currentDifficultyLevel * randomizationReductionFactor);
            randomizationMultiplier = Mathf.Max(0.2f, randomizationMultiplier); // Ensure it doesn't go below 0.2

            // Apply properties
            SetAirChaseProperties(controller, speedMultiplier, randomizationMultiplier);

            // Set player reference
            SetPlayerReference(controller);

            // Add to tracking list
            activeAirChaseEnemies.Add(enemy);

            // Setup lifecycle management
            EnemyLifecycle lifecycle = enemy.AddComponent<EnemyLifecycle>();
            lifecycle.Initialize(this, EnemyType.AirChase);

            return enemy;
        }
        else {
            Debug.LogError("Spawned Air Chase enemy doesn't have AirEnemyChase component!");
            Destroy(enemy);
            return null;
        }
    }

    private GameObject SpawnGroundChaseEnemy() {
        if (groundChaseSettings.enemyPrefab == null || groundChaseSettings.spawnLocations.Length == 0) {
            Debug.LogError("Cannot spawn Ground Chase enemy - missing prefab or spawn locations");
            return null;
        }

        // Choose a random spawn location
        Transform spawnPoint = groundChaseSettings.spawnLocations[Random.Range(0, groundChaseSettings.spawnLocations.Length)];

        // Instantiate the enemy
        GameObject enemy = Instantiate(groundChaseSettings.enemyPrefab, spawnPoint.position, Quaternion.identity);

        // Get and configure the controller
        GroundEnemyFixedChase controller = enemy.GetComponent<GroundEnemyFixedChase>();
        if (controller != null) {
            // Calculate adjusted values based on difficulty
            float speedMultiplier = 1.0f + (currentDifficultyLevel * speedScaleFactor);

            // Apply properties
            SetGroundChaseProperties(controller, speedMultiplier);

            SetPlayerReference(controller);

            // Add to tracking list
            activeGroundChaseEnemies.Add(enemy);

            // Setup lifecycle management
            EnemyLifecycle lifecycle = enemy.AddComponent<EnemyLifecycle>();
            lifecycle.Initialize(this, EnemyType.GroundChase);

            return enemy;
        }
        else {
            Debug.LogError("Spawned Ground Chase enemy doesn't have GroundEnemyFixedChase component!");
            Destroy(enemy);
            return null;
        }
    }

    private GameObject SpawnGroundHopEnemy() {
        if (groundHopSettings.enemyPrefab == null || groundHopSettings.spawnLocations.Length == 0) {
            Debug.LogError("Cannot spawn Ground Hop enemy - missing prefab or spawn locations");
            return null;
        }

        // Choose a random spawn location
        Transform spawnPoint = groundHopSettings.spawnLocations[Random.Range(0, groundHopSettings.spawnLocations.Length)];

        // Instantiate the enemy
        GameObject enemy = Instantiate(groundHopSettings.enemyPrefab, spawnPoint.position, Quaternion.identity);

        // Get and configure the controller
        GroundHopEnemyChase controller = enemy.GetComponent<GroundHopEnemyChase>();
        if (controller != null) {
            // Calculate adjusted values based on difficulty
            float speedMultiplier = 1.0f + (currentDifficultyLevel * speedScaleFactor);
            float hopTimeMultiplier = 1.0f - (currentDifficultyLevel * 0.1f); // Reduce hop time with difficulty
            hopTimeMultiplier = Mathf.Max(0.5f, hopTimeMultiplier); // Don't go below 50% of base time

            // Apply properties
            SetGroundHopProperties(controller, speedMultiplier, hopTimeMultiplier);

            // Set player reference
            SetPlayerReference(controller);

            // Add to tracking list
            activeGroundHopEnemies.Add(enemy);

            // Setup lifecycle management
            EnemyLifecycle lifecycle = enemy.AddComponent<EnemyLifecycle>();
            lifecycle.Initialize(this, EnemyType.GroundHop);

            return enemy;
        }
        else {
            Debug.LogError("Spawned Ground Hop enemy doesn't have GroundHopEnemyChase component!");
            Destroy(enemy);
            return null;
        }
    }

    // Use reflection to set player reference, since we don't know if the method exists yet
    private void SetPlayerReference(MonoBehaviour controller) {
        // Always try to use the SetPlayer method first
        var setPlayerMethod = controller.GetType().GetMethod("SetPlayer");
        if (setPlayerMethod != null) {
            setPlayerMethod.Invoke(controller, new object[] { player });
            return;
        }

        // If SetPlayer method doesn't exist, try to access the field directly as fallback
        var playerField = controller.GetType().GetField("player", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.Public |
                                                     System.Reflection.BindingFlags.NonPublic);
        if (playerField != null) {
            playerField.SetValue(controller, player);
            return;
        }

        Debug.LogWarning($"Could not set player reference on {controller.GetType().Name} - no compatible field or method found.");
    }

    private void SetAirChaseProperties(AirEnemyChase controller, float speedMultiplier, float randomizationMultiplier) {
        // Set properties directly (we'll add the required methods to AirEnemyChase later)
        controller.SetProperties(
            airChaseSettings.baseChaseSpeed * speedMultiplier,
            airChaseSettings.baseRotationSpeed * speedMultiplier,
            airChaseSettings.baseRandomization * randomizationMultiplier
        );
    }

    private void SetGroundChaseProperties(GroundEnemyFixedChase controller, float speedMultiplier) {
        // Teleport settings scale with difficulty
        float teleportCooldownMultiplier = 1.0f - (currentDifficultyLevel * 0.1f); // Reduce cooldown with difficulty
        teleportCooldownMultiplier = Mathf.Max(0.5f, teleportCooldownMultiplier); // Don't go below 50% of base cooldown

        controller.SetProperties(
            groundChaseSettings.baseMoveSpeed * speedMultiplier,
            groundChaseSettings.baseMaxHeightDifference,
            groundChaseSettings.baseMinHeightDifference,
            groundChaseSettings.baseTeleportCooldown * teleportCooldownMultiplier,
            groundChaseSettings.useRandomTeleport,
            groundChaseSettings.baseRandomTeleportMaxHeight,
            groundChaseSettings.baseRandomTeleportMinHeight
        );
    }

    private void SetGroundHopProperties(GroundHopEnemyChase controller, float speedMultiplier, float hopTimeMultiplier) {
        controller.SetProperties(
            groundHopSettings.baseMoveSpeed * speedMultiplier,
            groundHopSettings.baseHopDuration,
            groundHopSettings.baseMinTimeBetweenHops * hopTimeMultiplier,
            groundHopSettings.baseMaxTimeBetweenHops * hopTimeMultiplier
        );
    }

    // Called by the BountySystem to update difficulty
    public void SetDifficultyLevel(int level) {
        currentDifficultyLevel = Mathf.Max(0, level);

        // Update all active enemies
        UpdateAllEnemyProperties();

        // Log
        Debug.Log($"Enemy difficulty set to level {currentDifficultyLevel}");
    }

    private void UpdateAllEnemyProperties() {
        // Calculate multipliers
        float speedMultiplier = 1.0f + (currentDifficultyLevel * speedScaleFactor);
        float randomizationMultiplier = 1.0f - (currentDifficultyLevel * randomizationReductionFactor);
        randomizationMultiplier = Mathf.Max(0.2f, randomizationMultiplier);
        float hopTimeMultiplier = 1.0f - (currentDifficultyLevel * 0.1f);
        hopTimeMultiplier = Mathf.Max(0.5f, hopTimeMultiplier);
        float teleportCooldownMultiplier = 1.0f - (currentDifficultyLevel * 0.1f);
        teleportCooldownMultiplier = Mathf.Max(0.5f, teleportCooldownMultiplier);

        // Update Air Chase enemies
        foreach (GameObject enemy in activeAirChaseEnemies) {
            if (enemy != null) {
                AirEnemyChase controller = enemy.GetComponent<AirEnemyChase>();
                if (controller != null) {
                    SetAirChaseProperties(controller, speedMultiplier, randomizationMultiplier);
                }
            }
        }

        // Update Ground Chase enemies
        foreach (GameObject enemy in activeGroundChaseEnemies) {
            if (enemy != null) {
                GroundEnemyFixedChase controller = enemy.GetComponent<GroundEnemyFixedChase>();
                if (controller != null) {
                    SetGroundChaseProperties(controller, speedMultiplier);
                }
            }
        }

        // Update Ground Hop enemies
        foreach (GameObject enemy in activeGroundHopEnemies) {
            if (enemy != null) {
                GroundHopEnemyChase controller = enemy.GetComponent<GroundHopEnemyChase>();
                if (controller != null) {
                    SetGroundHopProperties(controller, speedMultiplier, hopTimeMultiplier);
                }
            }
        }
    }

    // Called by EnemyLifecycle when an enemy is destroyed
    public void RemoveEnemy(GameObject enemy, EnemyType type) {
        switch (type) {
            case EnemyType.AirChase:
                if (activeAirChaseEnemies.Contains(enemy)) {
                    activeAirChaseEnemies.Remove(enemy);
                }
                break;

            case EnemyType.GroundChase:
                if (activeGroundChaseEnemies.Contains(enemy)) {
                    activeGroundChaseEnemies.Remove(enemy);
                }
                break;

            case EnemyType.GroundHop:
                if (activeGroundHopEnemies.Contains(enemy)) {
                    activeGroundHopEnemies.Remove(enemy);
                }
                break;
        }
    }

    // Helper enum for enemy types
    public enum EnemyType {
        AirChase,
        GroundChase,
        GroundHop
    }
}

// Helper component to manage enemy lifecycle
public class EnemyLifecycle : MonoBehaviour {
    private EnemyController controller;
    private EnemyController.EnemyType enemyType;

    public void Initialize(EnemyController controller, EnemyController.EnemyType type) {
        this.controller = controller;
        this.enemyType = type;
    }

    private void OnDestroy() {
        if (controller != null) {
            controller.RemoveEnemy(gameObject, enemyType);
        }
    }
}
