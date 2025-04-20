using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PlayerHealthManager : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float damageInterval = 1.5f; // Time between damage instances from the same enemy
    [SerializeField] private float invulnerabilityTime = 0.5f; // Brief invulnerability after any hit
    
    [Header("Effects")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private GameObject damageEffect;
    
    [Header("Slowdown Settings")]
    [SerializeField] private float trapSlowdownFactor = 0.5f; // 50% movement speed when in a trap
    [SerializeField] private float slowdownDuration = 1.5f; // How long slowdown lasts after leaving a trap
    
    [Header("Enemy Types")]
    [SerializeField] private string[] enemyTags = new string[] { "Enemy", "GroundChaseEnemy", "AirChaseEnemy", "GroundHopEnemy" };
    
    // Enemy references - for manual assignment through inspector
    [Header("Enemy References (Optional)")]
    [SerializeField] private List<GameObject> groundChaseEnemies = new List<GameObject>();
    [SerializeField] private List<GameObject> airChaseEnemies = new List<GameObject>();
    [SerializeField] private List<GameObject> groundHopEnemies = new List<GameObject>();
    
    private CircleCollider2D detectionCollider;
    private SpriteRenderer playerSprite;
    private Color originalColor;
    private Dictionary<int, float> lastDamageTimeByEnemy = new Dictionary<int, float>();
    private bool isInvulnerable = false;
    private float lastInvulnerabilityTime;
    private bool isSlowed = false;
    private float slowedUntilTime = 0f;
    private PlayerController playerMovement; // Assume you have this component
    private AudioSource audioSource;
    private float originalMoveSpeed;
    
    private void Awake()
    {
        // Setup detection collider
        detectionCollider = GetComponent<CircleCollider2D>();
        detectionCollider.isTrigger = true;
        detectionCollider.radius = 0.75f; // Adjust based on your player's scale
        
        // Get references
        playerSprite = GetComponentInChildren<SpriteRenderer>();
        playerMovement = GetComponent<PlayerController>();
        audioSource = GetComponent<AudioSource>();
        
        if (playerSprite != null)
        {
            originalColor = playerSprite.color;
        }
        
        if (audioSource == null && damageSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        if (playerMovement != null)
        {
            originalMoveSpeed = playerMovement.MoveSpeed;
        }
    }
    
    private void Update()
    {
        // Check invulnerability state
        if (isInvulnerable && Time.time - lastInvulnerabilityTime > invulnerabilityTime)
        {
            isInvulnerable = false;
            // Make sure player color is reset at end of invulnerability
            if (playerSprite != null)
            {
                playerSprite.color = originalColor;
            }
        }
        
        // Check slowdown duration
        if (isSlowed && Time.time > slowedUntilTime)
        {
            EndSlowdown();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check for traps - apply slowdown instead of damage
        if (other.CompareTag("Trap"))
        {
            ApplySlowdown();
            return;
        }
        
        // Check for enemies - apply damage
        if (IsEnemy(other.gameObject))
        {
            // Only apply damage if not invulnerable
            if (!isInvulnerable)
            {
                ApplyDamage(other.gameObject);
            }
        }
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        // For traps, keep the player slowed while in contact
        if (other.CompareTag("Trap"))
        {
            // Reset the end time for slowdown
            slowedUntilTime = Time.time + slowdownDuration;
            
            // Make sure slowdown is applied
            if (!isSlowed)
            {
                ApplySlowdown();
            }
            return;
        }
        
        // For enemies, apply damage periodically if in continuous contact
        if (IsEnemy(other.gameObject) && !isInvulnerable)
        {
            int enemyID = other.gameObject.GetInstanceID();
            
            // Check if enough time has passed since the last damage from this enemy
            if (!lastDamageTimeByEnemy.ContainsKey(enemyID) || 
                Time.time - lastDamageTimeByEnemy[enemyID] >= damageInterval)
            {
                ApplyDamage(other.gameObject);
            }
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        // For traps, start the cooldown for slowdown effect
        if (other.CompareTag("Trap"))
        {
            // Set the end time for slowdown (continues for a short duration after leaving)
            slowedUntilTime = Time.time + slowdownDuration;
        }
    }
    
    private bool IsEnemy(GameObject obj)
    {
        // Check by tag
        foreach (string tag in enemyTags)
        {
            if (obj.CompareTag(tag))
            {
                return true;
            }
        }
        
        // Check by reference list
        if (groundChaseEnemies.Contains(obj) || 
            airChaseEnemies.Contains(obj) || 
            groundHopEnemies.Contains(obj))
        {
            return true;
        }
        
        // Check by component
        if (obj.GetComponent<GroundEnemyFixedChase>() != null ||
            obj.GetComponent<AirEnemyChase>() != null || 
            obj.GetComponent<GroundHopEnemyChase>() != null)
        {
            return true;
        }
        
        return false;
    }
    
    private void ApplyDamage(GameObject enemy)
    {
        // Record time of this damage instance
        int enemyID = enemy.GetInstanceID();
        lastDamageTimeByEnemy[enemyID] = Time.time;
        
        // Apply damage through GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateHealth(-damageAmount);
            Debug.Log($"Player damaged by {enemy.name} for {damageAmount} health");
        }
        
        // Set invulnerable state
        isInvulnerable = true;
        lastInvulnerabilityTime = Time.time;
        
        // Visual effects
        StartCoroutine(FlashEffect());
        
        // Sound effect
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
        
        // Particle effect
        if (damageEffect != null)
        {
            Instantiate(damageEffect, transform.position, Quaternion.identity);
        }
    }
    
    private void ApplySlowdown()
    {
        if (isSlowed) return;
        
        isSlowed = true;
        
        // Apply slowdown to player movement
        if (playerMovement != null)
        {
            playerMovement.MoveSpeed = originalMoveSpeed * trapSlowdownFactor;
            Debug.Log($"Player slowed: {originalMoveSpeed} -> {playerMovement.MoveSpeed}");
        }
        
        // Visual indication of slowdown
        if (playerSprite != null)
        {
            playerSprite.color = new Color(0.5f, 0.5f, 1f, 0.8f); // Blue tint
        }
    }
    
    private void EndSlowdown()
    {
        isSlowed = false;
        
        // Restore original move speed
        if (playerMovement != null)
        {
            playerMovement.MoveSpeed = originalMoveSpeed;
            Debug.Log("Player speed restored");
        }
        
        // Restore original color if not currently invulnerable
        if (playerSprite != null && !isInvulnerable)
        {
            playerSprite.color = originalColor;
        }
    }
    
    private IEnumerator FlashEffect()
    {
        if (playerSprite != null)
        {
            playerSprite.color = hitFlashColor;
            yield return new WaitForSeconds(flashDuration);
            
            // Only reset color if we're not slowed (to keep blue tint)
            if (!isSlowed)
            {
                playerSprite.color = originalColor;
            }
            else
            {
                playerSprite.color = new Color(0.5f, 0.5f, 1f, 0.8f); // Blue tint
            }
        }
    }
    
    // Public method to add enemy references at runtime
    public void RegisterEnemy(GameObject enemy, string enemyType)
    {
        switch (enemyType.ToLower())
        {
            case "groundchase":
                if (!groundChaseEnemies.Contains(enemy))
                    groundChaseEnemies.Add(enemy);
                break;
            case "airchase":
                if (!airChaseEnemies.Contains(enemy))
                    airChaseEnemies.Add(enemy);
                break;
            case "groundhop":
                if (!groundHopEnemies.Contains(enemy))
                    groundHopEnemies.Add(enemy);
                break;
        }
    }
}