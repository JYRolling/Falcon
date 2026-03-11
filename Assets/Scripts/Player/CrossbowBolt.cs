using UnityEngine;

// Crossbow bolt projectile fired by CrossbowController.
// Damages enemies on hit and destroys itself on ground/enemy contact.
// Prefab setup: needs Rigidbody2D + Collider2D (IsTrigger recommended).
[RequireComponent(typeof(Rigidbody2D))]
public class CrossbowBolt : MonoBehaviour
{
    private float damage;
    private LayerMask groundLayer;
    private bool hasHit;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Called by CrossbowController immediately after Instantiate.
    public void Init(float dmg, Vector2 velocity, LayerMask groundLayerMask)
    {
        damage = dmg;
        groundLayer = groundLayerMask;

        if (rb != null)
        {
            rb.velocity = velocity;
            rb.gravityScale = 0f;
        }

        // Safety: destroy after 6 seconds if nothing was hit.
        Destroy(gameObject, 6f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        HandleContact(other.gameObject, other.transform);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHit) return;
        HandleContact(collision.gameObject, collision.transform);
    }

    private void HandleContact(GameObject hitObject, Transform hitTransform)
    {
        // Enemy check (walk up hierarchy same as Arrow.cs)
        GameObject enemy = FindEnemyInHierarchy(hitTransform);
        if (enemy != null)
        {
            hasHit = true;
            float[] attackDetails = new float[] { damage, transform.position.x };
            enemy.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);
            Destroy(gameObject);
            return;
        }

        // Ground check
        if ((groundLayer.value & (1 << hitObject.layer)) != 0)
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }

    private static GameObject FindEnemyInHierarchy(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Enemy"))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }
}
