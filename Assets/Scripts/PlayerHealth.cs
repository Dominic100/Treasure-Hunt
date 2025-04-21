using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float damageAmount = 10f; // 10% damage per hit
    [SerializeField] private float invincibilityDuration = 1.5f; // Time after damage when player can't be damaged again
    
    [Header("Visual Feedback")]
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private int numberOfFlashes = 3;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.3f, 0.3f, 0.7f);
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Events
    public event Action<float> OnHealthChanged;
    public event Action OnPlayerDeath;
    
    // Private variables
    private bool isInvincible = false;
    private float invincibilityTimer = 0f;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    
    // Damage sources
    private LayerMask enemyLayers;
    
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        currentHealth = maxHealth;
        
        // Set up collision detection
        enemyLayers = LayerMask.GetMask("Enemy");
    }
    
    private void Update()
    {
        // Handle invincibility timer
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = originalColor;
                }
                
                if (debugMode)
                    Debug.Log("Player invincibility ended");
            }
        }
    }
    
    // Method for enemies to call when they damage the player
    public void TakeDamage(float amount)
    {
        // Ignore damage if player is invincible
        if (isInvincible) return;
        
        // Apply damage
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        
        // Trigger health changed event
        OnHealthChanged?.Invoke(currentHealth);
        
        if (debugMode)
            Debug.Log($"Player took {amount} damage. Health: {currentHealth}/{maxHealth}");
        
        // Start invincibility period
        StartInvincibility();
        
        // Visual feedback
        if (flashOnDamage && spriteRenderer != null)
        {
            StartCoroutine(FlashRoutine());
        }
        
        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    // Method called by enemy proximity triggers
    public void TakeDamageFromEnemy()
    {
        TakeDamage(damageAmount);
    }
    
    // Handle player death
    private void Die()
    {
        if (debugMode)
            Debug.Log("Player died!");
        
        // Trigger death event
        OnPlayerDeath?.Invoke();
        
        // You could implement death behavior here, like:
        // - Play death animation
        // - Disable player input
        // - Show game over screen
        // - Respawn logic
    }
    
    // Start invincibility period
    private void StartInvincibility()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
    }
    
    // Flash the sprite when damaged
    private System.Collections.IEnumerator FlashRoutine()
    {
        for (int i = 0; i < numberOfFlashes; i++)
        {
            spriteRenderer.color = damageFlashColor;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(flashDuration);
        }
    }
    
    // Heal the player
    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
        
        if (debugMode)
            Debug.Log($"Player healed for {amount}. Health: {currentHealth}/{maxHealth}");
    }
    
    // Get current health percentage (0-1)
    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
    
    // Full heal
    public void RestoreFullHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
        
        if (debugMode)
            Debug.Log("Player health fully restored");
    }
    
    // Check if player is at full health
    public bool IsFullHealth()
    {
        return Mathf.Approximately(currentHealth, maxHealth);
    }
}