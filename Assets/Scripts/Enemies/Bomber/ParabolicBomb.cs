using System.Collections;
using UnityEngine;

/// <summary>
/// Bomb projectile: expects a Rigidbody2D on the prefab.  
/// - Applies damage via SendMessage("Damage", float[] { damage, bombX }) to hits inside explosionRadius.
/// - Optionally explodes on contact with player or ground.  
/// - Self-destructs after lifetime.
/// - Optional translucent runtime indicator shows explosionRadius so players can dodge.
/// </summary>
public class ParabolicBomb : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damage = 15f;
    [SerializeField] private float explosionRadius = 0.6f;
    [SerializeField] private LayerMask damageMask; // assign to 'Player' layer (or layers that should receive damage)

    [Header("Collision/Timing")]
    [SerializeField] private bool explodeOnContact = true; // explode when hitting a collider in damageMask
    [SerializeField] private LayerMask groundMask; // layers treated as ground for explodeOnGround
    [SerializeField] private bool explodeOnGround = true;
    [SerializeField] private float lifetime = 6f;

    [Header("FX / Audio")]
    [SerializeField] private GameObject explosionParticle;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float soundVolume = 1f;

    [Header("Gravity")]
    [SerializeField] private float extraGravityMultiplier = 0f; // 0 = no extra, 1 = add one extra Physics2D.gravity per second

    [Header("Runtime Indicator (optional)")]
    [Tooltip("When true the bomb creates a translucent red indicator showing the explosion radius while alive.")]
    [SerializeField] private bool showRuntimeIndicator = true;
    [Tooltip("Alpha of the indicator color (0..1)")]
    [SerializeField, Range(0f, 1f)] private float indicatorAlpha = 0.22f;
    [Tooltip("Number of segments used to build the indicator mesh (higher = smoother).")]
    [SerializeField, Range(8, 64)] private int indicatorSegments = 32;
    [Tooltip("Vertical offset of the indicator relative to bomb (so it renders slightly above/below).")]
    [SerializeField] private float indicatorYOffset = -0.01f;

    private Rigidbody2D _rb;

    // runtime indicator objects
    private GameObject _indicatorGO;
    private Mesh _indicatorMesh;
    private Material _indicatorMaterial;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);

        if (showRuntimeIndicator)
            CreateIndicator();
    }

    private void FixedUpdate()
    {
        // Apply extra gravity independent of Rigidbody2D.gravityScale.
        // This lets you increase/decrease falling acceleration without touching the prefab gravityScale.
        if (_rb != null && !Mathf.Approximately(extraGravityMultiplier, 0f))
        {
            // Physics2D.gravity is acceleration; multiply by multiplier and integrate over fixed time step
            _rb.velocity += Physics2D.gravity * extraGravityMultiplier * Time.fixedDeltaTime;
        }

        // keep indicator positioned & scaled
        if (_indicatorGO != null)
            UpdateIndicatorTransform();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // explode on contact with configured damageMask layer
        if (explodeOnContact && IsInLayerMask(collision.gameObject, damageMask))
        {
            Explode();
            return;
        }

        // explode when hitting ground (if enabled)
        if (explodeOnGround && IsInLayerMask(collision.gameObject, groundMask))
        {
            Explode();
            return;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // also support trigger collisions
        if (explodeOnContact && IsInLayerMask(other.gameObject, damageMask))
        {
            Explode();
            return;
        }

        if (explodeOnGround && IsInLayerMask(other.gameObject, groundMask))
        {
            Explode();
            return;
        }
    }

    private void Explode()
    {
        // destroy indicator immediately
        DestroyIndicator();

        // spawn FX
        if (explosionParticle != null)
            Instantiate(explosionParticle, transform.position, Quaternion.identity);

        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, soundVolume);

        // damage everything inside explosionRadius that matches damageMask
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, damageMask);
        if (hits != null && hits.Length > 0)
        {
            float[] attackDetails = new float[2];
            attackDetails[0] = damage;
            attackDetails[1] = transform.position.x;

            foreach (var c in hits)
            {
                if (c == null) continue;
                // send Damage message; don't require receiver to avoid exceptions
                c.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);
            }
        }

        Destroy(gameObject);
    }

    private static bool IsInLayerMask(GameObject go, LayerMask mask)
    {
        if (go == null) return false;
        return ((mask.value & (1 << go.layer)) != 0);
    }

    private void OnDrawGizmosSelected()
    {
        // editor-only wireframe as before
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    // ---------------- Indicator helpers ----------------

    private void CreateIndicator()
    {
        // avoid duplicates
        if (_indicatorGO != null) return;

        _indicatorGO = new GameObject("ExplosionIndicator");
        _indicatorGO.transform.SetParent(transform, false);
        _indicatorGO.transform.localPosition = new Vector3(0f, indicatorYOffset, 0f);
        _indicatorGO.transform.localRotation = Quaternion.identity;

        // add components
        var mf = _indicatorGO.AddComponent<MeshFilter>();
        var mr = _indicatorGO.AddComponent<MeshRenderer>();

        // create mesh and material
        _indicatorMesh = BuildCircleMesh(explosionRadius, indicatorSegments);
        mf.sharedMesh = _indicatorMesh;

        Shader spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        _indicatorMaterial = new Material(spriteShader);
        Color c = new Color(1f, 0f, 0f, indicatorAlpha);
        // For Sprites/Default shader, color is _Color
        if (_indicatorMaterial.HasProperty("_Color"))
            _indicatorMaterial.SetColor("_Color", c);
        else
            _indicatorMaterial.color = c;

        // make it render on top slightly (optional)
        _indicatorMaterial.renderQueue = 3000; // Transparent queue
        mr.sharedMaterial = _indicatorMaterial;

        // set scale to 1; mesh already built to radius size
        _indicatorGO.transform.localScale = Vector3.one;
    }

    private void UpdateIndicatorTransform()
    {
        if (_indicatorGO == null) return;

        // ensure indicator follows bomb position and uses current explosionRadius
        _indicatorGO.transform.position = new Vector3(transform.position.x, transform.position.y + indicatorYOffset, transform.position.z);

        // if explosionRadius changed, rebuild mesh (cheap for small segment counts)
        if (_indicatorMesh == null || !Mathf.Approximately(_indicatorMesh.bounds.extents.x, explosionRadius))
        {
            // rebuild mesh to match radius
            if (_indicatorMesh != null) Destroy(_indicatorMesh);
            _indicatorMesh = BuildCircleMesh(explosionRadius, indicatorSegments);
            var mf = _indicatorGO.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = _indicatorMesh;
        }
    }

    private void DestroyIndicator()
    {
        if (_indicatorGO != null)
        {
            Destroy(_indicatorGO);
            _indicatorGO = null;
        }
        if (_indicatorMesh != null)
        {
            Destroy(_indicatorMesh);
            _indicatorMesh = null;
        }
        if (_indicatorMaterial != null)
        {
            Destroy(_indicatorMaterial);
            _indicatorMaterial = null;
        }
    }

    private Mesh BuildCircleMesh(float radius, int segments)
    {
        Mesh m = new Mesh();
        m.name = "IndicatorMesh";

        int vCount = segments + 1;
        Vector3[] verts = new Vector3[vCount];
        Vector2[] uvs = new Vector2[vCount];
        int[] tris = new int[segments * 3];

        verts[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            verts[i + 1] = new Vector3(x, y, 0f);
            uvs[i + 1] = new Vector2((x / (radius * 2f)) + 0.5f, (y / (radius * 2f)) + 0.5f);
        }

        for (int i = 0; i < segments; i++)
        {
            int a = 0;
            int b = i + 1;
            int c = (i + 2 <= segments) ? (i + 2) : 1;
            int ti = i * 3;
            tris[ti] = a;
            tris[ti + 1] = b;
            tris[ti + 2] = c;
        }

        m.vertices = verts;
        m.uv = uvs;
        m.triangles = tris;
        m.RecalculateBounds();
        m.RecalculateNormals();
        return m;
    }

    private void OnDestroy()
    {
        DestroyIndicator();
    }
}