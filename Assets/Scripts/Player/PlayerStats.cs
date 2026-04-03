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

    // Invulnerability flag (not serialized by default; you can serialize for testing)
    [SerializeField]
    private bool isInvulnerable = false;

    [Header("Damage Blink")]
    [SerializeField]
    private int damageBlinkCount = 4;

    [SerializeField]
    private float damageBlinkInterval = 0.06f;

    [SerializeField, Range(0f, 1f)]
    private float damageBlinkMinAlpha = 0.25f;

    private SpriteRenderer[] spriteRenderers;
    private Coroutine damageBlinkCoroutine;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

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
        if (amount <= 0f)
            return;

        // Respect invulnerability
        if (isInvulnerable)
        {
            Debug.Log("[PlayerStats] Player is invulnerable � damage ignored.");
            return;
        }

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        if (healthBar != null)
            healthBar.SetHealth(currentHealth);
        else
            Debug.LogWarning("[PlayerStats] Tried to update HealthBar but reference is null.");

        if (currentHealth > 0f)
            TriggerDamageBlink();

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

    // Invulnerability API
    public void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
        Debug.Log($"[PlayerStats] Invulnerability set to {isInvulnerable} on '{gameObject.name}'");
    }

    public void ToggleInvulnerability()
    {
        SetInvulnerable(!isInvulnerable);
    }

    public bool IsInvulnerable()
    {
        return isInvulnerable;
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

    private void TriggerDamageBlink()
    {
        if (!isActiveAndEnabled)
            return;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        if (damageBlinkCoroutine != null)
            StopCoroutine(damageBlinkCoroutine);

        damageBlinkCoroutine = StartCoroutine(DamageBlinkRoutine());
    }

    private IEnumerator DamageBlinkRoutine()
    {
        int blinkCount = Mathf.Max(1, damageBlinkCount);
        float interval = Mathf.Max(0.01f, damageBlinkInterval);

        var originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }

        for (int i = 0; i < blinkCount; i++)
        {
            SetSpriteAlpha(originalColors, damageBlinkMinAlpha);
            yield return new WaitForSeconds(interval);

            SetSpriteAlpha(originalColors, 1f);
            yield return new WaitForSeconds(interval);
        }

        SetSpriteAlpha(originalColors, 1f);
        damageBlinkCoroutine = null;
    }

    private void SetSpriteAlpha(Color[] originalColors, float alphaScale)
    {
        float clampedScale = Mathf.Clamp01(alphaScale);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr == null)
                continue;

            Color c = originalColors[i];
            c.a = originalColors[i].a * clampedScale;
            sr.color = c;
        }
    }
}
