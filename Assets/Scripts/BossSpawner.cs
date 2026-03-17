using UnityEngine;

// Spawns a boss and an optional wall when a trigger is entered.
// Usage:
// - Add this script to a GameObject that has a Collider2D set to "Is Trigger".
// - Assign `bossPrefab` and `bossSpawnPoint` (create an empty GameObject where the boss should appear).
// - Optionally assign `wallPrefab` and `wallSpawnPoint`.
// - The trigger will react to GameObjects with tag "Player" by default.
[RequireComponent(typeof(Collider2D))]
public class BossSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Boss prefab to instantiate (should include BossEnemyController)")]
    [SerializeField] private GameObject bossPrefab;
    [Tooltip("Optional wall prefab to instantiate when spawn occurs")]
    [SerializeField] private GameObject wallPrefab;

    [Header("Spawn Points")]
    [Tooltip("Empty GameObject marking where the boss will spawn")]
    [SerializeField] private Transform bossSpawnPoint;
    [Tooltip("Optional empty GameObject marking where the wall will spawn (falls back to spawner position)")]
    [SerializeField] private Transform wallSpawnPoint;

    [Header("Trigger")]
    [Tooltip("Tag required for the entering collider to trigger the spawn")]
    [SerializeField] private string triggerTag = "Player";
    [Tooltip("If true spawn only once then disable this component")]
    [SerializeField] private bool spawnOnce = true;

    private bool _hasSpawned = false;

    private void Awake()
    {
        // Ensure the Collider2D is configured as a trigger (helpful but not forced)
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"{nameof(BossSpawner)} on '{gameObject.name}': Collider2D.isTrigger is false. This spawner expects a trigger collider.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasSpawned && spawnOnce) return;

        if (other != null && other.CompareTag(triggerTag))
        {
            Spawn();
            if (spawnOnce) _hasSpawned = true;
        }
    }

    private void Spawn()
    {
        if (bossPrefab != null)
        {
            Vector3 pos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
            Quaternion rot = bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity;
            var bossInstance = Instantiate(bossPrefab, pos, rot);
            // Optional: make spawned boss children of a container for organization (uncomment and set a parent transform if desired)
            // bossInstance.transform.SetParent(someParentTransform, worldPositionStays: true);
            Debug.Log($"Spawned boss '{bossPrefab.name}' at {pos}");
        }
        else
        {
            Debug.LogWarning($"{nameof(BossSpawner)}: bossPrefab is not assigned on '{gameObject.name}'.");
        }

        if (wallPrefab != null)
        {
            Vector3 wpos = wallSpawnPoint != null ? wallSpawnPoint.position : transform.position;
            Quaternion wrot = wallSpawnPoint != null ? wallSpawnPoint.rotation : Quaternion.identity;
            Instantiate(wallPrefab, wpos, wrot);
            Debug.Log($"Spawned wall '{wallPrefab.name}' at {wpos}");
        }
    }

    // Visual helpers in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (bossSpawnPoint != null)
            Gizmos.DrawWireSphere(bossSpawnPoint.position, 0.25f);
        else
            Gizmos.DrawWireSphere(transform.position, 0.25f);

        Gizmos.color = Color.yellow;
        if (wallSpawnPoint != null)
            Gizmos.DrawWireCube(wallSpawnPoint.position, Vector3.one * 0.5f);
    }
}