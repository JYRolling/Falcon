using UnityEngine;

// ScriptableObject holding all stats for a pickup weapon.
// Can be used for crossbow, explosive bow, or other temporary special weapons.
// Create via: Right-click in Project > Weapons > Crossbow
[CreateAssetMenu(menuName = "Weapons/Crossbow", fileName = "NewCrossbow")]
public class CrossbowData : ScriptableObject
{
    [Header("General")]
    public string displayName = "Crossbow";
    public Sprite icon;

    [Header("Firing")]
    [Tooltip("Shots per second while holding the fire button.")]
    public float fireRate = 5f;
    [Tooltip("Speed of each bolt.")]
    public float bulletSpeed = 18f;
    [Tooltip("Prefab spawned as a projectile when firing.")]
    public GameObject boltPrefab;

    [Tooltip("Optional ArrowType applied when the projectile prefab uses the Arrow script.")]
    public ArrowType projectileArrowType;

    [Header("Ammo")]
    [Tooltip("Ammo given to the player when this pickup is collected.")]
    public int ammoPerPickup = 30;
    [Tooltip("Maximum ammo that can be held at once (prevents spamming pickups).")]
    public int maxAmmo = 60;

    [Header("Damage")]
    public float damage = 5f;

    [Header("Layers")]
    [Tooltip("Layers the bolt treats as ground and stops on.")]
    public LayerMask groundLayer;

    [Header("Audio")]
    public AudioClip fireSFX;
    public AudioClip pickupSFX;

    [Header("VFX")]
    [Tooltip("Optional muzzle flash effect spawned at the fire point each shot.")]
    public GameObject muzzleFlashPrefab;
}
