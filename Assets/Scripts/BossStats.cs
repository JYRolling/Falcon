using UnityEngine;

[DisallowMultipleComponent]
public class BossStats : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Maximum health for this boss.")]
    public float maxHealth = 200f;

    [Tooltip("Runtime current health. Initialized from maxHealth.")]
    [HideInInspector]
    public float currentHealth;

    [Header("Touch Damage")]
    [Tooltip("Damage applied when player touches the boss.")]
    public float touchDamage = 10f;

    [Tooltip("Cooldown (seconds) between touch damage attempts.")]
    public float touchDamageCooldown = 1f;

    [Tooltip("Touch damage box width.")]
    public float touchDamageWidth = 1f;

    [Tooltip("Touch damage box height.")]
    public float touchDamageHeight = 1f;

    [Header("Death FX")]
    [Tooltip("Particles instantiated on boss death (chunks).")]
    public GameObject deathChunkParticle;

    [Tooltip("Particles instantiated on boss death (blood).")]
    public GameObject deathBloodParticle;

    [Header("Required: Boss HealthBar reference")]
    [Tooltip("Assign a BossHealthBar in the inspector. No runtime fallback will be used.")]
    [SerializeField]
    public BossHealthBar bossHealthBar;

    /// <summary>
    /// Initialize runtime fields (call from controller Start).
    /// </summary>
    public void Initialize()
    {
        currentHealth = maxHealth;

        // Require bossHealthBar to be assigned in inspector — no auto-find fallback.
        if (bossHealthBar == null)
        {
            Debug.LogError($"[BossStats] BossHealthBar reference not assigned on '{gameObject.name}'. Assign a BossHealthBar in the inspector. No runtime fallback will be used.");
        }
        else
        {
            bossHealthBar.SetMaxHealth((int)maxHealth);
            bossHealthBar.SetHealth((int)currentHealth);
        }
    }

    /// <summary>
    /// Apply damage to the stats and update any Health UI (BossHealthBar is preferred).
    /// Returns true if the hit killed the boss.
    /// </summary>
    public bool ApplyDamage(float amount)
    {
        currentHealth -= amount;

        // Update BossHealthBar (primary)
        BossHealthBar.Instance?.UpdateHealth(currentHealth);

        // Fallback: update assigned BossHealthBar only if provided (no auto-find).
        if (bossHealthBar != null)
            bossHealthBar.SetHealth((int)currentHealth);

        return currentHealth <= 0f;
    }
}