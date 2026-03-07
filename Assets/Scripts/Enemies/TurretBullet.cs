using System.Collections;
using UnityEngine;

// Simple 2D projectile for the boss.
// - Attach to the bullet prefab (requires Rigidbody2D and Collider2D).
// - Recommended: set the bullet Collider2D IsTrigger = true and Rigidbody2D.gravityScale = 0.
public class TurretBullet : MonoBehaviour
{
    [Header("Motion")]
    public float speed = 10f;
    public bool aimAtPlayer = true; // retained for compatibility but no longer causes bullets to home
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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody2D>();

        // bullets should not be affected by gravity by default
        _rb.gravityScale = 0f;

        Destroy(gameObject, lifeTime);
    }

    private void Start()
    {
        // New behavior: always fire in the initial direction (or prefab/spawn rotation)
        // The bullet will not chase the player. This ensures bullets travel straight until they hit something or time out.
        Vector2 dir;

        // If an explicit initialDirection is provided (non-zero), use it.
        if (initialDirection != Vector2.zero)
        {
            dir = initialDirection.normalized;
        }
        else
        {
            // Use the bullet's local right vector as the firing direction (respects spawn rotation)
            dir = transform.right;
        }

        _rb.velocity = dir * speed;
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

        // resolve layer indices (may be -1 if layer not defined)
        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");

        // Only act when collider's layer is Player or Ground.
        // If those layers are not defined in project, fall back to tag check for Player.
        bool isPlayerLayer = (playerLayer >= 0 && other.layer == playerLayer);
        if (playerLayer < 0 && other.CompareTag("Player"))
            isPlayerLayer = true;

        bool isGroundLayer = (groundLayer >= 0 && other.layer == groundLayer);

        if (isPlayerLayer)
        {
            _dead = true; // prevent multiple triggers
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

        // Otherwise ignore the collision (do not destroy).
        // This makes the bullet persist unless it hits Player or Ground layers.
    }
}