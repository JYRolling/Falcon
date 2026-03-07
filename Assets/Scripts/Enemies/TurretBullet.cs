using System.Collections;
using UnityEngine;

// Simple 2D projectile for turrets.
// - Attach to the bullet prefab (requires Rigidbody2D and Collider2D).
public class TurretBullet : MonoBehaviour
{
    public enum FireMode
    {
        Left,
        Right,
        Up,
        Down,
        AimAtPlayer,      // aim at player position at spawn time
        InitialDirection, // use `initialDirection` (inspector)
        ExplicitTarget    // use SetTargetPosition(worldPos) (takes precedence)
    }

    [Header("Motion")]
    public float speed = 10f;
    [Tooltip("Choose how the bullet determines its firing direction.")]
    public FireMode fireMode = FireMode.InitialDirection;
    [Tooltip("When true and FireMode == AimAtPlayer the bullet will aim at the player's current position at spawn time.")]
    public bool aimAtPlayer = true;
    public Vector2 initialDirection = Vector2.right;

    [Header("Damage / life")]
    public float damage = 10f;
    public float lifeTime = 5f;

    [Header("Optional FX")]
    public GameObject hitParticle;

    // Optional: which layers should destroy the bullet on contact (e.g. Ground, Default).
    // Leave empty (all bits 0) to fall back to default behaviour (destroy on non-enemy collisions).
    public LayerMask destroyOnLayers;

    private Rigidbody2D _rb;
    private bool _dead = false;

    // Optional externally-provided aiming target. If set via SetTargetPosition, it takes precedence when FireMode==ExplicitTarget.
    private bool _hasExplicitTarget = false;
    private Vector2 _explicitTarget;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody2D>();

        // bullets should not be affected by gravity by default
        _rb.gravityScale = 0f;

        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// Call immediately after Instantiate to force the bullet to aim at this world position.
    /// Use when FireMode == ExplicitTarget.
    /// </summary>
    public void SetTargetPosition(Vector2 worldPosition)
    {
        _explicitTarget = worldPosition;
        _hasExplicitTarget = true;
    }

    private void Start()
    {
        Vector2 dir = Vector2.zero;

        switch (fireMode)
        {
            case FireMode.Left:
                dir = Vector2.left;
                break;
            case FireMode.Right:
                dir = Vector2.right;
                break;
            case FireMode.Up:
                dir = Vector2.up;
                break;
            case FireMode.Down:
                dir = Vector2.down;
                break;
            case FireMode.ExplicitTarget:
                if (_hasExplicitTarget)
                {
                    dir = _explicitTarget - (Vector2)transform.position;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                    else dir.Normalize();
                }
                else
                {
                    // Fallback to initialDirection if explicit target not provided
                    Debug.LogWarning($"TurretBullet: FireMode is ExplicitTarget but no explicit target set for '{gameObject.name}'. Falling back.");
                    fireMode = FireMode.InitialDirection;
                }
                break;
            case FireMode.AimAtPlayer:
                if (aimAtPlayer)
                {
                    var playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null)
                    {
                        dir = (Vector2)playerObj.transform.position - (Vector2)transform.position;
                        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                        else dir.Normalize();
                    }
                    else
                    {
                        Debug.LogWarning("TurretBullet: Player not found while using AimAtPlayer. Falling back to InitialDirection.");
                        fireMode = FireMode.InitialDirection;
                    }
                }
                else
                {
                    fireMode = FireMode.InitialDirection;
                }
                break;
            case FireMode.InitialDirection:
            default:
                if (initialDirection != Vector2.zero && initialDirection.sqrMagnitude > 0.0001f)
                    dir = initialDirection.normalized;
                else
                    dir = transform.right; // final fallback
                break;
        }

        // If dir still zero for some reason, ensure a fallback
        if (dir == Vector2.zero)
            dir = transform.right;

        _rb.velocity = dir * speed;

        // rotate sprite to face movement direction (2D Z-rotation)
        float rot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, rot - 90f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject other)
    {
        if (other == null || _dead) return;

        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");

        bool isPlayerLayer = (playerLayer >= 0 && other.layer == playerLayer);
        if (playerLayer < 0 && other.CompareTag("Player"))
            isPlayerLayer = true;

        bool isGroundLayer = (groundLayer >= 0 && other.layer == groundLayer);

        if (isPlayerLayer)
        {
            _dead = true;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.enabled = false;

            float[] attackDetails = new float[2];
            attackDetails[0] = damage;
            attackDetails[1] = transform.position.x;
            other.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);

            if (hitParticle != null)
                Instantiate(hitParticle, transform.position, Quaternion.identity);

            Destroy(gameObject, 0.01f);
            return;
        }

        if (isGroundLayer)
        {
            _dead = true;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.enabled = false;

            if (hitParticle != null)
                Instantiate(hitParticle, transform.position, Quaternion.identity);

            Destroy(gameObject, 0.01f);
            return;
        }
    }
}