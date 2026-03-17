using UnityEngine;

// Attach to bow/shooting-type weapon pickup prefabs dropped by enemies.
// Player can collect this to unlock/equip a ShootingType.
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private ShootingType shootingTypeToUnlock;
    [SerializeField] private bool equipImmediately = true;
    [SerializeField] private bool destroyOnPickup = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        Bow bow = FindBow(other.transform);
        if (bow == null)
            return;

        bool added = bow.UnlockShootingType(shootingTypeToUnlock, equipImmediately);
        if (added)
            Debug.Log($"[WeaponPickup] Unlocked shooting type: {shootingTypeToUnlock.displayName}");

        if (destroyOnPickup)
            Destroy(gameObject);
    }

    private Bow FindBow(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        Bow bow = hitTransform.GetComponentInChildren<Bow>();
        if (bow != null)
            return bow;

        Transform root = hitTransform.root;
        if (root != null)
            return root.GetComponentInChildren<Bow>();

        return null;
    }
}
