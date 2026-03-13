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

        PlayerWeaponManager weaponManager = FindWeaponManager(other.transform);
        if (weaponManager == null)
            return;

        bool added = weaponManager.UnlockShootingType(shootingTypeToUnlock, equipImmediately);
        if (added)
            Debug.Log($"[WeaponPickup] Unlocked shooting type: {shootingTypeToUnlock.displayName}");

        if (destroyOnPickup)
            Destroy(gameObject);
    }

    private PlayerWeaponManager FindWeaponManager(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        PlayerWeaponManager manager = hitTransform.GetComponent<PlayerWeaponManager>();
        if (manager != null)
            return manager;

        manager = hitTransform.GetComponentInChildren<PlayerWeaponManager>();
        if (manager != null)
            return manager;

        Transform root = hitTransform.root;
        if (root != null)
        {
            manager = root.GetComponent<PlayerWeaponManager>();
            if (manager != null)
                return manager;

            manager = root.GetComponentInChildren<PlayerWeaponManager>();
            if (manager != null)
                return manager;
        }

        return null;
    }
}
