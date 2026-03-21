using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

    private void OnDrawGizmos()
    {
        if (arrowType == null || !arrowType.isExplosive || !arrowType.showExplosionRadiusGizmo)
            return;

        float radius = Mathf.Max(0f, arrowType.explosionRadius);
        if (radius <= 0f)
            return;

        Gizmos.color = arrowType.explosionGizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);
    }

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

        // --- CHECK ENEMY FIRST ---
        GameObject enemyTarget = FindEnemyTarget(col);
        if (enemyTarget != null)
        {
            ApplyHitToEnemy(enemyTarget);
            return;
        }

        // Then check for ground colliders including Tilemap/Composite and respect LayerMask
        if (IsGroundCollider(col))
        {
            Debug.Log($"Arrow hit ground-like object '{col.gameObject.name}' (layer {col.gameObject.layer})");
            hasHit = true;
            Destroy(gameObject);
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

        // --- CHECK ENEMY FIRST ---
        GameObject enemyTarget = FindEnemyTarget(other);
        if (enemyTarget != null)
        {
            ApplyHitToEnemy(enemyTarget);
            return;
        }

        // Check for ground colliders including Tilemap/Composite and respect LayerMask
        if (IsGroundCollider(other))
        {
            Debug.Log($"Arrow trigger hit ground-like object '{other.gameObject.name}' (layer {other.gameObject.layer})");
            hasHit = true;

            if (arrowType != null && arrowType.isExplosive)
                ExplodeAt(transform.position);

            Destroy(gameObject);
            return;
        }
    }

    // Returns true if the collider is considered ground (Tilemap, Composite, Box, or other) AND its layer is included in groundLayer
    // All specific collider type checks replaced by generic Collider2D logic.
    private bool IsGroundCollider(Collider2D col)
    {
        if (col == null) return false;

        // If this collider belongs to an enemy, do not treat as ground
        if (col.gameObject.CompareTag("Enemy"))
            return false;

        // First, quick LayerMask check
        int layer = col.gameObject.layer;
        if ((groundLayer.value & (1 << layer)) == 0)
            return false;

        // If it has any Collider2D (the provided col qualifies) treat as ground.
        if (col.GetComponent<Collider2D>() != null) return true;

        // Additionally check for Tilemap on the object or parent (keeps prior behavior)
        if (col.GetComponent<Tilemap>() != null || col.GetComponentInParent<Tilemap>() != null) return true;

        // Fallback: treat any Collider2D on a ground-layer as ground
        return true;
    }

    // Tries to find a suitable enemy GameObject to send Damage to.
    // Now: only uses the "Enemy" tag � stops checking for specific enemy scripts.
    private GameObject FindEnemyTarget(Collider2D col)
    {
        if (col == null) return null;

        // Walk up parent chain and return first GameObject with tag "Enemy"
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

        Debug.Log($"Arrow attempting to apply {damage} damage to '{enemyObject.name}' (tag:{enemyObject.tag}, layer:{enemyObject.layer})");

        bool damageInvoked = TryInvokeDamageOnHierarchy(enemyObject, attackDetails);

        if (damageInvoked)
            Debug.Log($"Arrow: damage delivered to '{enemyObject.name}'");
        else
            Debug.LogWarning($"Arrow: no damage method detected on '{enemyObject.name}' or its children/parents � no damage applied.");

        if (arrowType != null && arrowType.isExplosive)
            ExplodeAt(transform.position);

        if (arrowType != null && arrowType.impactVFX != null)
            Instantiate(arrowType.impactVFX, transform.position, Quaternion.identity);

        Destroy(gameObject, 0.01f);
    }

    private void ExplodeAt(Vector2 center)
    {
        if (arrowType == null || !arrowType.isExplosive)
            return;

        float radius = Mathf.Max(0f, arrowType.explosionRadius);
        float explosionDamage = damage * Mathf.Max(0f, arrowType.explosionDamageMultiplier);

        if (radius <= 0f || explosionDamage <= 0f)
            return;

        if (arrowType.explosionVFX != null)
            Instantiate(arrowType.explosionVFX, center, Quaternion.identity);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
                continue;

            GameObject enemyTarget = FindEnemyTarget(col);
            if (enemyTarget == null)
                continue;

            float[] attackDetails = new float[2];
            attackDetails[0] = explosionDamage;
            attackDetails[1] = center.x;

            TryInvokeDamageOnHierarchy(enemyTarget, attackDetails);
        }
    }

    // Attempts to find and invoke a Damage method on the target's components (children and parents are searched).
    // Supports signatures:
    //   void Damage(float[])         <-- preferred (original project)
    //   void Damage(float) or Damage(int)
    //   void Damage()                <-- no-arg fallback
    private bool TryInvokeDamageOnHierarchy(GameObject target, float[] details)
    {
        if (target == null) return false;

        // Search target and all children first
        var components = target.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var comp in components)
        {
            if (comp == null) continue;
            var mi = comp.GetType().GetMethod("Damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) continue;

            var ps = mi.GetParameters();
            if (ps.Length == 1)
            {
                var pType = ps[0].ParameterType;
                if (pType == typeof(float[]))
                {
                    mi.Invoke(comp, new object[] { details });
                    return true;
                }
                if (pType == typeof(float) || pType == typeof(System.Single))
                {
                    mi.Invoke(comp, new object[] { details[0] });
                    return true;
                }
                if (pType == typeof(int) || pType == typeof(System.Int32))
                {
                    mi.Invoke(comp, new object[] { (int)details[0] });
                    return true;
                }
                if (pType == typeof(object))
                {
                    mi.Invoke(comp, new object[] { (object)details });
                    return true;
                }
            }
            else if (ps.Length == 0)
            {
                // no-arg Damage()
                mi.Invoke(comp, null);
                return true;
            }
        }

        // If not found in children, search parents upward (GetComponentsInParent includes self; we've covered self � so skip)
        var parent = target.transform.parent;
        while (parent != null)
        {
            var parentComps = parent.GetComponents<MonoBehaviour>();
            foreach (var comp in parentComps)
            {
                if (comp == null) continue;
                var mi = comp.GetType().GetMethod("Damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi == null) continue;

                var ps = mi.GetParameters();
                if (ps.Length == 1)
                {
                    var pType = ps[0].ParameterType;
                    if (pType == typeof(float[]))
                    {
                        mi.Invoke(comp, new object[] { details });
                        return true;
                    }
                    if (pType == typeof(float) || pType == typeof(System.Single))
                    {
                        mi.Invoke(comp, new object[] { details[0] });
                        return true;
                    }
                    if (pType == typeof(int) || pType == typeof(System.Int32))
                    {
                        mi.Invoke(comp, new object[] { (int)details[0] });
                        return true;
                    }
                    if (pType == typeof(object))
                    {
                        mi.Invoke(comp, new object[] { (object)details });
                        return true;
                    }
                }
                else if (ps.Length == 0)
                {
                    mi.Invoke(comp, null);
                    return true;
                }
            }
            parent = parent.parent;
        }

        return false;
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
