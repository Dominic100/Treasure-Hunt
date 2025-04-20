using UnityEngine;

public class CameraController : MonoBehaviour {
    [Header("Target Settings")]
    [SerializeField] private Transform target; // The player to follow
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10); // Default offset for 2D games
    [SerializeField] private LayerMask playerLayer; // Layer for the player

    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 0.125f; // Lower = smoother but slower camera
    [SerializeField] private bool followX = true; // Follow on X axis
    [SerializeField] private bool followY = true; // Follow on Y axis

    [Header("Boundaries")]
    [SerializeField] private bool useBoundaries = false;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 10f;
    [SerializeField] private float minY = -10f;
    [SerializeField] private float maxY = 10f;

    private Vector3 desiredPosition;
    private Vector3 smoothedPosition;

    private void Start() {
        // If target isn't assigned, try to find the player automatically by layer
        if (target == null) {
            FindPlayerByLayer();
            if (target == null) {
                Debug.LogWarning("CameraController: No target assigned and no player found on the specified layer");
            }
            else {
                Debug.Log("CameraController: Player found automatically");
            }
        }
    }

    private void FixedUpdate() {
        if (target == null)
            return;

        // Calculating the position the camera should move toward
        desiredPosition = target.position + offset;

        // Maintain current position for axes we don't want to follow
        if (!followX) desiredPosition.x = transform.position.x;
        if (!followY) desiredPosition.y = transform.position.y;

        // Apply boundaries if enabled
        if (useBoundaries) {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        // Smoothly move the camera towards the target position
        smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Always keep the same Z position (camera depth)
        smoothedPosition.z = offset.z;

        // Update camera position
        transform.position = smoothedPosition;
    }

    private void FindPlayerByLayer() {
        // Find all objects in the scene and check their layer
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects) {
            if (((1 << obj.layer) & playerLayer) != 0) { // Check if the object's layer matches the player layer
                target = obj.transform;
                Debug.Log($"CameraController: Player found - {target.name}");
                break;
            }
        }
    }

    // Optional: Visualize camera boundaries in the editor
    private void OnDrawGizmosSelected() {
        if (useBoundaries) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3((minX + maxX) / 2, (minY + maxY) / 2, 0),
                new Vector3(maxX - minX, maxY - minY, 0)
            );
        }
    }
}
