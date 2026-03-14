using UnityEngine;

// Attach to the crossbow pickup prefab dropped by enemies (or placed in level).
// Player walks into the trigger to collect it (Metal Slug style).
//
// Prefab setup:
//   - Add BoxCollider2D with IsTrigger = true
//   - Add Rigidbody2D with Body Type = Kinematic
//   - Add SpriteRenderer (crossbow sprite)
//   - Assign CrossbowData asset in Inspector
public class CrossbowPickup : MonoBehaviour
{
    [SerializeField] private CrossbowData crossbowData;

    [Tooltip("Leave 0 to use ammoPerPickup defined in CrossbowData.")]
    [SerializeField] private int ammoOverride = 0;

    [SerializeField] private bool destroyOnPickup = true;

    [Tooltip("Optional effect spawned at pickup position on collect.")]
    [SerializeField] private GameObject pickupEffectPrefab;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (crossbowData == null) return;

        CrossbowController controller = FindCrossbowController(other.transform);
        if (controller == null) return;

        int ammo = ammoOverride > 0 ? ammoOverride : crossbowData.ammoPerPickup;
        controller.Equip(crossbowData, ammo);

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        if (destroyOnPickup)
            Destroy(gameObject);
    }

    private static CrossbowController FindCrossbowController(Transform t)
    {
        if (t == null) return null;

        var ctrl = t.GetComponent<CrossbowController>();
        if (ctrl != null) return ctrl;

        ctrl = t.GetComponentInChildren<CrossbowController>();
        if (ctrl != null) return ctrl;

        var root = t.root;
        if (root != null)
        {
            ctrl = root.GetComponent<CrossbowController>();
            if (ctrl != null) return ctrl;

            ctrl = root.GetComponentInChildren<CrossbowController>();
            if (ctrl != null) return ctrl;
        }

        return null;
    }
}
