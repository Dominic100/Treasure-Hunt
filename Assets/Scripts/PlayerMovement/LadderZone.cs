using UnityEngine;

public class LadderZone : MonoBehaviour {
    private void Start() {
        // Ensure the composite collider is set as a trigger
        CompositeCollider2D compositeCollider = GetComponent<CompositeCollider2D>();
        if (compositeCollider != null) {
            compositeCollider.isTrigger = true;
            Debug.Log("Composite collider is set as a trigger");
        }
    }

    private void OnTriggerStay2D(Collider2D collision) {
        // This will continuously check while the player is in contact with the ladder
        if (collision.CompareTag("Player")) {
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null) {
                player.OnLadder = true;
                Debug.Log("Player is on ladder (Stay)");
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) {
        if (collision.CompareTag("Player")) {
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null) {
                player.OnLadder = true;
                Debug.Log("Player entered ladder");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision) {
        if (collision.CompareTag("Player")) {
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null) {
                player.OnLadder = false;
                Debug.Log("Player exited ladder");
            }
        }
    }
}
