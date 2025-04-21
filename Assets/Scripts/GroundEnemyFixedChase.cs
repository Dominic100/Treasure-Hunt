using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GroundEnemyFixedChase : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Tilemap platformTilemap;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private PolygonCollider2D chaseArea; // Area where enemy will chase player
    [SerializeField] private GameObject spawnPoint;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Platform Settings")]
    [SerializeField] private float maxHeightDifference = 4f; // Maximum height for direct teleports
    [SerializeField] private float minHeightDifference = -6f; // Minimum height for direct teleports
    [SerializeField] private float teleportCooldown = 1.5f; // Time between teleportations
    [SerializeField] private Vector2 triggerBoxSize = new Vector2(1.5f, 1.5f); // Size of platform corner trigger boxes

    [Header("Random Teleport Settings")]
    [SerializeField] private bool useRandomTeleport = false; // Toggle for random teleport behavior
    [SerializeField] private float randomTeleportMaxHeight = 7f; // Max height above player platform
    [SerializeField] private float randomTeleportMinHeight = -7f; // Min height below player platform

    [Header("Effects")]
    [SerializeField] private GameObject teleportEffect;
    [SerializeField] private AudioClip teleportSound;
    [SerializeField] private Color teleportColor = Color.blue;
    [SerializeField] private float flashDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false; // Enable debug logs

    // Private state variables
    private List<Platform> platforms = new List<Platform>();
    private Dictionary<int, List<GameObject>> platformTriggers = new Dictionary<int, List<GameObject>>();
    private Rigidbody2D rb;
    private bool isGrounded = false;
    private int currentPlatformIndex = -1;
    private int targetPlatformIndex = -1;
    private bool canTeleport = true;
    private float teleportTimer = 0f;
    private bool isPlayerInChaseArea = false;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private AudioSource audioSource;
    private bool isInitialized = false;

    // Track current target platform for triggers
    private Platform targetPlatform = null;
    private Platform intermediatePlatform = null;

    private void Start() {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        if (spriteRenderer != null) {
            originalColor = spriteRenderer.color;
        }

        if (audioSource == null && teleportSound != null) {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Set spawn position if provided
        if (spawnPoint != null) {
            transform.position = spawnPoint.transform.position;
        }

        // Make sure the chase area is a trigger
        if (chaseArea != null) {
            chaseArea.isTrigger = true;
        }

        Debug.Log("Just before initialization, checking references...");
        // Initialize the system
        InitializeNow();
    }

    private void InitializeNow() {
        // Scan the platforms in the chase area
        ScanForPlatforms();

        // Create trigger boxes for each platform corner
        CreatePlatformTriggers();

        // Set the enemy on a valid platform if needed
        EnsureEnemyOnPlatform();

        Debug.LogError($"DIRECT INIT: Enemy initialized with {platforms.Count} platforms in chase area");
        Debug.LogError($"STARTUP DIAGNOSTIC: Enemy={gameObject.name}, " +
                $"Platforms detected={platforms.Count}, " +
                $"Triggers created={platformTriggers.Count}, " +
                $"Player reference={player != null}, " +
                $"CurrentPlatform={currentPlatformIndex}, " +
                $"Tilemap={platformTilemap != null}, " +
                $"ChaseArea={chaseArea != null}, " +
                $"rb={rb != null}");
                
        isInitialized = true;
    }

    private void CreatePlatformTriggers() {
        // Clear any existing triggers
        foreach (var triggerList in platformTriggers.Values) {
            foreach (var trigger in triggerList) {
                Destroy(trigger);
            }
        }
        platformTriggers.Clear();

        // Create new triggers for each platform
        foreach (Platform platform in platforms) {
            platformTriggers[platform.index] = new List<GameObject>();

            // Create left corner trigger
            Vector3 leftPos = platformTilemap.GetCellCenterWorld(platform.leftPoint);
            leftPos.y += triggerBoxSize.y / 1.5f;

            GameObject leftTrigger = CreateTriggerBox(leftPos, platform, true);
            platformTriggers[platform.index].Add(leftTrigger);

            // Create right corner trigger
            Vector3 rightPos = platformTilemap.GetCellCenterWorld(platform.rightPoint);
            rightPos.y += triggerBoxSize.y / 1.5f;

            GameObject rightTrigger = CreateTriggerBox(rightPos, platform, false);
            platformTriggers[platform.index].Add(rightTrigger);
        }

        Debug.Log($"Created trigger boxes for {platforms.Count} platforms");
    }

    private GameObject CreateTriggerBox(Vector3 position, Platform platform, bool isLeftCorner) {
        GameObject triggerObject = new GameObject($"PlatformTrigger_{platform.index}_{(isLeftCorner ? "Left" : "Right")}");
        triggerObject.transform.position = position;
        triggerObject.transform.parent = transform.parent; // Same parent as enemy

        // Make sure the trigger is on a layer that can interact with the enemy
        triggerObject.layer = gameObject.layer;

        // Add collider
        BoxCollider2D collider = triggerObject.AddComponent<BoxCollider2D>();
        collider.size = triggerBoxSize;
        collider.isTrigger = true;

        // Add component to handle the trigger
        PlatformTrigger trigger = triggerObject.AddComponent<PlatformTrigger>();
        trigger.Setup(this, platform.index, isLeftCorner);

        Debug.Log($"Created trigger box at {position} for platform {platform.index} ({(isLeftCorner ? "left" : "right")})");

        return triggerObject;
    }

    public float MoveSpeed {
        get { return moveSpeed; }
        set { moveSpeed = Mathf.Max(1f, value); } // Ensure we don't set it below 1
    }
    
    public bool UseRandomTeleport {
        get { return useRandomTeleport; }
        set { useRandomTeleport = value; }
    }
    
    public float RandomTeleportMaxHeight {
        get { return randomTeleportMaxHeight; }
        set { randomTeleportMaxHeight = Mathf.Max(1f, value); } // Ensure positive value
    }
    
    public float RandomTeleportMinHeight {
        get { return randomTeleportMinHeight; }
        set { randomTeleportMinHeight = Mathf.Min(-1f, value); } // Ensure negative value
    }

    private void EnsureEnemyOnPlatform() {
        int platformIndex = GetPlatformAtPosition(transform.position);
        if (platformIndex == -1) {
            // Find the closest platform
            platformIndex = FindClosestPlatformIndex(transform.position);

            if (platformIndex >= 0) {
                // Place the enemy on this platform
                Platform platform = platforms[platformIndex];
                Vector3 platformCenter = GetPlatformCenter(platform);
                platformCenter.y += 1.0f; // Position above the platform

                transform.position = platformCenter;
                Debug.Log($"Placed enemy on platform {platformIndex}");
            }
        }

        currentPlatformIndex = platformIndex;
    }

    private void Update() {
        if (!isInitialized) return;

        // Update teleport cooldown
        if (!canTeleport) {
            teleportTimer -= Time.deltaTime;
            if (teleportTimer <= 0) {
                canTeleport = true;
                Debug.Log("Teleport cooldown ended - can teleport again");
            }
        }

        // Check if player is in chase area
        bool wasPlayerInChaseArea = isPlayerInChaseArea;
        isPlayerInChaseArea = IsPointInChaseArea(player.position);

        // Player just entered chase area
        if (!wasPlayerInChaseArea && isPlayerInChaseArea) {
            Debug.Log("Player entered chase area - starting chase");
        }

        // Don't chase if player is outside chase area
        if (!isPlayerInChaseArea) {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Check if the enemy is grounded
        CheckGrounded();

        // Update current platform
        int previousPlatform = currentPlatformIndex;
        UpdateCurrentPlatform();

        // If we changed platforms unexpectedly (e.g., fell), reconsider our targets
        if (previousPlatform != currentPlatformIndex && previousPlatform != -1) {
            Debug.Log($"Enemy changed platforms from {previousPlatform} to {currentPlatformIndex} - reconsidering targets");
            int playerPlatform = GetPlatformAtPosition(player.position);
            if (playerPlatform != -1 && playerPlatform != currentPlatformIndex) {
                if (useRandomTeleport) {
                    // No specific target needed for random teleport mode
                    targetPlatform = null;
                    intermediatePlatform = null;
                }
                else {
                    UpdateTargetForPlayerPlatform(playerPlatform);
                }
            }
        }

        // Update the target platform (player's platform) - only for non-random mode
        if (!useRandomTeleport) {
            UpdateTargetPlatform();
        }

        // Handle movement and teleportation
        ChasePlayer();
        
        // Safety check - if enemy has wandered too far from spawn, teleport back
        if (spawnPoint != null) {
            float distanceFromSpawn = Vector3.Distance(transform.position, spawnPoint.transform.position);
            if (distanceFromSpawn > 500f) { // Using a large threshold
                // Return to spawn point
                Debug.LogWarning($"Enemy too far from spawn ({distanceFromSpawn} units) - returning to spawn");
                ReturnToSpawn();
            }
        }
    }
    
    private void ReturnToSpawn() {
        if (spawnPoint == null) return;
        
        // Flash effect
        if (spriteRenderer != null) {
            StartCoroutine(FlashColor());
        }
        
        // Reset position
        transform.position = spawnPoint.transform.position;
        
        // Reset velocity
        if (rb != null) {
            rb.linearVelocity = Vector2.zero;
        }
        
        // Reset platform info
        EnsureEnemyOnPlatform();
    }

    private void UpdateCurrentPlatform() {
        int platformIndex = GetPlatformAtPosition(transform.position);
        if (platformIndex != currentPlatformIndex) {
            currentPlatformIndex = platformIndex;
            Debug.Log($"Enemy now on platform {currentPlatformIndex}");
        }
    }

    private void UpdateTargetPlatform() {
        if (!isPlayerInChaseArea) {
            targetPlatformIndex = -1;
            targetPlatform = null;
            return;
        }

        int playerPlatform = GetPlatformAtPosition(player.position);
        if (playerPlatform != targetPlatformIndex) {
            targetPlatformIndex = playerPlatform;
            targetPlatform = playerPlatform >= 0 && playerPlatform < platforms.Count ?
                platforms[playerPlatform] : null;
            Debug.Log($"New target platform: {targetPlatformIndex}");
        }
    }

    private void ChasePlayer() {
        // If not on a valid platform, do nothing (don't check target platform for random mode)
        if (currentPlatformIndex == -1 || (!useRandomTeleport && targetPlatformIndex == -1)) {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Get the player's platform for reference
        int playerPlatformIndex = GetPlatformAtPosition(player.position);

        // If on the same platform as player, move directly towards player
        if (currentPlatformIndex == playerPlatformIndex) {
            float moveDirection = Mathf.Sign(player.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
            return;
        }

        Platform currentPlatform = platforms[currentPlatformIndex];

        // Handle random teleportation if enabled
        if (useRandomTeleport) {
            // Just move to nearest edge and prepare for random teleport
            MoveToNearestPlatformEdge(currentPlatform);

            // We don't set specific target platforms here as the random selection
            // will happen when the trigger is hit
            this.targetPlatform = null;
            this.intermediatePlatform = null;
            return;
        }

        // Standard target-based teleport logic if random teleport is disabled
        Platform targetPlatform = platforms[targetPlatformIndex];

        float currentHeight = GetPlatformHeight(currentPlatform);
        float targetHeight = GetPlatformHeight(targetPlatform);
        float heightDifference = targetHeight - currentHeight;

        // If player platform is within height threshold, teleport directly
        if (heightDifference <= maxHeightDifference && heightDifference >= minHeightDifference) {
            // Just move to nearest edge of current platform
            MoveToNearestPlatformEdge(currentPlatform);
            this.targetPlatform = targetPlatform;
            this.intermediatePlatform = null;
        }
        // Otherwise, find an intermediate platform
        else {
            // Find the closest platform that's within height range of current platform
            // and closer to target platform in height
            Platform intermediatePlatform = FindIntermediatePlatform(currentPlatform, targetPlatform);

            if (intermediatePlatform != null) {
                // Move to the nearest edge and teleport to the intermediate platform
                MoveToNearestPlatformEdge(currentPlatform);
                this.targetPlatform = null;
                this.intermediatePlatform = intermediatePlatform;
            }
            else {
                // No valid intermediate platform found, just move randomly
                float randomDir = Mathf.Sign(Random.value - 0.5f);
                rb.linearVelocity = new Vector2(randomDir * moveSpeed, rb.linearVelocity.y);
                this.targetPlatform = null;
                this.intermediatePlatform = null;
            }
        }
    }

    private void MoveToNearestPlatformEdge(Platform platform) {
        if (platform == null) return;

        // Get world positions of platform edges
        Vector2 leftEdge = platformTilemap.GetCellCenterWorld(platform.leftPoint);
        Vector2 rightEdge = platformTilemap.GetCellCenterWorld(platform.rightPoint);

        // Determine which edge is closer
        float distToLeft = Vector2.Distance(transform.position, leftEdge);
        float distToRight = Vector2.Distance(transform.position, rightEdge);

        // Move towards the closer edge
        float moveDir;
        if (distToLeft < distToRight) {
            moveDir = Mathf.Sign(leftEdge.x - transform.position.x);
        }
        else {
            moveDir = Mathf.Sign(rightEdge.x - transform.position.x);
        }

        rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
    }

    // This method is called by the PlatformTrigger component
    public void OnPlatformTriggerEnter(int platformIndex, bool isLeftCorner) {
        // Debug the current state thoroughly
        Debug.Log($"OnPlatformTriggerEnter called: Platform {platformIndex}, {(isLeftCorner ? "Left" : "Right")} corner");
        Debug.Log($"Can teleport: {canTeleport}, Is grounded: {isGrounded}");
        Debug.Log($"Current target platform: {(targetPlatform != null ? targetPlatform.index.ToString() : "none")}");
        Debug.Log($"Current intermediate platform: {(intermediatePlatform != null ? intermediatePlatform.index.ToString() : "none")}");

        // Skip if on cooldown
        if (!canTeleport) {
            Debug.Log($"Teleport on cooldown ({teleportTimer:F2}s remaining) - ignoring trigger");
            return;
        }

        // Check if we should use random teleportation
        if (useRandomTeleport) {
            // Find a random platform within the height range of the player
            Platform randomPlatform = FindRandomPlatformNearPlayer();

            if (randomPlatform != null) {
                Debug.Log($"Random teleport to platform {randomPlatform.index}");
                TeleportToPlatform(randomPlatform);
            }
            else {
                Debug.Log("No valid random platform found for teleportation");
            }
            return;
        }

        // Skip if this is the platform we're currently on and we're not at an endpoint
        if (platformIndex == currentPlatformIndex) {
            // If player is on another platform, recalculate path immediately
            int playerPlatform = GetPlatformAtPosition(player.position);
            if (playerPlatform != currentPlatformIndex && playerPlatform != -1) {
                Debug.Log("On current platform trigger, but player is elsewhere - recalculating path");

                // Force recalculation of path based on player's new platform
                UpdateTargetForPlayerPlatform(playerPlatform);

                // Now check if we should teleport to the new target
                if (targetPlatform != null) {
                    Debug.Log($"Teleporting to newly calculated target platform {targetPlatform.index}");
                    TeleportToPlatform(targetPlatform);
                }
                else if (intermediatePlatform != null) {
                    Debug.Log($"Teleporting to newly calculated intermediate platform {intermediatePlatform.index}");
                    TeleportToPlatform(intermediatePlatform);
                }
            }
            else {
                Debug.Log("Ignoring trigger on current platform since player is also here");
            }
            return;
        }

        // If we have a target platform set, teleport there
        if (targetPlatform != null) {
            Debug.Log($"Teleporting to target platform {targetPlatform.index} from trigger");
            TeleportToPlatform(targetPlatform);
        }
        // Otherwise, if we have an intermediate platform set, teleport there
        else if (intermediatePlatform != null) {
            Debug.Log($"Teleporting to intermediate platform {intermediatePlatform.index} from trigger");
            TeleportToPlatform(intermediatePlatform);
        }
        else {
            // No explicit target - try to calculate one based on player position
            int playerPlatform = GetPlatformAtPosition(player.position);
            if (playerPlatform != -1 && playerPlatform != currentPlatformIndex) {
                Debug.Log($"No target set, but player is on platform {playerPlatform} - calculating route");
                UpdateTargetForPlayerPlatform(playerPlatform);

                if (targetPlatform != null) {
                    TeleportToPlatform(targetPlatform);
                }
                else if (intermediatePlatform != null) {
                    TeleportToPlatform(intermediatePlatform);
                }
                else {
                    Debug.Log("Couldn't calculate path to player platform");
                }
            }
            else {
                Debug.Log("No target or intermediate platform set - nothing to teleport to");
            }
        }
    }

    // Method to find a random platform within height range of player
    private Platform FindRandomPlatformNearPlayer() {
        int playerPlatformIndex = GetPlatformAtPosition(player.position);
        if (playerPlatformIndex == -1 || playerPlatformIndex >= platforms.Count) {
            return null; // Player not on a valid platform
        }

        Platform playerPlatform = platforms[playerPlatformIndex];
        float playerHeight = GetPlatformHeight(playerPlatform);

        // Get all platforms within the height range
        List<Platform> validPlatforms = new List<Platform>();

        foreach (Platform platform in platforms) {
            float platformHeight = GetPlatformHeight(platform);
            float heightDifference = platformHeight - playerHeight;

            // Check if this platform is within our height range from player
            if (heightDifference <= randomTeleportMaxHeight &&
                heightDifference >= randomTeleportMinHeight) {
                validPlatforms.Add(platform);
            }
        }

        if (validPlatforms.Count == 0) {
            Debug.Log("No valid platforms found within height range");
            return null;
        }

        // Randomly select one of the valid platforms
        int randomIndex = Random.Range(0, validPlatforms.Count);
        Platform randomPlatform = validPlatforms[randomIndex];

        Debug.Log($"Selected random platform {randomPlatform.index} at height difference {GetPlatformHeight(randomPlatform) - playerHeight:F2}");
        return randomPlatform;
    }

    private void UpdateTargetForPlayerPlatform(int playerPlatformIndex) {
        if (playerPlatformIndex == -1 || playerPlatformIndex >= platforms.Count) return;

        Platform currentPlatform = platforms[currentPlatformIndex];
        Platform playerPlatform = platforms[playerPlatformIndex];

        // Calculate if we can directly teleport
        float currentHeight = GetPlatformHeight(currentPlatform);
        float targetHeight = GetPlatformHeight(playerPlatform);
        float heightDifference = targetHeight - currentHeight;

        // If player platform is within height threshold, target directly
        if (heightDifference <= maxHeightDifference && heightDifference >= minHeightDifference) {
            targetPlatform = playerPlatform;
            intermediatePlatform = null;
            Debug.Log($"Set target platform to player's platform {playerPlatformIndex}");
        }
        // Otherwise, find an intermediate platform
        else {
            targetPlatform = null;
            intermediatePlatform = FindIntermediatePlatform(currentPlatform, playerPlatform);

            if (intermediatePlatform != null) {
                Debug.Log($"Set intermediate platform {intermediatePlatform.index} towards player's platform {playerPlatformIndex}");
            }
            else {
                Debug.Log($"Couldn't find intermediate platform towards player's platform {playerPlatformIndex}");
            }
        }
    }

    // Modify TeleportToPlatform to leave some targets active for next teleport
    private void TeleportToPlatform(Platform platform) {
        if (platform == null) {
            Debug.LogError("Attempted to teleport to null platform!");
            return;
        }

        Debug.Log($"TeleportToPlatform called for platform {platform.index}");

        // Store information about where we're teleporting to
        int newPlatformIndex = platform.index;
        Platform newPlatform = platform;

        // Randomly choose left or right edge of target platform
        Vector2 leftEdge = platformTilemap.GetCellCenterWorld(platform.leftPoint);
        Vector2 rightEdge = platformTilemap.GetCellCenterWorld(platform.rightPoint);

        Vector2 targetPos = (Random.value < 0.5f) ? leftEdge : rightEdge;

        // Add a small vertical offset to prevent sinking into the ground
        targetPos.y += 0.5f;

        // Teleport to the target position
        Debug.Log($"Teleporting to platform {platform.index} at position {targetPos}");
        Teleport(targetPos);

        // Update current platform index
        currentPlatformIndex = newPlatformIndex;

        // Reset teleport cooldown
        canTeleport = false;
        teleportTimer = teleportCooldown;

        // Important: update targets based on new platform position and player position
        int playerPlatform = GetPlatformAtPosition(player.position);
        if (playerPlatform != -1 && playerPlatform != currentPlatformIndex) {
            // Since we just teleported, immediately calculate the next target
            Debug.Log($"After teleport, player is on different platform {playerPlatform} - calculating next target");
            if (!useRandomTeleport) {
                UpdateTargetForPlayerPlatform(playerPlatform);
            }
            else {
                // In random mode, just clear the targets
                targetPlatform = null;
                intermediatePlatform = null;
            }
        }
        else {
            // Player is on the same platform we teleported to, so clear targets
            targetPlatform = null;
            intermediatePlatform = null;
        }
    }

    private Platform FindIntermediatePlatform(Platform current, Platform target) {
        float currentHeight = GetPlatformHeight(current);
        float targetHeight = GetPlatformHeight(target);

        // Find platforms that are:
        // 1. Within height threshold from current platform
        // 2. Closer to target platform in height than current platform

        Platform bestPlatform = null;
        float bestHeightDifference = float.MaxValue;

        foreach (Platform platform in platforms) {
            if (platform.index == current.index) continue; // Skip current platform

            float platformHeight = GetPlatformHeight(platform);

            // Calculate height differences
            float heightDiffFromCurrent = platformHeight - currentHeight;
            float heightDiffToTarget = Mathf.Abs(platformHeight - targetHeight);

            // Check if within threshold from current
            if (heightDiffFromCurrent <= maxHeightDifference && heightDiffFromCurrent >= minHeightDifference) {
                // Check if this platform is closer to target in height than current platform
                float currentToTargetDiff = Mathf.Abs(currentHeight - targetHeight);

                if (heightDiffToTarget < currentToTargetDiff && heightDiffToTarget < bestHeightDifference) {
                    bestPlatform = platform;
                    bestHeightDifference = heightDiffToTarget;
                }
            }
        }

        return bestPlatform;
    }

    private void Teleport(Vector2 targetPosition) {
        // Play teleport effect at current position
        if (teleportEffect != null) {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // Play teleport sound
        if (audioSource != null && teleportSound != null) {
            audioSource.PlayOneShot(teleportSound);
        }

        // Store original position for effect
        Vector2 startPosition = transform.position;

        // Move to the target position
        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);

        // Reset velocity
        rb.linearVelocity = Vector2.zero;

        // Play teleport effect at target position
        if (teleportEffect != null) {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // Flash color
        if (spriteRenderer != null) {
            StartCoroutine(FlashColor());
        }

        Debug.Log($"Teleported from {startPosition} to {targetPosition}");
    }

    private void CheckGrounded() {
        // Check if the enemy is touching the ground
        bool wasGrounded = isGrounded;
        isGrounded = false;

        // Cast rays to check for ground
        Collider2D col = GetComponent<Collider2D>();
        float width = col != null ? col.bounds.size.x * 0.8f : 0.5f;

        for (int i = 0; i < 3; i++) {
            float offset = (i - 1) * (width / 2f);
            Vector2 rayStart = new Vector2(transform.position.x + offset, transform.position.y);

            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, groundCheckDistance, groundLayer);

            if (hit.collider != null) {
                isGrounded = true;
                break;
            }
        }

        if (wasGrounded != isGrounded) {
            Debug.Log(isGrounded ? "Enemy is now grounded" : "Enemy is not grounded");
        }
    }

    private void ScanForPlatforms() {
        platforms.Clear();

        BoundsInt bounds = platformTilemap.cellBounds;
        HashSet<Vector3Int> visitedTiles = new HashSet<Vector3Int>();

        Debug.Log($"Scanning tilemap bounds: X({bounds.xMin}-{bounds.xMax}), Y({bounds.yMin}-{bounds.yMax})");

        for (int x = bounds.xMin; x < bounds.xMax; x++) {
            for (int y = bounds.yMin; y < bounds.yMax; y++) {
                Vector3Int pos = new Vector3Int(x, y, 0);

                // Skip if already processed or no tile
                if (visitedTiles.Contains(pos) || !platformTilemap.HasTile(pos)) continue;

                // Skip if not in chase area
                Vector3 worldPos = platformTilemap.GetCellCenterWorld(pos);
                if (!IsPointInChaseArea(worldPos)) continue;

                // Found a new platform, use flood fill to identify connected tiles
                Platform platform = new Platform();
                platform.index = platforms.Count;
                FloodFillPlatform(pos, visitedTiles, platform);

                // Only add platforms with at least 3 tiles
                if (platform.tiles.Count >= 3) {
                    platform.CalculateEndpoints();
                    platforms.Add(platform);

                    Debug.Log($"Platform {platform.index} found with {platform.tiles.Count} tiles");
                }
            }
        }
    }

    private void FloodFillPlatform(Vector3Int startPos, HashSet<Vector3Int> visitedTiles, Platform platform) {
        Queue<Vector3Int> tilesToProcess = new Queue<Vector3Int>();
        tilesToProcess.Enqueue(startPos);

        while (tilesToProcess.Count > 0) {
            Vector3Int currentPos = tilesToProcess.Dequeue();

            // Skip if already visited or not a valid tile or not in chase area
            if (visitedTiles.Contains(currentPos) || !platformTilemap.HasTile(currentPos)) continue;
            Vector3 worldPos = platformTilemap.GetCellCenterWorld(currentPos);
            if (!IsPointInChaseArea(worldPos)) continue;

            // Mark as visited and add to platform
            visitedTiles.Add(currentPos);
            platform.tiles.Add(currentPos);

            // Check horizontal neighbors only
            tilesToProcess.Enqueue(currentPos + Vector3Int.left);
            tilesToProcess.Enqueue(currentPos + Vector3Int.right);
        }
    }

    private bool IsPointInChaseArea(Vector3 point) {
        Debug.Log("Checking if point is in chase area: " + point);
        // If chase area is null, consider all points valid
        if (chaseArea == null) {
            Debug.LogError("Chase area is null - considering all points in chase area");
            return true;
        }
        
        try {
            return chaseArea.OverlapPoint(point);
        }
        catch (System.Exception e) {
            Debug.LogError($"Error in IsPointInChaseArea: {e.Message}");
            return true; // Default to allowing all points
        }
    }

    private int GetPlatformAtPosition(Vector3 worldPosition) {
        // First check if the position is in the chase area
        if (!IsPointInChaseArea(worldPosition)) {
            return -1;
        }

        // Cast a ray down to find the platform below
        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.down, 2f, groundLayer);
        if (hit.collider != null) {
            Vector3Int hitCell = platformTilemap.WorldToCell(hit.point);

            // Check which platform contains this point
            foreach (Platform platform in platforms) {
                if (platform.ContainsCell(hitCell)) {
                    return platform.index;
                }

                // Check surrounding cells as well
                for (int yOffset = -1; yOffset <= 1; yOffset++) {
                    for (int xOffset = -1; xOffset <= 1; xOffset++) {
                        Vector3Int checkCell = new Vector3Int(hitCell.x + xOffset, hitCell.y + yOffset, hitCell.z);
                        if (platform.ContainsCell(checkCell)) {
                            return platform.index;
                        }
                    }
                }
            }
        }

        return -1; // Not on any platform
    }

    private int FindClosestPlatformIndex(Vector3 worldPosition) {
        float closestDistance = float.MaxValue;
        int closestPlatform = -1;

        foreach (Platform platform in platforms) {
            Vector3 platformCenter = GetPlatformCenter(platform);
            float distance = Vector2.Distance(worldPosition, platformCenter);

            if (distance < closestDistance) {
                closestDistance = distance;
                closestPlatform = platform.index;
            }
        }

        return closestPlatform;
    }

    private Vector3 GetPlatformCenter(Platform platform) {
        // Calculate the center of the platform by averaging all tile positions
        Vector3 sum = Vector3.zero;
        foreach (Vector3Int tile in platform.tiles) {
            sum += platformTilemap.GetCellCenterWorld(tile);
        }
        return sum / platform.tiles.Count;
    }

    private float GetPlatformHeight(Platform platform) {
        // Return the average y position of the platform
        Vector3 leftPos = platformTilemap.GetCellCenterWorld(platform.leftPoint);
        Vector3 rightPos = platformTilemap.GetCellCenterWorld(platform.rightPoint);
        return (leftPos.y + rightPos.y) / 2f;
    }

    private IEnumerator FlashColor() {
        spriteRenderer.color = teleportColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }

    // Clean up triggers when scene changes
    private void OnDestroy() {
        foreach (var triggerList in platformTriggers.Values) {
            foreach (var trigger in triggerList) {
                if (trigger != null) {
                    Destroy(trigger);
                }
            }
        }
    }

    // Method for external systems to set player reference
    public void SetPlayer(Transform playerTransform) {
        this.player = playerTransform;
    }

    // Method for external systems to configure the enemy
    public void SetProperties(float newMoveSpeed, float newMaxHeightDifference, float newMinHeightDifference,
                        float newTeleportCooldown, bool useRandomTeleport,
                        float newRandomTeleportMaxHeight, float newRandomTeleportMinHeight) {
        // Validate and clamp values
        moveSpeed = Mathf.Max(1f, newMoveSpeed);
        maxHeightDifference = Mathf.Max(1f, newMaxHeightDifference);
        minHeightDifference = Mathf.Min(-1f, newMinHeightDifference);
        teleportCooldown = Mathf.Clamp(newTeleportCooldown, 0.5f, 5f);
        this.useRandomTeleport = useRandomTeleport;
        randomTeleportMaxHeight = Mathf.Max(1f, newRandomTeleportMaxHeight);
        randomTeleportMinHeight = Mathf.Min(-1f, newRandomTeleportMinHeight);

        // Log property changes if in debug mode
        if (debugMode) {
            Debug.Log($"Enemy properties updated: Speed={moveSpeed}, " +
                     $"HeightDifference=[{minHeightDifference}, {maxHeightDifference}], " +
                     $"TeleportCooldown={teleportCooldown}, UseRandomTeleport={this.useRandomTeleport}");
        }
    }

    [System.Serializable]
    public class Platform {
        public int index;
        public List<Vector3Int> tiles = new List<Vector3Int>();
        public Vector3Int leftPoint;
        public Vector3Int rightPoint;

        public void CalculateEndpoints() {
            leftPoint = tiles[0];
            rightPoint = tiles[0];

            foreach (Vector3Int tile in tiles) {
                if (tile.x < leftPoint.x) {
                    leftPoint = tile;
                }
                if (tile.x > rightPoint.x) {
                    rightPoint = tile;
                }
            }
        }

        public bool ContainsCell(Vector3Int cell) {
            foreach (Vector3Int tile in tiles) {
                if (tile.x == cell.x && tile.y == cell.y) {
                    return true;
                }
            }
            return false;
        }
    }

    // Optional: visualize the platforms and chase area in editor
    private void OnDrawGizmos() {
        // Draw chase area
        if (chaseArea != null) {
            Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.2f);
            Vector2[] points = chaseArea.points;
            for (int i = 0; i < points.Length; i++) {
                Vector2 worldPoint = chaseArea.transform.TransformPoint(points[i]);
                Vector2 nextWorldPoint = chaseArea.transform.TransformPoint(points[(i + 1) % points.Length]);
                Gizmos.DrawLine(worldPoint, nextWorldPoint);
            }
        }

        // Only draw platforms in play mode
        if (!Application.isPlaying) return;

        // Draw platforms
        foreach (Platform platform in platforms) {
            // Draw platform outline
            Gizmos.color = Color.blue;
            foreach (Vector3Int tile in platform.tiles) {
                Vector3 worldPos = platformTilemap.GetCellCenterWorld(tile);
                Gizmos.DrawWireCube(worldPos, new Vector3(0.9f, 0.9f, 0.1f));
            }

            // Draw platform endpoints
            if (platform.leftPoint != null && platform.rightPoint != null) {
                Vector3 leftPos = platformTilemap.GetCellCenterWorld(platform.leftPoint);
                Vector3 rightPos = platformTilemap.GetCellCenterWorld(platform.rightPoint);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(leftPos, 0.2f);
                Gizmos.DrawSphere(rightPos, 0.2f);

                // Draw trigger boxes
                if (Application.isPlaying) {
                    Gizmos.color = new Color(1f, 0.5f, 0, 0.5f); // Orange transparent
                    Vector3 leftTriggerPos = leftPos + Vector3.up * (triggerBoxSize.y / 2);
                    Vector3 rightTriggerPos = rightPos + Vector3.up * (triggerBoxSize.y / 2);
                    Gizmos.DrawWireCube(leftTriggerPos, triggerBoxSize);
                    Gizmos.DrawWireCube(rightTriggerPos, triggerBoxSize);
                }

                // Draw platform ID
                Vector3 centerPos = (leftPos + rightPos) / 2f + Vector3.up * 0.5f;
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(centerPos, $"Platform {platform.index}");
                #endif
            }
        }

        // Draw current and target platform highlights
        if (currentPlatformIndex >= 0 && currentPlatformIndex < platforms.Count) {
            Platform current = platforms[currentPlatformIndex];
            Gizmos.color = Color.green;
            Vector3 currentCenter = GetPlatformCenter(current);
            Gizmos.DrawWireSphere(currentCenter, 0.5f);
        }

        if (targetPlatformIndex >= 0 && targetPlatformIndex < platforms.Count) {
            Platform target = platforms[targetPlatformIndex];
            Gizmos.color = Color.yellow;
            Vector3 targetCenter = GetPlatformCenter(target);
            Gizmos.DrawWireSphere(targetCenter, 0.5f);
        }
    }
}

[RequireComponent(typeof(BoxCollider2D))]
public class PlatformTrigger : MonoBehaviour {
    private GroundEnemyFixedChase enemy;
    private int platformIndex;
    private bool isLeftCorner;
    private float cooldownTimer = 0f;
    private const float triggerCooldown = 1.5f; // Time between trigger activations

    public void Setup(GroundEnemyFixedChase enemy, int platformIndex, bool isLeftCorner) {
        this.enemy = enemy;
        this.platformIndex = platformIndex;
        this.isLeftCorner = isLeftCorner;
        Debug.Log($"Trigger for platform {platformIndex} ({(isLeftCorner ? "left" : "right")}) setup complete");
    }

    private void Start() {
        // Double-check the collider is setup correctly
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (!col.isTrigger) {
            Debug.LogError($"Trigger on {gameObject.name} is not set as isTrigger!");
            col.isTrigger = true;
        }
    }

    private void Update() {
        // Cool down the trigger
        if (cooldownTimer > 0) {
            cooldownTimer -= Time.deltaTime;
        }
    }

    // Make sure the OnTriggerEnter2D method is being called
    private void OnTriggerEnter2D(Collider2D collision) {
        // Check if on cooldown
        if (cooldownTimer > 0) {
            Debug.Log($"Trigger for platform {platformIndex} on cooldown, ignoring collision");
            return;
        }

        Debug.Log($"Trigger for platform {platformIndex} detected collision with {collision.gameObject.name}");

        // Only respond to the enemy that created this trigger
        if (collision.gameObject == enemy.gameObject) {
            Debug.Log($"IT'S THE ENEMY! Calling OnPlatformTriggerEnter for platform {platformIndex}");
            cooldownTimer = triggerCooldown; // Prevent rapid re-triggering
            enemy.OnPlatformTriggerEnter(platformIndex, isLeftCorner);
        }
    }

    // Also check for stay events for more reliable detection
    private void OnTriggerStay2D(Collider2D collision) {
        // No need to process if on cooldown
        if (cooldownTimer > 0) return;

        // If enemy is staying in trigger and the teleport is ready, activate it
        if (collision.gameObject == enemy.gameObject) {
            Debug.Log($"Enemy STAYING in trigger for platform {platformIndex} - activating");
            cooldownTimer = triggerCooldown;
            enemy.OnPlatformTriggerEnter(platformIndex, isLeftCorner);
        }
    }

    // Visualize the trigger in scene view for debugging
    private void OnDrawGizmos() {
        Gizmos.color = new Color(1f, 0, 0, 0.3f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) {
            Gizmos.DrawCube(transform.position, col.size);
        }
    }
}