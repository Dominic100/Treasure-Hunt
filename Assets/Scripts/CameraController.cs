using UnityEngine;

public class CameraController : MonoBehaviour {
    [Header("Target Settings")]
    [SerializeField] private Transform target;  // Player transform
    [SerializeField] private Vector3 offset = new Vector3(0, 1, -10);

    [Header("Movement Settings")]
    [SerializeField] private float smoothSpeed = 0.125f;  // Lower = smoother
    [SerializeField] private Vector2 minPosition;
    [SerializeField] private Vector2 maxPosition;

    private void Start() {
        // If no target is set, try to find the player
        if (target == null) {
            target = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (target == null) {
                Debug.LogError("No player found for camera to follow!");
                enabled = false;
                return;
            }
        }

        // Set initial position
        transform.position = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            offset.z
        );
    }

    private void FixedUpdate() {
        if (target == null) return;

        // Calculate desired position
        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            offset.z
        );

        // Smoothly move camera
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Clamp to bounds
        smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minPosition.x, maxPosition.x);
        smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minPosition.y, maxPosition.y);

        // Update camera position
        transform.position = smoothedPosition;

        // Debug log to check if camera is updating
        Debug.Log($"Camera Position: {transform.position}, Target Position: {target.position}");
    }

    // Optional: Visualize camera bounds in editor
    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3((minPosition.x + maxPosition.x) / 2, (minPosition.y + maxPosition.y) / 2, 0),
            new Vector3(maxPosition.x - minPosition.x, maxPosition.y - minPosition.y, 0)
        );
    }

    // Public method to set new bounds at runtime if needed
    public void SetBounds(Vector2 min, Vector2 max) {
        minPosition = min;
        maxPosition = max;
    }
}
