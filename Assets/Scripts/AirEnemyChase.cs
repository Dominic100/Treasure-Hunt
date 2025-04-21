using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class AirEnemyChase : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Movement Settings")]
    [SerializeField] private float chaseSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 2.0f;
    [SerializeField] private float minDistanceToPlayer = 1.5f;

    [Header("Path Randomization")]
    [SerializeField] private float swayMagnitude = 0.8f;
    [SerializeField] private float swayFrequency = 1.2f;
    [SerializeField] private float directionChangeSmoothing = 0.5f;
    [SerializeField] private float randomDirectionChangeInterval = 3f;
    [SerializeField] private float randomDirectionChangeMagnitude = 0.3f;

    [Header("Visual Effects")]
    [SerializeField] private bool faceMovementDirection = true;
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobMagnitude = 0.1f;

    [Header("Gameplay")]
    [SerializeField] private int health = 1;
    [SerializeField] private float boundaryCheckDistance = 500f; // Distance beyond which enemy self-destructs
    [SerializeField] private float returnToBoundaryForce = 0.1f; // Force pulling enemy back to spawn point

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Events
    [HideInInspector] public UnityEvent onDestroyed = new UnityEvent();

    // Private variables
    private Vector2 currentVelocity;
    private Vector2 targetDirection;
    private Vector2 smoothedDirection;
    private float swayTimer;
    private float nextDirectionChangeTime;
    private float initialY;
    private float bobTimer;
    private Vector2 randomOffset;
    private SpriteRenderer spriteRenderer;
    private Vector3 spawnPosition;
    private bool isInitialized = false;
    private bool isBeingDestroyed = false;

    void Start() {
        // Initialize components
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Initialize movement variables
        currentVelocity = Vector2.zero;
        targetDirection = Vector2.zero;
        smoothedDirection = Vector2.zero;
        initialY = transform.position.y;
        randomOffset = Vector2.zero;
        spawnPosition = transform.position;

        // Check for required references
        if (player == null) {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) {
                Debug.LogError("Player reference not set and could not be found with tag!");
                enabled = false;
                return;
            }
        }

        // Ignore platform collisions - this assumes the platforms are on a specific layer
        if (gameObject.GetComponent<Collider2D>() != null) {
            Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Platform"), true);
        }

        // Initialize timers
        swayTimer = Random.value * 10f; // Randomize initial sway phase
        bobTimer = Random.value * 10f;  // Randomize initial bob phase
        nextDirectionChangeTime = Time.time + Random.Range(1f, randomDirectionChangeInterval);

        // Start pathfinding coroutine
        StartCoroutine(UpdateRandomOffset());

        isInitialized = true;
    }

    void Update() {
        if (!isInitialized) return;

        // Update sway and bob timers
        swayTimer += Time.deltaTime * swayFrequency;
        bobTimer += Time.deltaTime * bobFrequency;

        // Always chase the player regardless of distance
        ChasePlayer();

        // Apply visual effects
        ApplyVisualEffects();

        // Check distance from spawn and apply return force if needed
        float distanceFromSpawn = Vector3.Distance(transform.position, spawnPosition);
        
        // Debug log distance when getting far
        if (debugMode && distanceFromSpawn > boundaryCheckDistance * 0.7f) {
            Debug.LogWarning($"Enemy {gameObject.name} getting far from spawn: {distanceFromSpawn}/{boundaryCheckDistance}");
        }
    }

    public float ChaseSpeed {
        get { return chaseSpeed; }
        set { chaseSpeed = Mathf.Max(0.5f, value); } // Ensure we don't set it below 0.5
    }
    
    public float RotationSpeed {
        get { return rotationSpeed; }
        set { rotationSpeed = Mathf.Max(0.5f, value); } // Ensure we don't set it below 0.5
    }

    void ChasePlayer() {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        float distanceFromSpawn = Vector2.Distance(transform.position, spawnPosition);

        // Get direction to player
        Vector2 directionToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;

        // Apply random offset and sway to direction
        Vector2 swayOffset = new Vector2(
            Mathf.Sin(swayTimer) * swayMagnitude,
            Mathf.Cos(swayTimer * 0.6f) * swayMagnitude * 0.5f
        );

        // Calculate target direction with offsets
        targetDirection = directionToPlayer + swayOffset + randomOffset;
        targetDirection.Normalize();

        // If we're too close to the player, back off slightly
        if (distanceToPlayer < minDistanceToPlayer) {
            targetDirection = -directionToPlayer * 0.5f + swayOffset * 0.8f;
            targetDirection.Normalize();
        }

        // If we're getting too far from spawn, add a return force
        if (distanceFromSpawn > boundaryCheckDistance * 0.7f) {
            Vector2 returnDirection = ((Vector2)spawnPosition - (Vector2)transform.position).normalized;
            float returnForce = Mathf.InverseLerp(boundaryCheckDistance * 0.7f, boundaryCheckDistance * 0.9f, distanceFromSpawn) * returnToBoundaryForce;
            
            // Blend between current direction and return direction
            targetDirection = Vector2.Lerp(targetDirection, returnDirection, returnForce);
            targetDirection.Normalize();
            
            if (debugMode) {
                Debug.Log($"Applying return force of {returnForce} at distance {distanceFromSpawn}");
                Debug.DrawRay(transform.position, returnDirection * 3, Color.yellow);
            }
        }

        // Smooth direction changes (this creates a trailing/following effect)
        smoothedDirection = Vector2.Lerp(smoothedDirection, targetDirection, directionChangeSmoothing * Time.deltaTime);

        // Calculate new position
        Vector3 newPosition = transform.position + (Vector3)(smoothedDirection * chaseSpeed * Time.deltaTime);
        
        // Check if new position would be too far
        float newDistanceFromSpawn = Vector3.Distance(newPosition, spawnPosition);
        if (newDistanceFromSpawn > boundaryCheckDistance * 0.95f) {
            // Hard limit at boundary edge - don't let it go further
            Vector2 directionFromSpawn = ((Vector2)newPosition - (Vector2)spawnPosition).normalized;
            newPosition = spawnPosition + (Vector3)(directionFromSpawn * boundaryCheckDistance * 0.95f);
            
            if (debugMode) {
                Debug.LogWarning("Enemy hit boundary limit - position clamped");
            }
        }
        
        // Apply movement
        transform.position = newPosition;
    }

    void ApplyVisualEffects() {
        // Apply bobbing effect
        if (bobMagnitude > 0) {
            float bobOffset = Mathf.Sin(bobTimer) * bobMagnitude;
            transform.position = new Vector3(
                transform.position.x,
                transform.position.y + bobOffset * Time.deltaTime,
                transform.position.z);
        }

        // Face the movement direction if enabled
        if (faceMovementDirection && smoothedDirection.x != 0) {
            bool facingRight = smoothedDirection.x > 0;
            spriteRenderer.flipX = !facingRight;
        }

        // Visualize movement direction
        if (debugMode) {
            Debug.DrawRay(transform.position, smoothedDirection * 2, Color.red);
            Debug.DrawRay(transform.position, targetDirection * 2, Color.blue);
        }
    }

    // Coroutine to update random direction change
    private IEnumerator UpdateRandomOffset() {
        while (true) {
            if (Time.time >= nextDirectionChangeTime) {
                // Generate new random offset
                randomOffset = new Vector2(
                    Random.Range(-randomDirectionChangeMagnitude, randomDirectionChangeMagnitude),
                    Random.Range(-randomDirectionChangeMagnitude, randomDirectionChangeMagnitude)
                );

                // Schedule next change
                nextDirectionChangeTime = Time.time + Random.Range(1f, randomDirectionChangeInterval);

                if (debugMode) Debug.Log("Direction changed with offset: " + randomOffset);
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    void OnDrawGizmosSelected() {
        // Draw minimum distance
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, minDistanceToPlayer);

        // Draw boundary distance (if in play mode)
        if (Application.isPlaying) {
            Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
            Gizmos.DrawWireSphere(spawnPosition, boundaryCheckDistance);
        }
    }

    public void SetPlayer(Transform playerTransform) {
        this.player = playerTransform;
    }

    public void SetProperties(float newChaseSpeed, float newRotationSpeed, float randomizationFactor) {
        // Validate and apply speed settings
        chaseSpeed = Mathf.Max(0.5f, newChaseSpeed);
        rotationSpeed = Mathf.Max(0.5f, newRotationSpeed);

        // Validate and apply randomization factor (clamp between 0.2 and 2.0)
        randomizationFactor = Mathf.Clamp(randomizationFactor, 0.2f, 2.0f);

        // Apply randomization factor to sway magnitude and random direction change
        swayMagnitude = 0.8f * randomizationFactor;
        randomDirectionChangeMagnitude = 0.3f * randomizationFactor;
        directionChangeSmoothing = 0.5f * Mathf.Lerp(0.5f, 1.5f, 1 - randomizationFactor);

        if (debugMode) {
            Debug.Log($"Updated properties - Chase Speed: {chaseSpeed}, Rotation Speed: {rotationSpeed}, " +
                      $"Randomization: {randomizationFactor} (Sway: {swayMagnitude}, Direction Change: {randomDirectionChangeMagnitude})");
        }
    }

    // Called when the enemy takes damage
    public void TakeDamage(int amount = 1) {
        health -= amount;
        if (health <= 0 && !isBeingDestroyed) {
            isBeingDestroyed = true;
            // Trigger any death effects here
            onDestroyed.Invoke();
            Destroy(gameObject);
        }
    }

    // Handle collision with player attacks or other dangerous objects
    private void OnTriggerEnter2D(Collider2D other) {
        // This assumes you have a projectile or attack with a specific tag
        if (other.CompareTag("PlayerAttack")) {
            TakeDamage();
        }
    }

    private void OnDestroy() {
        // Only invoke if this isn't being destroyed deliberately
        if (!isBeingDestroyed && Application.isPlaying) {
            Debug.LogWarning($"Enemy {gameObject.name} destroyed unexpectedly");
            onDestroyed.Invoke();
        }
    }
}