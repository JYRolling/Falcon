using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField]
    private float maxHealth;

    [SerializeField]
    private GameObject
        deathChunkParticle,
        deathBloodParticle;

    private float currentHealth;

    private GameManager GM;

    [SerializeField]
    public HealthBar healthBar;

    private void Start()
    {
        currentHealth = maxHealth;

        // Inspector assignment may be missing (or PlayerStats is on a prefab asset).
        // Try a runtime fallback.
        var gmObj = GameObject.Find("GameManager");
        if (gmObj != null)
        {
            GM = gmObj.GetComponent<GameManager>();
            if (GM == null)
                Debug.LogError("[PlayerStats] GameManager component missing on GameManager object.");
        }
        else
        {
            Debug.LogError("[PlayerStats] GameManager object not found in scene.");
        }

        if (healthBar != null)
        {
            // Reject invalid references (boss bar or prefab asset reference).
            if (healthBar is BossHealthBar || !healthBar.gameObject.scene.IsValid())
                healthBar = null;
        }

        if (healthBar == null)
        {
            healthBar = FindPlayerHealthBarInScene();
            if (healthBar == null)
            {
                Debug.LogError("[PlayerStats] HealthBar reference not assigned and no non-boss HealthBar found in scene.");
            }
            else
            {
                Debug.Log("[PlayerStats] HealthBar auto-found at runtime: " + healthBar.gameObject.name);
            }
        }

        if (healthBar != null)
        {
            // Use float API
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetHealth(currentHealth);
        }
    }

    // New: allow external assignment of the scene HealthBar after instantiation
    public void AssignHealthBar(HealthBar hb)
    {
        if (hb == null || hb is BossHealthBar || !hb.gameObject.scene.IsValid())
        {
            Debug.LogWarning("[PlayerStats] AssignHealthBar called with invalid HealthBar reference.");
            return;
        }

        healthBar = hb;
        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        Debug.Log($"[PlayerStats] AssignHealthBar: assigning '{hb.gameObject.name}' to player '{gameObject.name}'. currentHealth={currentHealth}, maxHealth={maxHealth}");

        healthBar.SetMaxHealth(maxHealth);
        healthBar.SetHealth(currentHealth);
    }

    public void DecreaseHealth(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (healthBar != null)
            healthBar.SetHealth(currentHealth);
        else
            Debug.LogWarning("[PlayerStats] Tried to update HealthBar but reference is null.");

        if (currentHealth <= 0.0f)
        {
            Die();
        }
    }

    public void IncreaseHealth(float amount)
    {
        if (amount <= 0f)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (healthBar != null)
            healthBar.SetHealth(currentHealth);
        else
            Debug.LogWarning("[PlayerStats] Tried to update HealthBar but reference is null.");
    }

    private void Die()
    {
        if (deathChunkParticle) Instantiate(deathChunkParticle, transform.position, deathChunkParticle.transform.rotation);
        if (deathBloodParticle) Instantiate(deathBloodParticle, transform.position, deathBloodParticle.transform.rotation);
        if (GM != null)
            GM.Respawn();
        else
            Debug.LogError("[PlayerStats] Cannot respawn because GameManager reference is null.");
        Destroy(gameObject);
    }

    private static HealthBar FindPlayerHealthBarInScene()
    {
        var bars = Object.FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hb in bars)
        {
            if (hb == null) continue;
            if (hb is BossHealthBar) continue;
            if (!hb.gameObject.scene.IsValid()) continue;
            return hb;
        }

        return null;
    }
}
