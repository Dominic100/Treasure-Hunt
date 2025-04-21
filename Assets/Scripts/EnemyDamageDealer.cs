using UnityEngine;

public class EnemyDamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private bool canDealDamage = true;
    [SerializeField] private float contactDamageAmount = 10f;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Collider Settings")]
    [SerializeField] private Collider2D damageCollider; // Set this in the inspector
    [SerializeField] private bool useThisCollider = false; // Set to true to use the collider on this GameObject
    [SerializeField] private bool ensureTriggerEnabled = true; // Force the collider to be a trigger
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    private PlayerHealth playerHealth;
    
    private void Awake()
    {
        // Get collider reference based on options
        if (useThisCollider)
        {
            damageCollider = GetComponent<Collider2D>();
        }
        
        // Validate that we have a collider
        if (damageCollider == null)
        {
            Debug.LogError($"No damage collider assigned to {gameObject.name}! Damage dealing won't work.");
            canDealDamage = false;
            return;
        }
        
        // Ensure the collider is a trigger if needed
        if (ensureTriggerEnabled && !damageCollider.isTrigger)
        {
            damageCollider.isTrigger = true;
            if (debugMode)
                Debug.Log($"Forced damage collider to be a trigger on {gameObject.name}");
        }
    }
    
    private void Start()
    {
        // Find player health component
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null && debugMode)
            {
                Debug.LogWarning($"Player object found, but it has no PlayerHealth component! {gameObject.name} won't deal damage.");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"No player with tag '{playerTag}' found! {gameObject.name} won't deal damage.");
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canDealDamage) return;
        
        // Only process this method if the collision happened with our specified damage collider
        if (other.CompareTag(playerTag) && 
            (damageCollider == null || damageCollider.gameObject == gameObject))
        {
            // Try to get player health if we don't have it yet
            if (playerHealth == null)
            {
                playerHealth = other.GetComponent<PlayerHealth>();
            }
            
            // Deal damage if we found the player health component
            if (playerHealth != null)
            {
                playerHealth.TakeDamageFromEnemy();
                
                if (debugMode)
                    Debug.Log($"{gameObject.name} dealt damage to player");
            }
        }
    }
    
    // This method handles the case when we're using a child collider
    public void OnDamageTriggerEnter(Collider2D other)
    {
        if (!canDealDamage) return;
        
        if (other.CompareTag(playerTag))
        {
            // Try to get player health if we don't have it yet
            if (playerHealth == null)
            {
                playerHealth = other.GetComponent<PlayerHealth>();
            }
            
            // Deal damage if we found the player health component
            if (playerHealth != null)
            {
                playerHealth.TakeDamageFromEnemy();
                
                if (debugMode)
                    Debug.Log($"{gameObject.name} dealt damage to player (via child collider)");
            }
        }
    }
    
    // Enable/disable damage dealing (can be called from other scripts)
    public void SetDamageEnabled(bool enabled)
    {
        canDealDamage = enabled;
        
        // You can also enable/disable the collider itself for better performance
        if (damageCollider != null)
        {
            damageCollider.enabled = enabled;
        }
    }
    
    // Utility method to create a dedicated trigger collider for damage
    [ContextMenu("Create Damage Trigger Collider")]
    private void CreateDamageTriggerCollider()
    {
        // Create a child object for the trigger
        GameObject triggerObj = new GameObject("DamageTrigger");
        triggerObj.transform.SetParent(transform);
        triggerObj.transform.localPosition = Vector3.zero;
        
        // Add a circle collider that's slightly larger than typical collision colliders
        CircleCollider2D newCollider = triggerObj.AddComponent<CircleCollider2D>();
        newCollider.isTrigger = true;
        newCollider.radius = 0.8f; // Default size, adjust in inspector
        
        // Add a trigger relay component to send collision events back to this script
        TriggerRelay relay = triggerObj.AddComponent<TriggerRelay>();
        relay.Initialize(this);
        
        // Assign this new collider as our damage collider
        damageCollider = newCollider;
        
        Debug.Log($"Created damage trigger collider for {gameObject.name}");
    }
}

// Helper component to relay trigger events from child colliders
public class TriggerRelay : MonoBehaviour
{
    private EnemyDamageDealer damageDealer;
    
    public void Initialize(EnemyDamageDealer dealer)
    {
        damageDealer = dealer;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (damageDealer != null)
        {
            damageDealer.OnDamageTriggerEnter(other);
        }
    }
}