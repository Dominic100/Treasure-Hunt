using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour {
    private Rigidbody2D rb;
    public bool OnLadder { get; set; }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float climbSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float trapSlowMultiplier = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;

    [Header("Tilemap References")]
    [SerializeField] private Tilemap climbTilemap;
    [SerializeField] private Tilemap trapTilemap;

    [Header("Health Settings")]
    [SerializeField] private float trapDamagePerSecond = 10f;
    private float lastDamageTime;

    private float verticalInput;
    private float horizontalInput;
    private bool wasOnLadder;
    private bool isOnTrap;
    private int jumpCount = 0;
    [SerializeField] private int maxAdditionalJumps = 1;

    private void Start() {
        rb = GetComponent<Rigidbody2D>();

        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        OnLadder = false;
    }

    private void Update() {
        // Get input
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // Handle jumping
        if (Input.GetButtonDown("Jump") && !isOnTrap && jumpCount < maxAdditionalJumps && !OnLadder) {
            Jump();
        }

        // Handle ladder state changes
        if (OnLadder != wasOnLadder) {
            if (OnLadder) {
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
            }
            else {
                rb.gravityScale = 3f;
            }
            wasOnLadder = OnLadder;
        }

        // Check for traps
        CheckTrap();
        
        // Check for damage
        // CheckForDamage();

        // Reset jump count when grounded
        if (isGrounded) {
            jumpCount = 0;
        }

        // Check if player is on a ladder
        CheckLadder();
    }

    // Add this property to your PlayerMovement class
    public float MoveSpeed 
    { 
        get { return moveSpeed; } 
        set { moveSpeed = value; }
    }

    private void FixedUpdate() {
        // Check if player is grounded
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (OnLadder) {
            // Ladder movement
            Vector2 climbVelocity = new Vector2(horizontalInput * moveSpeed, verticalInput * climbSpeed);
            rb.linearVelocity = climbVelocity;
        }
        else {
            // Normal movement
            float currentSpeed = isOnTrap ? moveSpeed * trapSlowMultiplier : moveSpeed;
            Vector2 movement = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);
            rb.linearVelocity = movement;
        }
    }

    private void Jump() {
        jumpCount++;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); // Reset vertical velocity
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    private void CheckLadder() {
        // Get the player's collider bounds
        Collider2D playerCollider = GetComponent<Collider2D>();
        Bounds bounds = playerCollider.bounds;

        // Check multiple points around the player for ladder tiles
        Vector3Int bottomLeft = climbTilemap.WorldToCell(bounds.min);
        Vector3Int bottomRight = climbTilemap.WorldToCell(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
        Vector3Int topLeft = climbTilemap.WorldToCell(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
        Vector3Int topRight = climbTilemap.WorldToCell(bounds.max);

        // If any of these points are on a ladder tile, set OnLadder to true
        OnLadder = climbTilemap.HasTile(bottomLeft) || climbTilemap.HasTile(bottomRight) ||
                   climbTilemap.HasTile(topLeft) || climbTilemap.HasTile(topRight);

        // Adjust gravity based on ladder state
        if (OnLadder) {
            rb.gravityScale = 0f;
        }
        else {
            rb.gravityScale = 3f;
        }
    }

    private void CheckTrap() {
        Vector3Int cellPosition = trapTilemap.WorldToCell(transform.position);
        isOnTrap = trapTilemap.HasTile(cellPosition);

        if (isOnTrap) {
            Debug.Log("Player is on a trap");
        }
    }

    // Optional: Visualize ground check in Scene view
    private void OnDrawGizmos() {
        if (groundCheck != null) {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}