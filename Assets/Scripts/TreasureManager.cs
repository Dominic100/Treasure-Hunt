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

    private bool[,] reachableTiles;

    void Start() {
        ComputeReachableTiles();
        PlaceTreasures();
    }

    void ComputeReachableTiles() {
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

    // Update GetReachableTiles to use bounds
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
        treasures.Add(candidates[Random.Range(0, candidates.Count)]);

        int attempts = 0;
        int maxAttempts = candidates.Count * 10;

        while (treasures.Count < maxTreasures && attempts < maxAttempts) {
            Vector2Int candidate = candidates[Random.Range(0, candidates.Count)];
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
                attempts = 0;
            }
        }

        return treasures;
    }

    void PlaceTreasures() {
        List<Vector2Int> reachableTiles = GetReachableTiles();
        List<Vector2Int> treasurePositions = PoissonSampleTiles(reachableTiles,
                                                               minTreasureDistance,
                                                               treasureCount);

        foreach (var tilePos in treasurePositions) {
            Vector3 worldPos = platformTilemap.GetCellCenterWorld((Vector3Int)tilePos);
            Instantiate(treasurePrefab, worldPos, Quaternion.identity);
        }
    }

    // Debug visualization
    void OnDrawGizmos() {
        if (reachableTiles == null) return;
        if (!platformTilemap) return;

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
}
