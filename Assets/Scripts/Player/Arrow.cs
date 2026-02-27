using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Arrow : MonoBehaviour
{
    Rigidbody2D rb;
    bool hasHit;

    // Legacy inspector fields - will be overridden by ArrowType if assigned.
    [Header("Legacy (overridden by ArrowType if set)")]
    public float damage = 1f;
    public LayerMask groundLayer;

    // New: assign an ArrowType ScriptableObject on the prefab (or set at runtime).
    [Header("Data")]
    public ArrowType arrowType;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Apply ArrowType data if present
        if (arrowType != null)
        {
            damage = arrowType.damage;
            groundLayer = arrowType.groundLayer;

            if (rb != null)
                rb.gravityScale = arrowType.gravityScale;

            // optional: set sprite if SpriteRenderer exists and arrowType.sprite assigned
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && arrowType.sprite != null)
                sr.sprite = arrowType.sprite;
        }
    }

    void Update()
    {
        if (hasHit == false && rb != null)
        {
            float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHit) return;

        Collider2D col = collision.collider;

        // Check for ground colliders including Tilemap/Composite and respect LayerMask
        if (IsGroundCollider(col))
        {
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        // Find enemy target (tries components first, then tag fallback)
        GameObject enemyTarget = FindEnemyTarget(col);
        if (enemyTarget != null)
        {
            ApplyHitToEnemy(enemyTarget);
            return;
        }

        // Non-enemy collisions: default behavior
        hasHit = true;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }

        if (arrowType != null && arrowType.impactVFX != null)
        {
            Instantiate(arrowType.impactVFX, transform.position, Quaternion.identity);
        }

        Destroy(gameObject, 0.01f);
    }

    // Use this if the arrow collider is a trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        // Check for ground colliders including Tilemap/Composite and respect LayerMask
        if (IsGroundCollider(other))
        {
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        GameObject enemyTarget = FindEnemyTarget(other);
        if (enemyTarget != null)
        {
            ApplyHitToEnemy(enemyTarget);
            return;
        }
    }

    // Returns true if the collider is considered ground (Tilemap, Composite, Box, or other) AND its layer is included in groundLayer
    private bool IsGroundCollider(Collider2D col)
    {
        if (col == null) return false;

        // First, quick LayerMask check
        int layer = col.gameObject.layer;
        if ((groundLayer.value & (1 << layer)) == 0)
            return false;

        // Then check for common ground collider types or tilemap presence
        if (col.GetComponent<BoxCollider2D>() != null) return true;
        if (col.GetComponent<EdgeCollider2D>() != null) return true;
        if (col.GetComponent<TilemapCollider2D>() != null) return true;
        if (col.GetComponent<CompositeCollider2D>() != null) return true;

        // Tilemap component may be on the same GameObject or a parent
        if (col.GetComponent<Tilemap>() != null || col.GetComponentInParent<Tilemap>() != null) return true;

        // Fallback: treat any Collider2D on a ground-layer as ground
        return true;
    }

    // Tries to find a suitable enemy GameObject to send Damage to.
    // Priority:
    //  1) Known enemy controller components on parents (Basic/Chasing/AirChasing)
    //  2) Walk up and return first GameObject with tag "Enemy"
    private GameObject FindEnemyTarget(Collider2D col)
    {
        if (col == null) return null;

        // 1) component checks (explicit)
        var basic = col.GetComponentInParent<BasicEnemyController>();
        if (basic != null) return basic.gameObject;

        var chasing = col.GetComponentInParent<ChasingEnemyController>();
        if (chasing != null) return chasing.gameObject;

        var air = col.GetComponentInParent<AirChasingEnemyController>();
        if (air != null) return air.gameObject;

        // add any other enemy controller types here...

        // 2) tag fallback: walk up parent chain and return first GameObject with tag "Enemy"
        Transform t = col.transform;
        while (t != null)
        {
            if (t.gameObject.CompareTag("Enemy"))
                return t.gameObject;
            t = t.parent;
        }

        // nothing found
        return null;
    }

    // Centralized logic to apply hit behavior and send Damage message
    private void ApplyHitToEnemy(GameObject enemyObject)
    {
        hasHit = true;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }

        float[] attackDetails = new float[2];
        attackDetails[0] = damage;
        attackDetails[1] = transform.position.x;

        // Use SendMessage so the enemy script's private Damage method receives it.
        enemyObject.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);

        if (arrowType != null && arrowType.impactVFX != null)
            Instantiate(arrowType.impactVFX, transform.position, Quaternion.identity);

        Destroy(gameObject, 0.01f);
    }

    // Public API to set the ArrowType at runtime (call before letting it move, e.g. right after Instantiate)
    public void SetType(ArrowType type)
    {
        arrowType = type;
        if (arrowType != null)
        {
            damage = arrowType.damage;
            groundLayer = arrowType.groundLayer;
            if (rb != null)
                rb.gravityScale = arrowType.gravityScale;

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && arrowType.sprite != null)
                sr.sprite = arrowType.sprite;
        }
    }

    // Public API to tell the arrow what counts as ground (keeps backwards compatibility)
    public void WhatisGround(LayerMask mask)
    {
        groundLayer = mask;
    }

    public void WhatisGround(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            groundLayer = 1 << layer;
    }

    public void WhatisGround(int layerIndex)
    {
        if (layerIndex >= 0 && layerIndex < 32)
            groundLayer = 1 << layerIndex;
    }
}
