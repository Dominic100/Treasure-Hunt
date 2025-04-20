using UnityEngine;

public class Treasure : MonoBehaviour {
    private bool isCollected = false;
    
    private void Awake() {
        // Configure physics to prevent falling
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) {
            // Set to kinematic so gravity doesn't affect it
            rb.bodyType = RigidbodyType2D.Kinematic;
            // Or just remove gravity effect
            rb.gravityScale = 0;
        }
    }

    private void Start() {
        // Ensure we have a trigger collider for player interaction
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) {
            Debug.LogError($"Treasure {gameObject.name} is missing a Collider2D component!");
            CircleCollider2D circleCol = gameObject.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = 0.5f;
            Debug.Log("Added CircleCollider2D to treasure");
        } 
        else if (!col.isTrigger) {
            Debug.LogWarning($"Treasure collider must be a trigger! Setting isTrigger to true.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (isCollected) return; // Prevent double collection
        
        Debug.Log($"Treasure trigger with: {other.gameObject.name} (tag: {other.tag})");
        
        if (other.CompareTag("Player")) {
            isCollected = true;
            Debug.Log($"Player collecting treasure: {gameObject.name}");
            
            var treasureManager = FindAnyObjectByType<TreasureManager>();
            if (treasureManager != null) {
                treasureManager.CollectTreasure(gameObject);
            }
            else {
                Debug.LogError("TreasureManager not found!");
                Destroy(gameObject);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision) {
    Debug.Log($"Collision (not trigger) with: {collision.gameObject.name}");
    
    // Try to handle collection even if using regular collision
    if (collision.gameObject.CompareTag("Player")) {
        Debug.Log("Player collision detected - attempting collection via collision");
        OnTriggerEnter2D(collision.collider);
    }
}
}