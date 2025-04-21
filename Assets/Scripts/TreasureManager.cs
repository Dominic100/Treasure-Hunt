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
    
    [Header("Respawn Settings")]
    public float respawnDelay = 2f;
    public float minDistanceFromPlayer = 8f; // Minimum distance from player for spawning
    
    private bool[,] reachableTiles;
    private List<GameObject> activeTreasures = new List<GameObject>();
    private int collectedTreasures = 0;
    private List<Vector2Int> usedPositions = new List<Vector2Int>();
    private Transform playerTransform;
    
    // Event for external scripts to subscribe to
    public event Action OnTreasureCollected;

    private void Awake() {
        // Find player reference
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            playerTransform = player.transform;
        }
        
        // Initialize immediately
        Initialize();
    }

    public void Initialize() {
        ResetTreasures();
    }

    public void ResetTreasures() {
        // Stop all coroutines to avoid conflicts
        StopAllCoroutines();
        
        // Clear existing treasures
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
    }

    public int GetCollectedTreasures() {
        return collectedTreasures;
    }

    public int GetRemainingTreasures() {
        return activeTreasures.Count;
    }

    public void CollectTreasure(GameObject treasure) {
        if (activeTreasures.Contains(treasure)) {
            activeTreasures.Remove(treasure);
            collectedTreasures++;
            
            // Notify subscribers
            OnTreasureCollected?.Invoke();
            
            // Start respawn timer
            StartCoroutine(RespawnAfterDelay());
            
            Destroy(treasure);
        }
        else {
            // Destroy anyway if not tracked
            Destroy(treasure);
        }
    }
    
    private IEnumerator RespawnAfterDelay() {
        yield return new WaitForSeconds(respawnDelay);
        SpawnNewTreasure();
    }
    
    private void SpawnNewTreasure() {
        if (playerTransform == null) {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) {
                playerTransform = player.transform;
            }
        }
        
        List<Vector2Int> reachableTiles = GetReachableTiles();
        List<Vector2Int> validTiles = new List<Vector2Int>();
        
        // If player exists, filter by distance from player
        if (playerTransform != null) {
            Vector3Int playerCell = platformTilemap.WorldToCell(playerTransform.position);
            Vector2Int playerTilePos = new Vector2Int(playerCell.x, playerCell.y);
            float minDistInTiles = minDistanceFromPlayer / platformTilemap.cellSize.x;
            
            foreach (Vector2Int tile in reachableTiles) {
                if (!usedPositions.Contains(tile)) {
                    float distToPlayer = Vector2.Distance(tile, playerTilePos);
                    if (distToPlayer >= minDistInTiles) {
                        validTiles.Add(tile);
                    }
                }
            }
            
            // If no tiles meet the distance requirement, add any unused tiles
            if (validTiles.Count == 0) {
                foreach (Vector2Int tile in reachableTiles) {
                    if (!usedPositions.Contains(tile)) {
                        validTiles.Add(tile);
                    }
                }
            }
        } else {
            // No player reference, just filter out used positions
            foreach (Vector2Int tile in reachableTiles) {
                if (!usedPositions.Contains(tile)) {
                    validTiles.Add(tile);
                }
            }
        }
        
        if (validTiles.Count == 0) {
            // No available positions, clear used positions and retry
            usedPositions.Clear();
            validTiles = reachableTiles;
        }
        
        if (validTiles.Count > 0) {
            // Pick a random position
            Vector2Int tilePos = validTiles[UnityEngine.Random.Range(0, validTiles.Count)];
            Vector3 worldPos = platformTilemap.GetCellCenterWorld(new Vector3Int(tilePos.x, tilePos.y, 0));
            worldPos.y += 0.5f; // Lift above platform
            
            // Create treasure
            GameObject treasure = Instantiate(treasurePrefab, worldPos, Quaternion.identity);
            
            // Configure treasure physics
            Rigidbody2D rb = treasure.GetComponent<Rigidbody2D>();
            if (rb != null) {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0;
            }
            
            treasure.transform.parent = transform;
            activeTreasures.Add(treasure);
            usedPositions.Add(tilePos);
        }
    }

    void ComputeReachableTiles() {
        if (platformTilemap == null || ladderTilemap == null) {
            Debug.LogError("Tilemaps not assigned in TreasureManager!");
            return;
        }

        // Get the actual bounds of the tilemap
        BoundsInt bounds = platformTilemap.cellBounds;

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
        
        // Apply player distance filtering for initial placement 
        if (playerTransform != null) {
            Vector3Int playerCell = platformTilemap.WorldToCell(playerTransform.position);
            Vector2Int playerTilePos = new Vector2Int(playerCell.x, playerCell.y);
            float minDistInTiles = minDistanceFromPlayer / platformTilemap.cellSize.x;
            
            List<Vector2Int> distantTiles = new List<Vector2Int>();
            foreach (Vector2Int tile in reachableTiles) {
                float distToPlayer = Vector2.Distance(tile, playerTilePos);
                if (distToPlayer >= minDistInTiles) {
                    distantTiles.Add(tile);
                }
            }
            
            // If we have enough distant tiles, use them
            if (distantTiles.Count >= treasureCount * 1.5f) {
                reachableTiles = distantTiles;
            }
            // Otherwise use all tiles (fallback)
        }
        
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