using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/Melee Weapon", fileName = "NewMeleeWeapon")]
public class MeleeWeapon : ScriptableObject
{
    [Header("General")]
    public string displayName = "Sword";
    public Sprite icon;

    [Header("Combat")]
    public float damage = 10f;
    public float attackCooldown = 0.6f;
    public float attackRange = 1.2f;

    [Header("VFX")]
    public GameObject hitEffectPrefab;
    public AudioClip attackSFX;
    public AudioClip hitSFX;

    [Header("Optional")]
    [Tooltip("Prefab to equip as visual representation (e.g., sword model). If null, uses icon only.")]
    public GameObject visualPrefab;
}
