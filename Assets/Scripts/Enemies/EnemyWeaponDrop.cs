using UnityEngine;

// Attach this component to an enemy root object.
// Call DropNow() when the enemy dies to spawn a weapon pickup prefab.
public class EnemyWeaponDrop : MonoBehaviour
{
    [System.Serializable]
    public class DropEntry
    {
        [Tooltip("Weapon pickup prefab to spawn.")]
        public GameObject prefab;

        [Tooltip("Relative weight used when selecting this entry.")]
        [Min(0f)]
        public float weight = 1f;
    }

    [Header("Drop Chance")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.35f;

    [SerializeField] private DropEntry[] possibleDrops;

    [Header("Spawn")]
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Vector2 randomOffset = new Vector2(0.25f, 0.1f);

    private bool hasDropped;

    public void DropNow()
    {
        if (hasDropped)
            return;

        hasDropped = true;

        if (possibleDrops == null || possibleDrops.Length == 0)
            return;

        if (Random.value > dropChance)
            return;

        GameObject selected = SelectDropPrefab();
        if (selected == null)
            return;

        Vector3 basePos = dropPoint != null ? dropPoint.position : transform.position;
        Vector3 jitter = new Vector3(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(-randomOffset.y, randomOffset.y),
            0f);

        Instantiate(selected, basePos + jitter, Quaternion.identity);
    }

    private GameObject SelectDropPrefab()
    {
        float totalWeight = 0f;
        for (int i = 0; i < possibleDrops.Length; i++)
        {
            DropEntry entry = possibleDrops[i];
            if (entry == null || entry.prefab == null || entry.weight <= 0f)
                continue;

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);
        float running = 0f;

        for (int i = 0; i < possibleDrops.Length; i++)
        {
            DropEntry entry = possibleDrops[i];
            if (entry == null || entry.prefab == null || entry.weight <= 0f)
                continue;

            running += entry.weight;
            if (roll <= running)
                return entry.prefab;
        }

        return null;
    }
}