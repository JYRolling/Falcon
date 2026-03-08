using System.Collections;
using UnityEngine;

// Simple 2D projectile for the boss.
// - Attach to the bullet prefab (requires Rigidbody2D and Collider2D).
// - Recommended: set the bullet Collider2D IsTrigger = true and Rigidbody2D.gravityScale = 0.
public class BossBullet : MonoBehaviour
{
    [Header("Motion")]
    public float speed = 10f;
    [Tooltip("When true the bullet will aim at the player's current position at spawn time (or use SetTargetPosition).")]
    public bool aimAtPlayer = true;
    public Vector2 initialDirection = Vector2.right;

    [Header("Damage / life")]
    public float damage = 10f;
    public float lifeTime = 5f;

    [Header("Optional FX")]
    public GameObject hitParticle;

    public LayerMask destroyOnLayers;

    private Rigidbody2D _rb;
    private bool _dead = false;

    // Optional externally-provided aiming target. If set via SetTargetPosition, it takes precedence as a fixed target.
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

    public void SetTargetPosition(Vector2 worldPosition)
    {
        _explicitTarget = worldPosition;
        _hasExplicitTarget = true;
    }

    private void Start()
    {
        // Determine target position (explicit target takes precedence)
        Vector2 targetPos = Vector2.zero; // initialize to avoid CS0165
        bool haveTarget = false;

        if (_hasExplicitTarget)
        {
            targetPos = _explicitTarget;
            haveTarget = true;
        }
        else if (aimAtPlayer)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                targetPos = playerObj.transform.position;
                haveTarget = true;
            }
        }

        Vector2 dir;

        if (haveTarget)
        {
            dir = targetPos - (Vector2)transform.position;
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.right;
            else
                dir.Normalize();
        }
        else if (initialDirection != Vector2.zero && initialDirection.sqrMagnitude > 0.0001f)
        {
            dir = initialDirection.normalized;
        }
        else
        {
            dir = transform.right;
        }

        // Set velocity toward the chosen target (single-shot, not homing)
        _rb.velocity = dir * speed;

        // Match the rotation logic used in your EnemyBulletScript (visual orientation)
        float rot = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, rot + 90f);
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
    }
}