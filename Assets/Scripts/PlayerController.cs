using UnityEngine;

public class PlayerController : MonoBehaviour {
    private Rigidbody2D rb;
    public bool OnLadder { get; set; }

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float climbSpeed = 5f;
    public float jumpForce = 10f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    private float verticalInput;
    private float horizontalInput;
    private bool wasOnLadder;

    void Start() {
        rb = GetComponent<Rigidbody2D>();

        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        OnLadder = false;
    }

    void Update() {
        // Get input
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // Debug input values
        Debug.Log($"Horizontal: {horizontalInput}, Vertical: {verticalInput}");

        // Handle jumping
        if (Input.GetButtonDown("Jump") && isGrounded && !OnLadder) {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
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
    }

    void FixedUpdate() {
        // Check if player is grounded
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (OnLadder) {
            // Ladder movement
            Vector2 climbVelocity = new Vector2(horizontalInput * moveSpeed, verticalInput * climbSpeed);
            rb.linearVelocity = climbVelocity;
            Debug.Log($"Ladder Movement: {climbVelocity}");
        }
        else {
            // Normal movement
            Vector2 movement = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
            rb.linearVelocity = movement;
            Debug.Log($"Normal Movement: {movement}");
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
