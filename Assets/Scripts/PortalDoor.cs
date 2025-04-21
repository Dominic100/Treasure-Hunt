using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PortalDoor : MonoBehaviour
{
    [Header("Door Properties")]
    [SerializeField] private int doorID = 0; // Unique ID for each door pair
    [SerializeField] private bool isEntryDoor = true; // true = open door, false = exit/closed door
    [SerializeField] private PortalDoor pairedDoor; // Reference to the paired door
    
    [Header("Teleportation Settings")]
    [SerializeField] private float teleportDelay = 2f; // Delay before teleporting
    [SerializeField] private float exitOffset = 1f; // How far in front of exit door to place player
    [SerializeField] private bool preserveHorizontalVelocity = false; // Keep horizontal momentum after teleport
    
    [Header("Effects")]
    [SerializeField] private bool showTeleportIndicator = true;
    [SerializeField] private GameObject teleportIndicatorPrefab;
    [SerializeField] private AudioClip teleportInitiateSound;
    [SerializeField] private AudioClip teleportCancelSound;
    [SerializeField] private AudioClip teleportCompleteSound;
    
    // References
    private Tilemap doorTilemap;
    private BoxCollider2D doorCollider;
    private AudioSource audioSource;
    
    // State tracking
    private Transform playerTransform;
    private Rigidbody2D playerRigidbody;
    private Coroutine teleportCoroutine;
    private GameObject activeIndicator;
    
    private void Awake()
    {
        // Get references
        doorCollider = GetComponent<BoxCollider2D>();
        audioSource = GetComponent<AudioSource>();
        
        // Create audio source if needed
        if (audioSource == null && (teleportInitiateSound != null || 
            teleportCancelSound != null || teleportCompleteSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        // Ensure the collider is a trigger
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }
        else
        {
            Debug.LogWarning($"Door {gameObject.name} has no collider!");
        }
    }
    
    private void Start()
    {
        // Validate the paired door
        if (pairedDoor == null)
        {
            Debug.LogError($"Door {gameObject.name} has no paired door assigned!");
        }
        else if (pairedDoor.doorID != doorID)
        {
            Debug.LogWarning($"Door {gameObject.name} is paired with a door that has a different ID!");
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only process if this is an entry door
        if (!isEntryDoor) return;
        
        // Check if it's the player
        if (other.CompareTag("Player"))
        {
            playerTransform = other.transform;
            playerRigidbody = other.GetComponent<Rigidbody2D>();
            
            // Start teleport sequence
            if (teleportCoroutine == null)
            {
                teleportCoroutine = StartCoroutine(TeleportSequence());
            }
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        // Only process if this is an entry door
        if (!isEntryDoor) return;
        
        // Check if it's the player
        if (other.CompareTag("Player"))
        {
            // Cancel teleport if player leaves the trigger area
            if (teleportCoroutine != null)
            {
                StopCoroutine(teleportCoroutine);
                teleportCoroutine = null;
                
                // Remove indicator if any
                if (activeIndicator != null)
                {
                    Destroy(activeIndicator);
                }
                
                // Play cancel sound
                if (audioSource != null && teleportCancelSound != null)
                {
                    audioSource.clip = teleportCancelSound;
                    audioSource.Play();
                }
            }
            
            playerTransform = null;
            playerRigidbody = null;
        }
    }
    
    private IEnumerator TeleportSequence()
    {
        // Create indicator
        if (showTeleportIndicator && teleportIndicatorPrefab != null)
        {
            activeIndicator = Instantiate(teleportIndicatorPrefab, 
                                          playerTransform.position + Vector3.up, 
                                          Quaternion.identity);
        }
        
        // Play initiate sound
        if (audioSource != null && teleportInitiateSound != null)
        {
            audioSource.clip = teleportInitiateSound;
            audioSource.Play();
        }
        
        // Wait for delay
        float timer = 0f;
        while (timer < teleportDelay)
        {
            // Update indicator position if it exists
            if (activeIndicator != null && playerTransform != null)
            {
                activeIndicator.transform.position = playerTransform.position + Vector3.up;
                
                // Optional: Show progress in some way
                float progress = timer / teleportDelay;
                // For example, scale the indicator based on progress
                activeIndicator.transform.localScale = Vector3.one * (1f + progress * 0.5f);
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
        
        // Perform teleport if player is still in trigger and paired door exists
        if (playerTransform != null && pairedDoor != null)
        {
            TeleportPlayer();
        }
        
        // Clean up
        if (activeIndicator != null)
        {
            Destroy(activeIndicator);
        }
        
        teleportCoroutine = null;
    }
    
    private void TeleportPlayer()
    {
        if (pairedDoor == null || playerTransform == null) return;
        
        // Calculate facing direction
        Vector2 exitDirection = pairedDoor.transform.right; // Assuming doors face right by default
        
        // Calculate exit position (slightly in front of the exit door)
        Vector3 exitPosition = pairedDoor.transform.position + (Vector3)(exitDirection * exitOffset);
        
        // Store original velocity if we're preserving it
        Vector2 originalVelocity = Vector2.zero;
        if (preserveHorizontalVelocity && playerRigidbody != null)
        {
            originalVelocity.x = playerRigidbody.linearVelocity.x;
        }
        
        // Teleport the player
        playerTransform.position = exitPosition;
        
        // Restore horizontal velocity if needed
        if (preserveHorizontalVelocity && playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = new Vector2(originalVelocity.x, playerRigidbody.linearVelocity.y);
        }
        
        // Play completion sound (on exit door)
        if (pairedDoor.audioSource != null && teleportCompleteSound != null)
        {
            pairedDoor.audioSource.clip = teleportCompleteSound;
            pairedDoor.audioSource.Play();
        }
    }
    
    // Helper method to find paired door by ID (optional, for automatic setup)
    public void FindPairedDoor()
    {
        if (pairedDoor != null) return;
        
        PortalDoor[] allDoors = FindObjectsOfType<PortalDoor>();
        foreach (PortalDoor door in allDoors)
        {
            if (door != this && door.doorID == doorID && door.isEntryDoor != isEntryDoor)
            {
                pairedDoor = door;
                Debug.Log($"Door {gameObject.name} automatically paired with {door.gameObject.name}");
                return;
            }
        }
    }
    
    // Optional: Visual gizmos for editor
    private void OnDrawGizmosSelected()
    {
        // Draw door connection in editor
        if (pairedDoor != null)
        {
            Gizmos.color = isEntryDoor ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, pairedDoor.transform.position);
            
            if (isEntryDoor)
            {
                // Draw exit position
                Vector2 exitDirection = pairedDoor.transform.right; // Assuming doors face right by default
                Vector3 exitPosition = pairedDoor.transform.position + (Vector3)(exitDirection * exitOffset);
                Gizmos.DrawWireSphere(exitPosition, 0.5f);
            }
        }
    }
}