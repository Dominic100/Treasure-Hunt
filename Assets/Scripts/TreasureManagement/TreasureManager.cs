using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TreasureManager : MonoBehaviour {
    [Header("Tilemaps")]
    public Tilemap platformTilemap;
    public Tilemap ladderTilemap;

    [Header("Treasure Settings")]
    public GameObject treasurePrefab;
    public int minTreasureDistance = 5;
    public int treasureCount = 10;
    public int treasureBaseValue = 100;

    [Header("Respawn Settings")]
    public bool enableRespawning = true;
    public float respawnDelay = 2f;
    public float respawnDistanceFromPlayer = 10f;
    public bool increaseDifficultyOverTime = false;
    
    private bool[,] reachableTiles;
    private List<GameObject> activeTreasures = new List<GameObject>();
    private int collectedTreasures = 0;
    private List<Vector2Int> usedPositions = new List<Vector2Int>();
    private Transform playerTransform;

    // Event for GameManager to subscribe to
    public event Action<int> OnTreasureCollected;

    private void Awake() {
        // Find player reference
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            playerTransform = player.transform;
        }
    }

    public void Initialize() {
        ResetTreasures();
        
        // Start respawn checker if enabled
        if (enableRespawning) {
            StartCoroutine(CheckTreasureCount());
        }
    }

    private IEnumerator CheckTreasureCount() {
        while (true) {
            // Check if we need more treasures
            if (activeTreasures.Count < treasureCount) {
                RespawnTreasure();
            }
            
            yield return new WaitForSeconds(1f);
        }
    }

    public void ResetTreasures() {
        // Stop all coroutines to avoid conflicts
        StopAllCoroutines();
        
        // Clear existing treasures with extra safety
        for (int i = activeTreasures.Count - 1; i >= 0; i--) {
            if (activeTreasures[i] != null) {
                Destroy(activeTreasures[i]);
            }
        }
        activeTreasures.Clear();
        usedPositions.Clear();
        collectedTreasures = 0;

        // Generate new treasures
        ComputeReachableTiles();
        PlaceTreasures();
        
        // Start respawn checker if enabled
        if (enableRespawning) {
            StartCoroutine(CheckTreasureCount());
        }
    }

    // Add this public method to TreasureManager class right after the existing GetRemainingTreasures() method
    public int GetTreasureCount() {
        return collectedTreasures;
    }

    public void CollectTreasure(GameObject treasure) {
        Debug.Log($"TreasureManager.CollectTreasure called for {treasure.name}");
        
        if (activeTreasures.Contains(treasure)) {
            Debug.Log($"Treasure {treasure.name} found in active treasures list");
            activeTreasures.Remove(treasure);
            collectedTreasures++;
            
            int value = CalculateTreasureValue(treasure);
            Debug.Log($"Invoking OnTreasureCollected event with value: {value}");
            
            if (OnTreasureCollected != null) {
                OnTreasureCollected.Invoke(value);
            }
            else {
                Debug.LogError("OnTreasureCollected event has no subscribers!");
            }
            
            // Start respawn timer if enabled
            if (enableRespawning) {
                StartCoroutine(RespawnAfterDelay());
            }
            
            Destroy(treasure);
        }
        else {
            Debug.LogWarning($"Treasure {treasure.name} not found in active treasures list!");
            // List all active treasures for debugging
            Debug.Log($"Active treasures ({activeTreasures.Count}):");
            foreach (var t in activeTreasures) {
                Debug.Log($" - {(t != null ? t.name : "null")}");
            }
            
            // Destroy anyway
            Destroy(treasure);
        }
    }
    
    private IEnumerator RespawnAfterDelay() {
        yield return new WaitForSeconds(respawnDelay);
        RespawnTreasure();
    }
    
    private void RespawnTreasure() {
        // Get positions away from player
        Vector2Int? newPos = GetSpawnPositionAwayFromPlayer();
        if (!newPos.HasValue) {
            Debug.Log("No valid respawn position found away from player");
            return;
        }
        
        Vector3Int cellPos = new Vector3Int(newPos.Value.x, newPos.Value.y, 0);
        Vector3 worldPos = platformTilemap.GetCellCenterWorld(cellPos);
        worldPos.y += 0.5f; // Lift above platform
        
        GameObject treasure = Instantiate(treasurePrefab, worldPos, Quaternion.identity);
        
        // Configure treasure physics
        Rigidbody2D rb = treasure.GetComponent<Rigidbody2D>();
        if (rb != null) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
        }
        
        treasure.transform.parent = transform;
        activeTreasures.Add(treasure);
        usedPositions.Add(newPos.Value);
        
        Debug.Log($"Respawned treasure at {worldPos}");
    }

    private Vector2Int? GetSpawnPositionAwayFromPlayer() {
        if (playerTransform == null) {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) {
                playerTransform = player.transform;
            }
            else {
                Debug.LogWarning("Player not found - can't calculate distance for respawn");
                // Fall back to a random position
                List<Vector2Int> allTiles = GetReachableTiles();
                if (allTiles.Count > 0) {
                    return allTiles[UnityEngine.Random.Range(0, allTiles.Count)];
                }
                return null;
            }
        }
        
        // Get player position in tilemap space
        Vector3Int playerCell = platformTilemap.WorldToCell(playerTransform.position);
        Vector2Int playerTilePos = new Vector2Int(playerCell.x, playerCell.y);
        
        // Get all reachable tiles
        List<Vector2Int> reachableTiles = GetReachableTiles();
        
        // Filter by distance and used positions
        List<Vector2Int> validTiles = new List<Vector2Int>();
        foreach (Vector2Int tile in reachableTiles) {
            float distInTiles = Vector2.Distance(tile, playerTilePos);
            if (distInTiles >= respawnDistanceFromPlayer / platformTilemap.cellSize.x && 
                !usedPositions.Contains(tile)) {
                validTiles.Add(tile);
            }
        }
        
        // If there are valid tiles, pick a random one
        if (validTiles.Count > 0) {
            return validTiles[UnityEngine.Random.Range(0, validTiles.Count)];
        }
        
        // If no valid tiles, try any unreachable tiles that don't have a platform
        foreach (Vector2Int tile in reachableTiles) {
            if (!usedPositions.Contains(tile)) {
                validTiles.Add(tile);
            }
        }
        
        if (validTiles.Count > 0) {
            return validTiles[UnityEngine.Random.Range(0, validTiles.Count)];
        }
        
        // If everything fails, return null
        return null;
    }

    private int CalculateTreasureValue(GameObject treasure) {
        // This can be expanded to handle different treasure types
        if (increaseDifficultyOverTime) {
            // Increase value as more treasures are collected
            return treasureBaseValue + (int)(treasureBaseValue * 0.05f * collectedTreasures);
        }
        return treasureBaseValue;
    }

    public int GetCollectedTreasures() {
        return collectedTreasures;
    }

    public int GetRemainingTreasures() {
        return activeTreasures.Count;
    }

    void ComputeReachableTiles() {
        if (platformTilemap == null || ladderTilemap == null) {
            Debug.LogError("Tilemaps not assigned in TreasureManager!");
            return;
        }

        // Get the actual bounds of the tilemap
        BoundsInt bounds = platformTilemap.cellBounds;
        Debug.Log($"Tilemap bounds: x={bounds.x} to {bounds.x + bounds.size.x}, " +
                  $"y={bounds.y} to {bounds.y + bounds.size.y}");

        // Initialize the reachable tiles array using the bounds
        reachableTiles = new bool[bounds.size.x, bounds.size.y];

        // Check each cell within the bounds
        for (int x = bounds.x; x < bounds.x + bounds.size.x; x++) {
            for (int y = bounds.y; y < bounds.y + bounds.size.y; y++) {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                Vector3Int belowPos = new Vector3Int(x, y - 1, 0);

                // Check if there's a platform below
                bool platformBelow = platformTilemap.HasTile(belowPos);

                // Check if this is a ladder cell
                bool isLadder = ladderTilemap.HasTile(tilePos);

                // Check if there's a ladder below but not at current position
                bool isAboveLadder = ladderTilemap.HasTile(belowPos) && !ladderTilemap.HasTile(tilePos);

                // Convert world coordinates to array indices
                int arrayX = x - bounds.x;
                int arrayY = y - bounds.y;

                // Cell is reachable if:
                // 1. It's directly above a platform OR
                // 2. It's a ladder cell OR
                // 3. It's the cell just above a ladder's highest point
                if (platformBelow || isLadder || isAboveLadder) {
                    reachableTiles[arrayX, arrayY] = true;
                }
            }
        }
    }

    List<Vector2Int> GetReachableTiles() {
        List<Vector2Int> tiles = new List<Vector2Int>();
        BoundsInt bounds = platformTilemap.cellBounds;

        for (int x = 0; x < bounds.size.x; x++) {
            for (int y = 0; y < bounds.size.y; y++) {
                if (reachableTiles[x, y]) {
                    // Convert array indices back to world coordinates
                    Vector3Int tilePos = new Vector3Int(x + bounds.x, y + bounds.y, 0);
                    if (!platformTilemap.HasTile(tilePos)) {
                        tiles.Add(new Vector2Int(tilePos.x, tilePos.y));
                    }
                }
            }
        }
        return tiles;
    }

    List<Vector2Int> PoissonSampleTiles(List<Vector2Int> candidates, int minDistance, int maxTreasures) {
        List<Vector2Int> treasures = new List<Vector2Int>();
        if (candidates.Count == 0) return treasures;

        // Pick first random position
        treasures.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        usedPositions.Add(treasures[0]); // Track used positions

        int attempts = 0;
        int maxAttempts = candidates.Count * 10;

        while (treasures.Count < maxTreasures && attempts < maxAttempts) {
            Vector2Int candidate = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            attempts++;

            bool valid = true;
            foreach (var placed in treasures) {
                int manhattanDist = Mathf.Abs(candidate.x - placed.x) +
                                  Mathf.Abs(candidate.y - placed.y);
                if (manhattanDist < minDistance) {
                    valid = false;
                    break;
                }
            }

            if (valid) {
                treasures.Add(candidate);
                usedPositions.Add(candidate); // Track used positions
                attempts = 0;
            }
        }

        return treasures;
    }

    void PlaceTreasures() {
        if (treasurePrefab == null) {
            Debug.LogError("Treasure prefab not assigned in TreasureManager!");
            return;
        }

        List<Vector2Int> reachableTiles = GetReachableTiles();
        List<Vector2Int> treasurePositions = PoissonSampleTiles(reachableTiles,
                                                            minTreasureDistance,
                                                            treasureCount);

        foreach (var tilePos in treasurePositions) {
            // Get center position of the tile
            Vector3 worldPos = platformTilemap.GetCellCenterWorld((Vector3Int)tilePos);
            
            // Adjust position slightly above the platform to prevent falling through
            worldPos.y += 0.5f;
            
            // Create treasure with correct rotation (no tilt)
            GameObject treasure = Instantiate(treasurePrefab, worldPos, Quaternion.identity);
            
            // Important: Make sure it doesn't have a regular rigidbody that would be affected by gravity
            Rigidbody2D rb = treasure.GetComponent<Rigidbody2D>();
            if (rb != null) {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0;
            }
            
            treasure.transform.parent = transform; // Parent to TreasureManager
            activeTreasures.Add(treasure);
            
            Debug.Log($"Placed treasure at position {worldPos}");
        }

        Debug.Log($"Placed {activeTreasures.Count} treasures");
    }

    void OnDrawGizmos() {
        if (reachableTiles == null || !platformTilemap) return;

        BoundsInt bounds = platformTilemap.cellBounds;

        for (int x = 0; x < bounds.size.x; x++) {
            for (int y = 0; y < bounds.size.y; y++) {
                if (reachableTiles[x, y]) {
                    // Convert array indices back to world coordinates
                    Vector3Int tilePos = new Vector3Int(x + bounds.x, y + bounds.y, 0);
                    Vector3 worldPos = platformTilemap.GetCellCenterWorld(tilePos);
                    Gizmos.color = new Color(0, 1, 0, 0.3f);
                    Gizmos.DrawCube(worldPos, Vector3.one * 0.5f);
                }
            }
        }
    }

    void OnDestroy() {
        // Clean up any remaining treasures
        foreach (var treasure in activeTreasures) {
            if (treasure != null)
                Destroy(treasure);
        }
        activeTreasures.Clear();
        
        // Stop all coroutines
        StopAllCoroutines();
    }
}