using System.Collections;
using UnityEngine;
using Falcon.Utils;

/// <summary>
/// Stomper enemy:
/// - Repeatedly jumps toward the player's last-known position (or a fallback point)
/// - Deals touch damage when overlapping player on landing / contact
/// - Compatible with BossStats/BossHealthBar and Animator booleans: IsIdle, IsJumping
/// </summary>
public class StomperEnemyController : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Optional fallback target when player not found.")]
    [SerializeField] private Transform fallbackTarget;

    [Tooltip("If true, attempt to predict player's future position using Rigidbody2D.velocity")]
    [SerializeField] private bool predictPlayerMovement = false;
    [Tooltip("Prediction time (seconds) used when predictPlayerMovement is true.")]
    [SerializeField] private float predictionTime = 0.35f;

    [Header("Jump")]
    [SerializeField] private float jumpDuration = 0.9f;
    [SerializeField] private float jumpArcHeight = 2.0f;
    [SerializeField] private float pauseAfterLanding = 0.8f;

    [Header("Ground")]
    [Tooltip("Transform used as origin for ground checks (downward ray). If null the enemy is considered always grounded.")]
    [SerializeField] private Transform groundCheck;
    [Tooltip("Layer(s) considered ground")]
    [SerializeField] private LayerMask whatIsGround;
    [Tooltip("Distance for the downward ground raycast")]
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Behavior")]
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private float initialDelay = 0.2f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private GameObject deathParticle;
    [SerializeField] private string sceneToLoadOnDefeat = "";
    [SerializeField] private float sceneLoadDelay = 1f;

    [Header("Optional: Boss-style health UI")]
    [SerializeField] private BossStats bossStats;

    [Header("Optional Animator")]
    [Tooltip("Animator should expose boolean parameters: IsIdle, IsJumping")]
    [SerializeField] private Animator animator;

    [Header("Touch Damage")]
    [Tooltip("Transform used to check touch-damage area (center). If empty will use the enemy root.")]
    [SerializeField] private Transform touchDamageCheck;
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private float touchDamage = 10f;
    [SerializeField] private float touchDamageWidth = 1f;
    [SerializeField] private float touchDamageHeight = 1f;
    [SerializeField] private float touchDamageCooldown = 1.0f;

    // runtime
    private GameObject alive;
    private Transform _player;
    private Rigidbody2D _playerRb;
    private float _currentHealth;
    private bool _isRunning = false;
    private bool _isDead = false;

    // ground state
    private bool groundDetected = true;

    private float lastTouchDamageTime = 0f;
    private Vector2 touchDamageBotLeft;
    private Vector2 touchDamageTopRight;
    private float[] attackDetails = new float[2];

    // Rigidbody handling to avoid physics jitter when we set transform directly
    private Rigidbody2D _rb;
    private float _originalGravityScale;

    // keep a reference absolute scale to preserve sprite size when flipping
    private float _initialScaleX = 1f;

    private void Awake()
    {
    }

    private void Start()
    {
        alive = transform.Find("Alive")?.gameObject ?? gameObject;
        if (animator == null && alive != null) animator = alive.GetComponentInChildren<Animator>();

        if (touchDamageCheck == null)
            touchDamageCheck = alive != null ? alive.transform : transform;

        // if groundCheck not set, default to touchDamageCheck / alive so raycast origin exists
        if (groundCheck == null)
            groundCheck = alive != null ? alive.transform : transform;

        _currentHealth = maxHealth;

        var pgo = GameObject.FindGameObjectWithTag("Player");
        if (pgo != null)
        {
            _player = pgo.transform;
            _playerRb = pgo.GetComponent<Rigidbody2D>();
        }

        if (bossStats == null)
            bossStats = GetComponent<BossStats>();

        if (bossStats != null)
        {
            bossStats.Initialize();
            BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);
        }

        // Cache own Rigidbody2D (if present) so we can use MovePosition during jumps and temporarily suspend gravity.
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null)
        {
            _originalGravityScale = _rb.gravityScale;
        }

        // cache absolute x scale to preserve sprite size when flipping
        var flipTarget = alive != null ? alive.transform : transform;
        _initialScaleX = Mathf.Abs(flipTarget.localScale.x);
        if (_initialScaleX <= 0f) _initialScaleX = 1f;

        if (startOnAwake)
            StartPatternWithDelay(initialDelay);
    }

    private void Update()
    {
        UpdateGroundedState();
        CheckTouchDamage();
        UpdateFacing();
    }

    private void UpdateFacing()
    {
        // Always face the player if present; otherwise face fallback target if available
        if (_player != null)
        {
            FacePosition(_player.position);
            return;
        }
        if (fallbackTarget != null)
        {
            FacePosition(fallbackTarget.position);
            return;
        }
        // no target: keep current facing
    }

    private void FacePosition(Vector3 pos)
    {
        Transform flipTarget = alive != null ? alive.transform : transform;
        float dx = pos.x - transform.position.x;
        if (Mathf.Approximately(dx, 0f)) return;
        Vector3 sc = flipTarget.localScale;
        sc.x = (dx > 0f ? 1f : -1f) * _initialScaleX;
        flipTarget.localScale = sc;
    }

    private void UpdateGroundedState()
    {
        // If no groundCheck transform or no ground layer assigned, consider grounded to avoid blocking behavior.
        if (groundCheck == null || whatIsGround == 0)
        {
            groundDetected = true;
            return;
        }

        // cast a short ray downward to detect ground (matches pattern used elsewhere in project)
        groundDetected = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, whatIsGround);
        // Optional: visualize for debugging
        // Debug.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance, groundDetected ? Color.green : Color.red);
    }

    private void StartPatternWithDelay(float delay)
    {
        if (_isRunning || _isDead) return;
        _isRunning = true;
        StartCoroutine(PatternLoop(delay));
    }

    private IEnumerator PatternLoop(float initialDelaySec)
    {
        if (initialDelaySec > 0f) yield return new WaitForSeconds(initialDelaySec);

        while (!_isDead)
        {
            // wait until we're grounded before attempting next stomp
            yield return new WaitUntil(() => groundDetected || _isDead);

            Vector3 target = ChooseTargetPosition();

            // lock target at jump start (stomper locks to last-known)
            yield return StartCoroutine(PerformJump(transform.position, target));

            // landed: allow touch damage and idle
            SetAnimatorBool("IsIdle", true);
            yield return new WaitForSeconds(pauseAfterLanding);
            SetAnimatorBool("IsIdle", false);
        }
    }

    private Vector3 ChooseTargetPosition()
    {
        if (_player != null)
        {
            if (predictPlayerMovement && _playerRb != null)
            {
                return _player.position + (Vector3)(_playerRb.velocity * predictionTime);
            }
            return _player.position;
        }
        if (fallbackTarget != null) return fallbackTarget.position;
        return transform.position; // jump in place if nothing else
    }

    private IEnumerator PerformJump(Vector3 startPos, Vector3 endPos)
    {
        float t = 0f;
        float snapDistanceThreshold = 0.5f;

        // If close to configured startPos, snap via Rigidbody.MovePosition (if present) to avoid transform-driven teleport.
        if (Vector3.Distance(_rb != null ? (Vector3)_rb.position : transform.position, startPos) <= snapDistanceThreshold)
        {
            if (_rb != null)
                _rb.MovePosition(startPos);
            else
                transform.position = startPos;
        }
        else
        {
            startPos = _rb != null ? (Vector3)_rb.position : transform.position;
        }

        // mark as not grounded while jumping
        groundDetected = false;

        // If we have a Rigidbody2D, temporarily zero velocity and disable gravity so physics doesn't pull us during the scripted motion.
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            _rb.gravityScale = 0f;
            // Do NOT change bodyType; switching types can trigger solver snaps on restore.
        }

        SetAnimatorBool("IsJumping", true);
        SetAnimatorBool("IsIdle", false);

        // Remove midpoint flip: facing is handled continuously in UpdateFacing

        // Use FixedUpdate timing to move the Rigidbody via MovePosition for deterministic, smooth physics integration.
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float s = Mathf.Clamp01(elapsed / jumpDuration);
            Vector3 basePos = Vector3.Lerp(startPos, endPos, s);
            float arc = 4f * jumpArcHeight * s * (1f - s);
            Vector3 newPos = new Vector3(basePos.x, basePos.y + arc, basePos.z);

            if (_rb != null)
            {
                _rb.MovePosition(newPos);
            }
            else
            {
                transform.position = newPos;
            }

            yield return new WaitForFixedUpdate();
        }

        // ensure exact final pos
        if (_rb != null)
        {
            _rb.MovePosition(endPos);
            // give physics one fixed tick to reconcile
            yield return new WaitForFixedUpdate();

            // restore physics gravity and clear velocity so we don't get leftover motion
            _rb.gravityScale = _originalGravityScale;
            _rb.velocity = Vector2.zero;
        }
        else
        {
            transform.position = endPos;
        }

        SetAnimatorBool("IsJumping", false);

        // allow next frame's Update to re-evaluate grounded state
    }

    private void CheckTouchDamage()
    {
        if (touchDamageCheck == null) return;

        float cooldown = bossStats != null ? bossStats.touchDamageCooldown : touchDamageCooldown;
        if (Time.time < lastTouchDamageTime + cooldown) return;

        float width = bossStats != null ? bossStats.touchDamageWidth : touchDamageWidth;
        float height = bossStats != null ? bossStats.touchDamageHeight : touchDamageHeight;
        float dmg = bossStats != null ? bossStats.touchDamage : touchDamage;

        touchDamageBotLeft.Set(touchDamageCheck.position.x - (width / 2), touchDamageCheck.position.y - (height / 2));
        touchDamageTopRight.Set(touchDamageCheck.position.x + (width / 2), touchDamageCheck.position.y + (height / 2));

        Collider2D hit = Physics2D.OverlapArea(touchDamageBotLeft, touchDamageTopRight, whatIsPlayer);
        if (hit != null)
        {
            lastTouchDamageTime = Time.time;
            attackDetails[0] = dmg;
            attackDetails[1] = alive != null ? alive.transform.position.x : transform.position.x;
            hit.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);
        }
    }

    public void Damage(float[] attackDetailsIn)
    {
        if (_isDead) return;
        if (attackDetailsIn == null || attackDetailsIn.Length == 0) return;

        float dmg = attackDetailsIn[0];
        bool died = false;

        if (bossStats != null)
        {
            died = bossStats.ApplyDamage(dmg);
        }
        else
        {
            _currentHealth -= dmg;
            if (_currentHealth <= 0f) died = true;
        }

        if (died) Die();
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        BossHealthBar.Instance?.UnregisterBoss();
        GetComponent<EnemyWeaponDrop>()?.DropNow();

        if (deathParticle) Instantiate(deathParticle, transform.position, Quaternion.identity);

        if (!string.IsNullOrEmpty(sceneToLoadOnDefeat))
            StartCoroutine(LoadSceneAfterDelay(sceneToLoadOnDefeat, sceneLoadDelay));
        else
            Destroy(gameObject);
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void OnDrawGizmos()
    {
        if (fallbackTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(fallbackTarget.position, 0.12f);
        }

        if (touchDamageCheck != null)
        {
            float width = bossStats != null ? bossStats.touchDamageWidth : touchDamageWidth;
            float height = bossStats != null ? bossStats.touchDamageHeight : touchDamageHeight;

            Vector2 botLeft = new Vector2(touchDamageCheck.position.x - (width / 2), touchDamageCheck.position.y - (height / 2));
            Vector2 botRight = new Vector2(touchDamageCheck.position.x + (width / 2), touchDamageCheck.position.y - (height / 2));
            Vector2 topRight = new Vector2(touchDamageCheck.position.x + (width / 2), touchDamageCheck.position.y + (height / 2));
            Vector2 topLeft = new Vector2(touchDamageCheck.position.x - (width / 2), touchDamageCheck.position.y + (height / 2));

            Gizmos.color = Color.red;
            Gizmos.DrawLine(botLeft, botRight);
            Gizmos.DrawLine(botRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, botLeft);
        }

        // optionally draw ground ray
        if (groundCheck != null)
        {
            Gizmos.color = groundDetected ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
        }
    }

    private void SetAnimatorBool(string param, bool value)
    {
        if (animator == null) return;
        if (!animator.HasParameter(param))
        {
            animator.SetBool(param, value);
            return;
        }
        animator.SetBool(param, value);
    }
}