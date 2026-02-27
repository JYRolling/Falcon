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
}