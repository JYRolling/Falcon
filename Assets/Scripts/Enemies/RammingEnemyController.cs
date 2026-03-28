using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RammingEnemyController : MonoBehaviour
{
    private enum State { Preparing, Dashing, Waiting, Dead }

    [Header("Core")]
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private Transform wallCheck; // transform used for raycast-based wall detection
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float waitTimeAfterHit = 1.0f;
    [SerializeField] private Transform playerTransform; // optional, falls back to tag "Player"

    [Header("Health (fallback)")]
    [SerializeField] private float maxHealth = 20f;

    [Header("Boss (optional)")]
    [SerializeField] private BossStats bossStats; // if present, will be used for health + UI

    [Header("Optional: scene transition on boss defeat")]
    [Tooltip("Name of scene to load when this boss is defeated. Leave empty to disable.")]
    [SerializeField] private string sceneToLoadOnDefeat = "";
    [Tooltip("Delay (seconds) before loading the scene after boss death.")]
    [SerializeField] private float sceneLoadDelay = 1.0f;

    [Header("Touch Damage (Boss-like)")]
    [SerializeField] private Transform touchDamageCheck;
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private float touchDamage = 10f; // fallback if no BossStats
    [SerializeField] private float touchDamageCooldown = 1f; // fallback
    [SerializeField] private float touchDamageWidth = 1f; // fallback
    [SerializeField] private float touchDamageHeight = 1f; // fallback

    private Rigidbody2D _rb;
    private int _facingDirection = 1;
    private State _state = State.Preparing;
    private float _currentHealth;

    // touch damage runtime
    private float lastTouchDamageTime = -999f;
    private Vector2 touchDamageBotLeft;
    private Vector2 touchDamageTopRight;
    private float[] attackDetails = new float[2];

    // wallCheck flip helpers
    private bool _wallCheckIsChild = false;
    private Vector3 _wallCheckInitialLocalPos = Vector3.zero;
    private Vector3 _wallCheckInitialOffset = Vector3.zero;
    private Vector3 _wallCheckInitialLocalScale = Vector3.one;
    private Vector3 _wallCheckInitialWorldScale = Vector3.one;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
            Debug.LogWarning("RammingEnemyController: Rigidbody2D missing - add one for proper physics movement.");

        _currentHealth = maxHealth;
    }

    private void OnDestroy()
    {
        // ensure UI is unregistered if boss was using it
        BossHealthBar.Instance?.UnregisterBoss();
    }

    private void Start()
    {
        // cache wallCheck initial position/offset/scale so we can mirror or flip transform when facing changes
        if (wallCheck != null)
        {
            _wallCheckIsChild = wallCheck.IsChildOf(transform);
            if (_wallCheckIsChild)
            {
                _wallCheckInitialLocalPos = wallCheck.localPosition;
                _wallCheckInitialLocalScale = wallCheck.localScale;
            }
            else
            {
                _wallCheckInitialOffset = wallCheck.position - transform.position;
                _wallCheckInitialWorldScale = wallCheck.localScale;
            }
        }

        // BossStats setup (optional)
        if (bossStats == null)
        {
            bossStats = GetComponent<BossStats>();
            if (bossStats != null)
            {
                bossStats.Initialize();
                BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);
            }
        }
        else
        {
            bossStats.Initialize();
            BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);
        }

        if (playerTransform == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTransform = go.transform;
        }

        // face the player horizontally at start (if available)
        if (playerTransform != null)
            _facingDirection = (playerTransform.position.x >= transform.position.x) ? 1 : -1;

        ApplyVisualFacing();
        StartDash();
    }

    private void Update()
    {
        // Check touch damage every frame like BossEnemyController
        CheckTouchDamage();
    }

    private void FixedUpdate()
    {
        if (_state == State.Dashing)
        {
            // Raycast-based wall check from wallCheck (or from transform if null)
            Vector2 origin = (wallCheck != null) ? (Vector2)wallCheck.position : (Vector2)transform.position;
            Vector2 dir = (_facingDirection == 1) ? Vector2.right : Vector2.left;

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, wallCheckDistance, whatIsGround);
            if (hit.collider != null)
            {
                // treat as wall hit
                if (_rb != null) _rb.velocity = Vector2.zero;
                _state = State.Waiting;
                StopAllCoroutines();
                StartCoroutine(WaitThenDash());
                return;
            }

            // move horizontally; use MovePosition for consistent physics
            if (_rb != null)
            {
                Vector2 next = _rb.position + Vector2.right * (_facingDirection * dashSpeed * Time.fixedDeltaTime);
                _rb.MovePosition(next);
            }
            else
            {
                transform.Translate(Vector3.right * (_facingDirection * dashSpeed * Time.fixedDeltaTime), Space.World);
            }
        }
    }

    private void StartDash()
    {
        if (_state == State.Dead) return;
        _state = State.Dashing;

        // zero vertical velocity for a pure horizontal ram
        if (_rb != null)
            _rb.velocity = Vector2.zero;
    }

    private IEnumerator WaitThenDash()
    {
        _state = State.Waiting;
        // stop movement
        if (_rb != null) _rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(waitTimeAfterHit);

        // flip direction and start dashing again
        _facingDirection *= -1;
        ApplyVisualFacing();
        StartDash();
    }

    private void ApplyVisualFacing()
    {
        // flip by euler Y like other scripts in project (this flips the whole enemy)
        Vector3 e = transform.eulerAngles;
        e.y = (_facingDirection == 1) ? 0f : 180f;
        transform.eulerAngles = e;

        // --- flip the wallCheck's transform (scale/rotation) rather than moving its object ---
        if (wallCheck == null) return;

        if (_wallCheckIsChild)
        {
            // If wallCheck is a child, prefer flipping its localScale.x so its transform is mirrored
            Vector3 s = _wallCheckInitialLocalScale;
            s.x = Mathf.Abs(s.x) * (_facingDirection == 1 ? 1f : -1f);
            wallCheck.localScale = s;

            // keep localPosition unchanged (child will follow parent's rotation for world position)
            wallCheck.localPosition = _wallCheckInitialLocalPos;
        }
        else
        {
            // For an external wallCheck (not child) we flip its transform by:
            // 1) mirroring its offset around the enemy (so it stays on the front side)
            // 2) flipping its localScale.x so its orientation is mirrored
            Vector3 mirroredOffset = _wallCheckInitialOffset;
            mirroredOffset.x = Mathf.Abs(mirroredOffset.x) * _facingDirection;
            wallCheck.position = transform.position + mirroredOffset;

            Vector3 ws = _wallCheckInitialWorldScale;
            ws.x = Mathf.Abs(ws.x) * (_facingDirection == 1 ? 1f : -1f);
            wallCheck.localScale = ws;

            // also rotate the wallCheck 180 degrees around Y when facing left so its forward vector aligns
            wallCheck.rotation = Quaternion.Euler(0f, (_facingDirection == 1 ? 0f : 180f), 0f);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_state != State.Dashing) return;
        if (collision == null) return;

        // ensure we only treat collisions against ground/walls per layer mask
        int otherLayer = collision.gameObject.layer;
        if ((whatIsGround.value & (1 << otherLayer)) == 0) return;

        // check contact normals to detect roughly horizontal impact (a wall)
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > 0.5f)
            {
                // stop dash and begin wait+flip routine
                if (_rb != null) _rb.velocity = Vector2.zero;
                _state = State.Waiting;
                StopAllCoroutines();
                StartCoroutine(WaitThenDash());
                break;
            }
        }
    }

    // public damage API so player/projectiles can kill this enemy
    public void Damage(float amount)
    {
        if (_state == State.Dead) return;

        // prefer BossStats if present
        if (bossStats != null)
        {
            bool died = bossStats.ApplyDamage(amount);
            if (died) Die();
            return;
        }

        // fallback
        _currentHealth -= amount;
        if (_currentHealth <= 0f) Die();
    }

    private void Die()
    {
        if (_state == State.Dead) return;
        _state = State.Dead;

        // death VFX from BossStats if available
        if (bossStats != null)
        {
            if (bossStats.deathChunkParticle) Instantiate(bossStats.deathChunkParticle, transform.position, bossStats.deathChunkParticle.transform.rotation);
            if (bossStats.deathBloodParticle) Instantiate(bossStats.deathBloodParticle, transform.position, bossStats.deathBloodParticle.transform.rotation);
        }

        // unregister UI
        BossHealthBar.Instance?.UnregisterBoss();

        if (!string.IsNullOrEmpty(sceneToLoadOnDefeat))
            StartCoroutine(LoadSceneAfterDelay(sceneToLoadOnDefeat, sceneLoadDelay));
        else
            Destroy(gameObject);
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    // --- Touch damage implementation (mirrors BossEnemyController) ---
    private void CheckTouchDamage()
    {
        float cooldown = (bossStats != null) ? bossStats.touchDamageCooldown : touchDamageCooldown;
        if (Time.time >= lastTouchDamageTime + cooldown && touchDamageCheck != null)
        {
            float width = (bossStats != null) ? bossStats.touchDamageWidth : touchDamageWidth;
            float height = (bossStats != null) ? bossStats.touchDamageHeight : touchDamageHeight;

            touchDamageBotLeft.Set(touchDamageCheck.position.x - (width / 2), touchDamageCheck.position.y - (height / 2));
            touchDamageTopRight.Set(touchDamageCheck.position.x + (width / 2), touchDamageCheck.position.y + (height / 2));

            float damageAmount = (bossStats != null) ? bossStats.touchDamage : touchDamage;

            Collider2D hit = Physics2D.OverlapArea(touchDamageBotLeft, touchDamageTopRight, whatIsPlayer);
            if (hit != null)
            {
                lastTouchDamageTime = Time.time;
                attackDetails[0] = damageAmount;
                attackDetails[1] = transform.position.x;
                hit.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    // --- Editor gizmos to visualize touch area and wall check ---
    private void OnDrawGizmos()
    {
        // Touch damage area (magenta)
        if (touchDamageCheck != null)
        {
            float width = (bossStats != null) ? bossStats.touchDamageWidth : touchDamageWidth;
            float height = (bossStats != null) ? bossStats.touchDamageHeight : touchDamageHeight;

            Vector3 pos = touchDamageCheck.position;
            Vector3 bl = new Vector3(pos.x - (width / 2f), pos.y - (height / 2f), pos.z);
            Vector3 br = new Vector3(pos.x + (width / 2f), pos.y - (height / 2f), pos.z);
            Vector3 tr = new Vector3(pos.x + (width / 2f), pos.y + (height / 2f), pos.z);
            Vector3 tl = new Vector3(pos.x - (width / 2f), pos.y + (height / 2f), pos.z);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }

        // Wall check ray (yellow)
        Vector3 origin = (wallCheck != null) ? wallCheck.position : transform.position;
        Vector3 dir = (_facingDirection == 1) ? Vector3.right : Vector3.left;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + dir * wallCheckDistance);
        Gizmos.DrawWireSphere(origin + dir * wallCheckDistance, 0.05f);
    }
}