using UnityEngine;

public enum ShootingStyle { Single, Multi }

[CreateAssetMenu(menuName = "Bow/Shooting Type", fileName = "NewShootingType")]
public class ShootingType : ScriptableObject
{
    [Header("General")]
    public string displayName;
    public ShootingStyle shootingStyle = ShootingStyle.Single;
    public Sprite icon;

    [Tooltip("Optional: arrow prefab to use for this shooting type. If null Bow uses its selected arrow prefab.")]
    public GameObject arrowPrefab;

    [Tooltip("Optional: link to ArrowType ScriptableObject to apply arrow data.")]
    public ArrowType arrowType;

    [Header("Multi-shot")]
    [Tooltip("Number of projectiles for multi-shot. 1 = single.")]
    public int projectileCount = 1;
    [Tooltip("Total spread angle in degrees for multi-shot.")]
    public float spreadAngle = 30f;

    [Header("Modifiers")]
    public float launchForceMultiplier = 1f;
}