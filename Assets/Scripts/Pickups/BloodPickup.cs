using UnityEngine;

// Attach to blood pickup prefab dropped by enemies (or placed in level).
// Player walks into the trigger to collect it and restore health.
//
// Prefab setup:
//   - Add BoxCollider2D with IsTrigger = true
//   - Add Rigidbody2D with Body Type = Kinematic
//   - Add SpriteRenderer (blood sprite)
//   - Assign health restore amount in Inspector
public class BloodPickup : MonoBehaviour
{
    [SerializeField] private float healthRestoreAmount = 20f;

    [SerializeField] private bool destroyOnPickup = true;

    [Tooltip("Optional effect spawned at pickup position on collect.")]
    [SerializeField] private GameObject pickupEffectPrefab;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats playerStats = FindPlayerStats(other.transform);
        if (playerStats == null) return;

        playerStats.IncreaseHealth(healthRestoreAmount);

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        if (destroyOnPickup)
            Destroy(gameObject);
    }

    private static PlayerStats FindPlayerStats(Transform t)
    {
        if (t == null) return null;

        var stats = t.GetComponent<PlayerStats>();
        if (stats != null) return stats;

        stats = t.GetComponentInChildren<PlayerStats>();
        if (stats != null) return stats;

        var root = t.root;
        if (root != null)
        {
            stats = root.GetComponent<PlayerStats>();
            if (stats != null) return stats;

            stats = root.GetComponentInChildren<PlayerStats>();
            if (stats != null) return stats;
        }

        return null;
    }
}
