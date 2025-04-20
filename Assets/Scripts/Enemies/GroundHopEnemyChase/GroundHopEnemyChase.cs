using System.Collections;
using UnityEngine;

public class GroundHopEnemyChase : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask groundLayer;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float baseHopHeight = 5f;
    [SerializeField] private float additionalHeightAbovePlayer = 5f; // Height to add above player
    [SerializeField] private float horizontalHopDistance = 4f;
    [SerializeField] private float hopDuration = 3f; // Increased duration for slower hops

    [Header("Hop Timing")]
    [SerializeField] private float ascendPortion = 0.4f; // Portion of time spent ascending (0-1)
    [SerializeField] private float descendPortion = 0.6f; // Portion of time spent descending (0-1)
    [SerializeField] private AnimationCurve hopCurve; // Custom curve for more control over hop motion

    [Header("Slope Handling")]
    [SerializeField] private int groundCheckRays = 5; // Multiple rays for better slope detection
    [SerializeField] private float groundCheckDistance = 3f; // Increased to detect ground below slopes
    [SerializeField] private float slopeRaycastSpread = 0.5f; // Width of raycast pattern

    [Header("Hop Behavior")]
    [SerializeField] private float minTimeBetweenHops = 2f;
    [SerializeField] private float maxTimeBetweenHops = 5f;
    [SerializeField] private float hopAnticipationTime = 0.5f;
    [SerializeField] private GameObject hopWarningEffect;

    [Header("Visual Effects")]
    [SerializeField] private Color anticipationColor = Color.red;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private GameObject spawnPoint; // Optional spawn point reference

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private UnityEngine.Events.UnityEvent onDestroyed = new UnityEngine.Events.UnityEvent();

    // Private variables
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D enemyCollider;
    private bool isGrounded;
    private bool isHopping;
    private float nextHopTime;
    private float lastGroundedY;
    private int facingDirection = 1; // 1 = right, -1 = left
    private Vector2 hopDestination;
    private bool isBeingDestroyed = false;
    private Vector3 initialPosition;

    void OnValidate() {
        // Create default hop curve if not set
        if (hopCurve.keys.Length == 0) {
            // Create a custom curve that rises quickly and falls slowly
            Keyframe[] keys = new Keyframe[3];
            keys[0] = new Keyframe(0, 0, 0, 2f); // Start
            keys[1] = new Keyframe(ascendPortion, 1, 0, 0); // Peak
            keys[2] = new Keyframe(1, 0, -2f, 0); // End

            hopCurve = new AnimationCurve(keys);
        }
    }

    void Start() {
        // Get components
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();
        
        // Save initial position for reset
        initialPosition = transform.position;

        // Component validation
        if (rb == null) {
            Debug.LogError("Rigidbody2D missing on " + gameObject.name);
            enabled = false;
            return;
        }

        if (enemyCollider == null) {
            Debug.LogError("Collider2D missing on " + gameObject.name);
            enabled = false;
            return;
        }

        // Save initial color
        if (spriteRenderer != null) {
            normalColor = spriteRenderer.color;
        }

        // Configure rigidbody for better physics
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.gravityScale = 3f; // Increase gravity for better ground detection

        // Initialize hop curve if needed
        OnValidate();

        // Initialize hop timer
        ResetHopTimer();

        // Save initial Y position as ground level
        lastGroundedY = transform.position.y;

        // Additional validations
        if (groundLayer.value == 0) {
            Debug.LogError("Ground layer not set on " + gameObject.name);
        }

        if (player == null) {
            Debug.LogError("Player reference not set on " + gameObject.name);
        }
    }

    public void SetPlayer(Transform playerTransform) {
        this.player = playerTransform;
    }

    public void SetProperties(float newMoveSpeed, float newHopDuration, float newMinTimeBetweenHops, float newMaxTimeBetweenHops) {
        // Validate and apply move speed
        moveSpeed = Mathf.Max(1f, newMoveSpeed);

        // Validate and apply hop duration
        hopDuration = Mathf.Clamp(newHopDuration, 0.5f, 5f);

        // Validate and apply hop timing
        minTimeBetweenHops = Mathf.Max(0.5f, newMinTimeBetweenHops);
        maxTimeBetweenHops = Mathf.Max(minTimeBetweenHops + 0.5f, newMaxTimeBetweenHops);

        // Log changes if debug mode is enabled
        if (debugMode) {
            Debug.Log($"Properties updated: Speed={moveSpeed}, " +
                     $"HopDuration={hopDuration}, TimeBetweenHops=[{minTimeBetweenHops}, {maxTimeBetweenHops}]");
        }

        // Reset hop timer with new values
        if (gameObject.activeInHierarchy) {
            ResetHopTimer();
        }
    }

    void Update() {
        // Check grounded state
        CheckGrounded();

        // Skip if player is missing
        if (player == null) return;

        // Update facing direction
        UpdateFacingDirection();

        // Try to hop if conditions are met
        if (isGrounded && !isHopping && Time.time >= nextHopTime) {
            if (debugMode) Debug.Log("Starting hop sequence");
            StartCoroutine(HopTowardsPlayer());
        }

        // For immediate testing (can be removed in final version)
        if (Input.GetKeyDown(KeyCode.H)) {
            ForceHop();
        }
        
        // Safety check - if enemy has wandered too far from start/spawn, jump back
        Vector3 referencePoint = spawnPoint != null ? spawnPoint.transform.position : initialPosition;
        float distanceFromOrigin = Vector3.Distance(transform.position, referencePoint);
        
        if (distanceFromOrigin > 500f) {
            Debug.LogWarning($"Enemy too far from origin ({distanceFromOrigin} units) - jumping back");
            ReturnToSpawn();
        }
    }
    
    private void ReturnToSpawn() {
        // Stop current hop if happening
        StopAllCoroutines();
        isHopping = false;
        
        // Reset position
        Vector3 targetPosition = spawnPoint != null ? spawnPoint.transform.position : initialPosition;
        transform.position = targetPosition;
        
        // Reset physics
        if (rb != null) {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // Reset visual state
        if (spriteRenderer != null) {
            spriteRenderer.color = normalColor;
        }
        
        if (hopWarningEffect != null) {
            hopWarningEffect.SetActive(false);
        }
        
        // Reset hop timer
        ResetHopTimer();
        
        // Flash to indicate teleport
        StartCoroutine(FlashColor());
    }
    
    private IEnumerator FlashColor() {
        if (spriteRenderer == null) yield break;
        
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.cyan;
        yield return new WaitForSeconds(0.2f);
        spriteRenderer.color = originalColor;
    }

    void FixedUpdate() {
        // Only move horizontally if grounded and not hopping
        if (isGrounded && !isHopping) {
            rb.linearVelocity = new Vector2(moveSpeed * facingDirection, rb.linearVelocity.y);
        }
    }

    void CheckGrounded() {
        // Use multiple raycasts for better ground detection on slopes
        isGrounded = false;
        bool wasGrounded = isGrounded;

        Vector2 center = new Vector2(transform.position.x, transform.position.y - (enemyCollider.bounds.extents.y - 0.1f));

        // Cast multiple rays in a fan pattern
        for (int i = 0; i < groundCheckRays; i++) {
            float xOffset = ((i / (float)(groundCheckRays - 1)) - 0.5f) * slopeRaycastSpread;
            Vector2 rayStart = new Vector2(center.x + xOffset, center.y);

            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, groundCheckDistance, groundLayer);

            if (hit.collider != null) {
                isGrounded = true;

                // Save the ground Y position when first grounded
                if (!wasGrounded) {
                    lastGroundedY = transform.position.y;
                    if (debugMode) Debug.Log("Grounded at Y: " + lastGroundedY);
                }

                // Debug visualization
                if (debugMode) {
                    Debug.DrawRay(rayStart, Vector2.down * groundCheckDistance, Color.green);
                }

                break;
            }
            else if (debugMode) {
                Debug.DrawRay(rayStart, Vector2.down * groundCheckDistance, Color.red);
            }
        }
    }

    void UpdateFacingDirection() {
        if (player.position.x > transform.position.x && facingDirection < 0 ||
            player.position.x < transform.position.x && facingDirection > 0) {
            Flip();
        }
    }

    private void OnDestroy() {
        // Only invoke if this isn't being destroyed during scene cleanup
        if (!isBeingDestroyed && Application.isPlaying && gameObject.scene.isLoaded) {
            Debug.LogWarning($"GroundHopEnemy {gameObject.name} destroyed unexpectedly - this should not happen");
            onDestroyed?.Invoke();
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision) {
        // If hit by player or projectile, just force a hop instead of taking damage
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("PlayerAttack")) {
            if (!isHopping && isGrounded) {
                Debug.Log($"Collision with {collision.gameObject.name} - jumping away instead of being destroyed");
                ForceHop();
            }
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other) {
        // Same as collision but for triggers
        if (other.CompareTag("Player") || other.CompareTag("PlayerAttack")) {
            if (!isHopping && isGrounded) {
                Debug.Log($"Trigger with {other.gameObject.name} - jumping away instead of being destroyed");
                ForceHop();
            }
        }
    }
    
    // Add this method to handle any damage instead of destruction
    public void TakeDamage(int amount = 1) {
        // Instead of taking damage and being destroyed, just react by jumping
        if (!isHopping && isGrounded) {
            Debug.Log("Enemy hit but not destroyed - forcing hop instead");
            ForceHop();
        }
    }

    void Flip() {
        facingDirection *= -1;
        Vector3 currentScale = transform.localScale;
        currentScale.x *= -1;
        transform.localScale = currentScale;
    }

    IEnumerator HopTowardsPlayer() {
        if (isHopping) yield break;

        isHopping = true;

        // Show anticipation
        if (spriteRenderer != null) {
            spriteRenderer.color = anticipationColor;
        }

        if (hopWarningEffect != null) {
            hopWarningEffect.SetActive(true);
        }

        // Wait for anticipation time
        yield return new WaitForSeconds(hopAnticipationTime);

        // Reset visual effects
        if (spriteRenderer != null) {
            spriteRenderer.color = normalColor;
        }

        if (hopWarningEffect != null) {
            hopWarningEffect.SetActive(false);
        }

        // Determine target position that respects slopes
        Vector2 startPos = transform.position;
        CalculateHopDestination();

        // Calculate height adjustment
        float playerElevation = player.position.y - lastGroundedY;
        float totalHeight = CalculateHopHeight(playerElevation);

        if (debugMode) {
            Debug.Log($"Player elevation: {playerElevation}, Total hop height: {totalHeight}");
        }

        // Temporarily disable gravity and physics
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        // Start the hop
        float timeElapsed = 0;
        Vector2 previousPosition = startPos;

        while (timeElapsed < hopDuration) {
            // Calculate normalized time
            float normalizedTime = timeElapsed / hopDuration;

            // Get height factor from curve for more control over the hop motion
            float heightFactor = hopCurve.Evaluate(normalizedTime);

            // Calculate horizontal position (slowed down in the middle for "hang time")
            float horizontalT = GetHorizontalProgress(normalizedTime);
            float xPos = Mathf.Lerp(startPos.x, hopDestination.x, horizontalT);

            // Calculate vertical position with enhanced height
            float yPos = Mathf.Lerp(startPos.y, hopDestination.y, normalizedTime) + (heightFactor * totalHeight);

            // Calculate intended new position
            Vector2 newPosition = new Vector2(xPos, yPos);

            // Check if the move would hit a collider
            RaycastHit2D hit = Physics2D.Linecast(previousPosition, newPosition, groundLayer);

            if (hit.collider != null) {
                // We'd hit something - adjust course to slide along surface
                if (debugMode) Debug.Log($"Hit obstacle during hop: {hit.collider.name} at {hit.point}");

                // Move to the hit point plus a small offset in the normal direction
                newPosition = hit.point + hit.normal * 0.1f;

                // Try to find ground below the current position for landing
                RaycastHit2D groundHit = Physics2D.Raycast(newPosition, Vector2.down, groundCheckDistance, groundLayer);

                if (groundHit.collider != null) {
                    // Adjust hop to land on this ground point
                    hopDestination = new Vector2(
                        hopDestination.x,
                        groundHit.point.y + 0.1f
                    );
                }
            }

            // Update position
            transform.position = newPosition;
            previousPosition = newPosition;

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Final position - ensure we're at the exact target
        transform.position = new Vector3(hopDestination.x, hopDestination.y, transform.position.z);

        // Restore gravity
        rb.gravityScale = originalGravity;

        // Reset hop timer
        ResetHopTimer();

        // Small delay to ensure proper grounding
        yield return new WaitForSeconds(0.1f);

        isHopping = false;

        if (debugMode) {
            Debug.Log("Hop complete, next hop in " + (nextHopTime - Time.time) + " seconds");
        }
    }

    // Calculate horizontal progress to create "hang time" at the peak
    float GetHorizontalProgress(float t) {
        // Use a custom curve for horizontal movement that slows down in the middle
        // This creates the illusion of "hanging" at the peak of the jump
        if (t < ascendPortion) {
            // During ascent: move horizontally a bit slower
            return Mathf.Lerp(0, 0.4f, t / ascendPortion);
        }
        else {
            // During descent: accelerate horizontal movement
            float descentT = (t - ascendPortion) / (1 - ascendPortion);
            return Mathf.Lerp(0.4f, 1f, descentT);
        }
    }

    // Calculate the total hop height
    float CalculateHopHeight(float playerElevation) {
        // Base hop height + additional height to reach above player
        float minRequiredHeight = playerElevation + additionalHeightAbovePlayer;

        // Use the larger of baseHopHeight or the height needed to clear player by additionalHeightAbovePlayer
        return Mathf.Max(baseHopHeight, minRequiredHeight);
    }

    void CalculateHopDestination() {
        float directionToPlayer = Mathf.Sign(player.position.x - transform.position.x);
        float targetX = transform.position.x + (directionToPlayer * horizontalHopDistance);

        // Start high to ensure we find ground
        Vector2 rayStart = new Vector2(targetX, transform.position.y + 5f);

        // Cast multiple rays in a pattern to find the best landing spot
        float bestDistance = float.MaxValue;
        Vector2 bestLandingSpot = new Vector2(targetX, lastGroundedY);
        bool foundGround = false;

        // Try rays at different angles to find a landing spot
        for (int i = 0; i < 5; i++) {
            float rayAngle = -90f + (i * 10f - 20f); // -90° (down), then -110°, -100°, -80°, -70°
            Vector2 rayDirection = Quaternion.Euler(0, 0, rayAngle) * Vector2.right;

            RaycastHit2D hit = Physics2D.Raycast(rayStart, rayDirection, 10f, groundLayer);

            if (hit.collider != null) {
                float distToHit = Vector2.Distance(transform.position, hit.point);

                // If this is a better (closer to ideal distance) landing spot
                if (Mathf.Abs(distToHit - horizontalHopDistance) < bestDistance) {
                    bestDistance = Mathf.Abs(distToHit - horizontalHopDistance);
                    bestLandingSpot = hit.point + Vector2.up * 0.1f; // Slightly above hit point
                    foundGround = true;

                    if (debugMode) {
                        Debug.DrawLine(rayStart, hit.point, Color.yellow, 1.0f);
                    }
                }
            }
        }

        // If we didn't find ground, cast a ray straight down from the target X
        if (!foundGround) {
            RaycastHit2D downHit = Physics2D.Raycast(
                rayStart,
                Vector2.down,
                20f,
                groundLayer
            );

            if (downHit.collider != null) {
                bestLandingSpot = downHit.point + Vector2.up * 0.1f;
                foundGround = true;

                if (debugMode) {
                    Debug.DrawLine(rayStart, downHit.point, Color.green, 1.0f);
                }
            }
        }

        // If we still didn't find ground, use the last grounded Y as fallback
        if (!foundGround) {
            bestLandingSpot = new Vector2(targetX, lastGroundedY);
        }

        hopDestination = bestLandingSpot;
    }

    void ResetHopTimer() {
        nextHopTime = Time.time + Random.Range(minTimeBetweenHops, maxTimeBetweenHops);
    }

    public void ForceHop() {
        if (!isHopping && isGrounded) {
            if (debugMode) Debug.Log("Force hop triggered");
            StartCoroutine(HopTowardsPlayer());
        }
    }

    public void ConfigureCollisionLayers(int groundLayerNum, int platformLayerNum) {
        Physics2D.IgnoreLayerCollision(gameObject.layer, platformLayerNum, true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, groundLayerNum, false);

        if (debugMode) {
            Debug.Log($"Configured collision layers - ground:{groundLayerNum}, platform:{platformLayerNum}");
        }
    }

    // Visualize ground check and hop trajectory in the editor
    private void OnDrawGizmos() {
        if (!Application.isPlaying) return;

        // Draw ground check rays
        if (enemyCollider != null) {
            Vector2 center = new Vector2(transform.position.x, transform.position.y - (enemyCollider.bounds.extents.y - 0.1f));

            for (int i = 0; i < groundCheckRays; i++) {
                float xOffset = ((i / (float)(groundCheckRays - 1)) - 0.5f) * slopeRaycastSpread;
                Vector2 rayStart = new Vector2(center.x + xOffset, center.y);

                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawLine(rayStart, rayStart + Vector2.down * groundCheckDistance);
            }
        }

        // Draw hop destination and trajectory
        if (debugMode && player != null) {
            float directionToPlayer = Mathf.Sign(player.position.x - transform.position.x);
            float targetX = transform.position.x + (directionToPlayer * horizontalHopDistance);
            Vector3 hopTarget = new Vector3(targetX, lastGroundedY, transform.position.z);

            // Show estimated landing position
            Gizmos.color = new Color(1f, 0.5f, 0, 0.3f);
            Gizmos.DrawWireSphere(hopTarget, 0.5f);

            // Show height above player
            if (player != null) {
                Gizmos.color = Color.cyan;
                Vector3 playerTopPos = player.position + Vector3.up * additionalHeightAbovePlayer;
                Gizmos.DrawLine(player.position, playerTopPos);
                Gizmos.DrawWireSphere(playerTopPos, 0.2f);
            }

            // Draw trajectory curve when not hopping
            if (!isHopping) {
                float playerElevation = player.position.y - lastGroundedY;
                float totalHeight = CalculateHopHeight(playerElevation);

                Gizmos.color = new Color(1f, 1f, 0, 0.5f);
                Vector3 prev = transform.position;

                // Draw estimated trajectory path
                int segments = 20;
                for (int i = 1; i <= segments; i++) {
                    float t = i / (float)segments;

                    // Calculate horizontal position with hang time
                    float horizontalT = GetHorizontalProgress(t);
                    float x = Mathf.Lerp(transform.position.x, hopTarget.x, horizontalT);

                    // Calculate vertical position with curve
                    float heightFactor = hopCurve.Evaluate(t);
                    float y = Mathf.Lerp(transform.position.y, hopTarget.y, t) + (heightFactor * totalHeight);

                    Vector3 pos = new Vector3(x, y, transform.position.z);
                    Gizmos.DrawLine(prev, pos);
                    prev = pos;
                }
            }
        }
    }
}