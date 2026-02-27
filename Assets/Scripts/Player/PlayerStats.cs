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

        if (healthBar == null)
        {
            healthBar = FindObjectOfType<HealthBar>();
            if (healthBar == null)
            {
                Debug.LogError("[PlayerStats] HealthBar reference not assigned and no HealthBar found in scene.");
            }
            else
            {
                Debug.Log("[PlayerStats] HealthBar auto-found at runtime: " + healthBar.gameObject.name);
            }
        }

        if (healthBar != null)
        {
            healthBar.SetMaxHealth((int)maxHealth);
        }
    }

    public void DecreaseHealth(float amount)
    {
        currentHealth -= amount;

        if (healthBar != null)
            healthBar.SetHealth((int)currentHealth);
        else
            Debug.LogWarning("[PlayerStats] Tried to update HealthBar but reference is null.");

        if (currentHealth <= 0.0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Instantiate(deathChunkParticle, transform.position, deathChunkParticle.transform.rotation);
        Instantiate(deathBloodParticle, transform.position, deathBloodParticle.transform.rotation);
        if (GM != null)
            GM.Respawn();
        else
            Debug.LogError("[PlayerStats] Cannot respawn because GameManager reference is null.");
        Destroy(gameObject);
    }
}
