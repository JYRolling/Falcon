using UnityEngine;

// Attach this to the Player root GameObject.
// Manages unlocking and equipping both bow (ShootingType) and melee weapons.
public class PlayerWeaponManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Bow bowl;
    [SerializeField] private Transform meleeWeaponVisualParent;

    [Header("Melee Weapons")]
    private MeleeWeapon[] unlockedMeleeWeapons = new MeleeWeapon[0];
    private int selectedMeleeWeaponIndex = -1;
    private MeleeWeapon equippedMeleeWeapon;
    private GameObject currentMeleeVisual;

    private void Start()
    {
        // Auto-find references if not assigned
        if (bowl == null)
        {
            bowl = GetComponentInChildren<Bow>();
        }

        if (meleeWeaponVisualParent == null)
        {
            // Try to find or create a visual parent (e.g., a child Transform)
            Transform existing = transform.Find("MeleeVisual");
            if (existing != null)
            {
                meleeWeaponVisualParent = existing;
            }
        }
    }

    /// <summary>
    /// Unlock and optionally equip a MeleeWeapon.
    /// Returns true if new weapon was added, false if already owned.
    /// </summary>
    public bool UnlockMeleeWeapon(MeleeWeapon weapon, bool equipImmediately)
    {
        if (weapon == null)
            return false;

        // Check if already owned
        int existingIndex = FindMeleeWeaponIndex(weapon);
        if (existingIndex >= 0)
        {
            if (equipImmediately)
                EquipMeleeWeapon(existingIndex);
            return false;
        }

        // Add new weapon
        int oldLen = unlockedMeleeWeapons.Length;
        MeleeWeapon[] next = new MeleeWeapon[oldLen + 1];

        for (int i = 0; i < oldLen; i++)
            next[i] = unlockedMeleeWeapons[i];

        next[oldLen] = weapon;
        unlockedMeleeWeapons = next;

        if (equipImmediately)
            EquipMeleeWeapon(oldLen);

        return true;
    }

    /// <summary>
    /// Unlock and optionally equip a ShootingType (bow weapon).
    /// </summary>
    public bool UnlockShootingType(ShootingType shootingType, bool equipImmediately)
    {
        if (bowl == null)
        {
            Debug.LogWarning("[PlayerWeaponManager] Bow not found, cannot unlock shooting type.");
            return false;
        }

        return bowl.UnlockShootingType(shootingType, equipImmediately);
    }

    /// <summary>
    /// Equip a melee weapon by index.
    /// </summary>
    public void EquipMeleeWeapon(int index)
    {
        if (index < 0 || index >= unlockedMeleeWeapons.Length)
            return;

        selectedMeleeWeaponIndex = index;
        equippedMeleeWeapon = unlockedMeleeWeapons[index];

        // Update visual representation
        UpdateMeleeVisual();

        Debug.Log($"[PlayerWeaponManager] Equipped melee weapon: {equippedMeleeWeapon.displayName}");
    }

    /// <summary>
    /// Get the currently equipped melee weapon.
    /// </summary>
    public MeleeWeapon GetEquippedMeleeWeapon()
    {
        return equippedMeleeWeapon;
    }

    /// <summary>
    /// Cycle to next melee weapon.
    /// </summary>
    public void CycleNextMeleeWeapon()
    {
        if (unlockedMeleeWeapons.Length == 0)
            return;

        int nextIndex = (selectedMeleeWeaponIndex + 1) % unlockedMeleeWeapons.Length;
        EquipMeleeWeapon(nextIndex);
    }

    /// <summary>
    /// Cycle to previous melee weapon.
    /// </summary>
    public void CyclePreviousMeleeWeapon()
    {
        if (unlockedMeleeWeapons.Length == 0)
            return;

        int prevIndex = (selectedMeleeWeaponIndex - 1 + unlockedMeleeWeapons.Length) % unlockedMeleeWeapons.Length;
        EquipMeleeWeapon(prevIndex);
    }

    /// <summary>
    /// Get all unlocked melee weapons.
    /// </summary>
    public MeleeWeapon[] GetUnlockedMeleeWeapons()
    {
        return unlockedMeleeWeapons;
    }

    /// <summary>
    /// Get the current equipped melee weapon index.
    /// </summary>
    public int GetSelectedMeleeWeaponIndex()
    {
        return selectedMeleeWeaponIndex;
    }

    private int FindMeleeWeaponIndex(MeleeWeapon weapon)
    {
        for (int i = 0; i < unlockedMeleeWeapons.Length; i++)
        {
            if (unlockedMeleeWeapons[i] == weapon)
                return i;
        }
        return -1;
    }

    private void UpdateMeleeVisual()
    {
        // Clear old visual
        if (currentMeleeVisual != null)
            Destroy(currentMeleeVisual);

        if (equippedMeleeWeapon == null || equippedMeleeWeapon.visualPrefab == null)
            return;

        // Instantiate new visual
        Transform spawnParent = meleeWeaponVisualParent != null ? meleeWeaponVisualParent : transform;
        currentMeleeVisual = Instantiate(equippedMeleeWeapon.visualPrefab, spawnParent);
        currentMeleeVisual.transform.localPosition = Vector3.zero;
        currentMeleeVisual.name = equippedMeleeWeapon.displayName;
    }
}
