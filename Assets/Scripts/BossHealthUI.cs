using UnityEngine;

// BossHealthBar combines the previous BossHealthUI show/hide + boss registration behavior
// with the existing HealthBar visuals. Attach this to the UI GameObject that acts as the
// boss health container (for example Canvas -> BossHealthPanel). It will show/hide the
// container when bosses register/unregister and drive the inherited HealthBar.
public class BossHealthBar : HealthBar
{
    public static BossHealthBar Instance { get; private set; }

    [Tooltip("Optional: root GameObject to enable/disable when showing/hiding the boss health UI. If null, this GameObject is used.")]
    [SerializeField] private GameObject container;

    // how many bosses registered the UI (supports multiple bosses)
    private int _registeredBossCount = 0;
    private int _currentMaxHealth = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple BossHealthBar instances found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (container == null)
            container = gameObject;

        // Hide UI initially
        if (container != null)
            container.SetActive(false);
    }

    // Called by a boss when it becomes active
    public void RegisterBoss(float maxHealth, float currentHealth)
    {
        _registeredBossCount = Mathf.Max(0, _registeredBossCount) + 1;
        _currentMaxHealth = Mathf.CeilToInt(maxHealth);

        SetMaxHealthInternal(_currentMaxHealth);
        SetHealthInternal(Mathf.Clamp(Mathf.RoundToInt(currentHealth), 0, _currentMaxHealth));

        if (container != null)
            container.SetActive(true);
    }

    // Called by boss to update the current displayed HP
    public void UpdateHealth(float currentHealth)
    {
        if (_registeredBossCount <= 0) return;
        SetHealthInternal(Mathf.Clamp(Mathf.RoundToInt(currentHealth), 0, _currentMaxHealth));
    }

    // Called by a boss when it is destroyed (or dies)
    public void UnregisterBoss()
    {
        _registeredBossCount = Mathf.Max(0, _registeredBossCount - 1);
        if (_registeredBossCount == 0 && container != null)
            container.SetActive(false);
    }

    // Internal helpers that call into the inherited HealthBar methods
    private void SetMaxHealthInternal(int health)
    {
        // HealthBar.SetMaxHealth handles slider, fill color, etc.
        SetMaxHealth(health);
    }

    private void SetHealthInternal(int health)
    {
        SetHealth(health);
    }
}