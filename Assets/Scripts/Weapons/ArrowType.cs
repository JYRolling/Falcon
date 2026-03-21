using UnityEngine;

[CreateAssetMenu(menuName = "Arrow/Arrow Type", fileName = "NewArrowType")]
public class ArrowType : ScriptableObject
{
    [Header("General")]
    public string displayName;
    public float damage = 1f;
    public LayerMask groundLayer;
    public float gravityScale = 1f;

    [Tooltip("Sprite used by the arrow GameObject in the world")]
    public Sprite sprite;

    [Tooltip("Icon used for UI (bow/slot icons). If null, 'sprite' will be used as a fallback.")]
    public Sprite icon;

    [Header("VFX")]
    public GameObject impactVFX;

    [Header("Explosion")]
    [Tooltip("If true, arrow will explode on first hit and deal area damage.")]
    public bool isExplosive = false;

    [Min(0f)]
    [Tooltip("Explosion radius in world units.")]
    public float explosionRadius = 1.5f;

    [Min(0f)]
    [Tooltip("Multiplier applied to base damage for explosion hit.")]
    public float explosionDamageMultiplier = 1f;

    [Tooltip("Optional explosion VFX prefab.")]
    public GameObject explosionVFX;

    [Header("Debug Gizmo")]
    [Tooltip("Show explosion radius gizmo in Scene view when the arrow object is selected.")]
    public bool showExplosionRadiusGizmo = true;

    [Tooltip("Color used for the explosion radius gizmo.")]
    public Color explosionGizmoColor = new Color(1f, 0.35f, 0.1f, 0.9f);
}