using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy that repeatedly:
/// 1) jumps from point A -> point B
/// 2) throws a parabolic bomb at the player
/// 3) jumps back B -> A
/// 4) throws again
/// Loops until destroyed. Compatible with existing "Damage(float[] attackDetails)" messaging used in this project.
/// Supports Animator booleans: IsIdle, IsThrowing, IsJumping
/// </summary>
public class JumpingBomberEnemyController : MonoBehaviour
{
    [Header("Path Points")]
    [Tooltip("Point A (start)")]
    [SerializeField] private Transform pointA;
    [Tooltip("Point B (target)")]
    [SerializeField] private Transform pointB;

    [Header("Jump")]
    [Tooltip("Duration (seconds) of each jump (A->B or B->A)")]
    [SerializeField] private float jumpDuration = 0.9f;
    [Tooltip("Maximum arc height relative to direct line (world units)")]
    [SerializeField] private float jumpArcHeight = 2.0f;
    [Tooltip("Pause after landing before next action")]
    [SerializeField] private float pauseAfterLanding = 5f;

    [Header("Bomb / Projectile")]
    [SerializeField] private GameObject bombPrefab;              // prefab should have Rigidbody2D
    [SerializeField] private Transform bombSpawnPoint;           // where bombs are spawned
    [Tooltip("Preferred launch angle in degrees for the bomb")]
    [SerializeField] private float bombLaunchAngleDeg = 50f;
    [Tooltip("Fallback speed when ballistic calc fails")]
    [SerializeField] private float bombFallbackSpeed = 8f;
    [Tooltip("How long to keep IsThrowing true (seconds)")]
    [SerializeField] private float throwAnimDuration = 0.35f;
    [SerializeField] private float bombSpeedMultiplier = 1f; // 1 = normal speed, >1 faster, <1 slower (clamped to >0)

    [Header("Behavior")]
    [Tooltip("Start the pattern automatically on Start")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("Optional time to wait before starting first loop")]
    [SerializeField] private float initialDelay = 0.25f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private GameObject deathParticle;
    [SerializeField] private string sceneToLoadOnDefeat = "";
    [SerializeField] private float sceneLoadDelay = 1f;

    // Optional BossStats so this enemy can register with the same BossHealthBar used by BossEnemyController.
    [Header("Optional: Boss-style health UI")]
    [Tooltip("Assign a BossStats component to enable BossHealthBar registration. If left empty a simple local health is used.")]
    [SerializeField] private BossStats bossStats;

    // Optional Animator - will be auto-found on an 'Alive' child or any Animator in children if left null.
    [Header("Optional: Animator")]
    [Tooltip("Animator should expose boolean parameters: IsIdle, IsThrowing, IsJumping")]
    [SerializeField] private Animator animator;

    [Header("Touch Damage")]
    [Tooltip("Transform used to check touch-damage area (center). If empty will use the enemy root.")]
    [SerializeField] private Transform touchDamageCheck;
    [Tooltip("Layer(s) considered player for touch damage")]
    [SerializeField] private LayerMask whatIsPlayer;
    // fallback touch damage values used when bossStats == null
    [SerializeField] private float touchDamage = 10f;
    [SerializeField] private float touchDamageWidth = 1f;
    [SerializeField] private float touchDamageHeight = 1f;
    [SerializeField] private float touchDamageCooldown = 3f;

    private GameObject alive;
    private float _currentHealth;
    private Transform _player;
    private bool _isRunning = false;
    private bool _isDead = false;

    // touch-damage runtime
    private float lastTouchDamageTime = 0f;
    private Vector2 touchDamageBotLeft;
    private Vector2 touchDamageTopRight;
    private float[] attackDetails = new float[2];

    private void Awake()
    {
    }

    private void OnDestroy()
    {
        // Unregister health UI if it was registered
        BossHealthBar.Instance?.UnregisterBoss();
    }

    private void Start()
    {
        // try to find an 'Alive' child (consistent with BossEnemyController) and animator
        alive = transform.Find("Alive")?.gameObject ?? gameObject;
        if (animator == null && alive != null) animator = alive.GetComponentInChildren<Animator>();

        // Ensure touchDamageCheck fallback
        if (touchDamageCheck == null)
            touchDamageCheck = alive != null ? alive.transform : transform;

        // local fallback health (used when bossStats not provided)
        _currentHealth = maxHealth;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;

        // If a BossStats component was assigned (or present on the same GameObject) use it and register with BossHealthBar.
        if (bossStats == null)
        {
            bossStats = GetComponent<BossStats>();
            if (bossStats == null)
            {
                // do not auto-add by default; leave optional
            }
        }

        if (bossStats != null)
        {
            bossStats.Initialize();
            // Register with the BossHealthBar so it shows a health bar like other bosses.
            BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);
        }

        // sanity checks
        if (pointA == null || pointB == null)
            Debug.LogWarning($"{nameof(JumpingBomberEnemyController)}: pointA/pointB not both assigned.");

        if (bombSpawnPoint == null)
            bombSpawnPoint = transform; // fallback

        if (startOnAwake)
            StartPatternWithDelay(initialDelay);
    }

    private void Update()
    {
        // Keep touch-damage checks running each frame
        CheckTouchDamage();
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
            // Jump A -> B (animator flag handled in PerformJump)
            yield return StartCoroutine(PerformJump(pointA != null ? pointA.position : transform.position,
                                                    pointB != null ? pointB.position : transform.position));
            // Throw at player (also plays IsThrowing)
            ThrowBombAtPlayer();
            // Show idle during pause after landing
            SetAnimatorBool("IsIdle", true);
            yield return new WaitForSeconds(pauseAfterLanding);
            SetAnimatorBool("IsIdle", false);

            // Jump B -> A
            yield return StartCoroutine(PerformJump(pointB != null ? pointB.position : transform.position,
                                                    pointA != null ? pointA.position : transform.position));
            // Throw at player
            ThrowBombAtPlayer();
            // Idle during pause
            SetAnimatorBool("IsIdle", true);
            yield return new WaitForSeconds(pauseAfterLanding);
            SetAnimatorBool("IsIdle", false);
        }
    }

    // Moves the GameObject along a smooth parabolic arc between two positions over jumpDuration.
    // Flips sprite (alive) and bomb spawn point when reaching midpoint of the jump.
    private IEnumerator PerformJump(Vector3 startPos, Vector3 endPos)
    {
        float t = 0f;
        // position snapping at start to avoid visual pop
        transform.position = startPos;

        // set jumping animation
        SetAnimatorBool("IsJumping", true);
        SetAnimatorBool("IsIdle", false);
        SetAnimatorBool("IsThrowing", false);

        // prepare midpoint flip state
        bool midpointFlipped = false;
        bool spawnIsChild = bombSpawnPoint != null && (alive != null ? bombSpawnPoint.IsChildOf(alive.transform) : bombSpawnPoint.IsChildOf(transform));
        bool bombIsSelf = bombSpawnPoint == transform || bombSpawnPoint == alive?.transform;

        while (t < jumpDuration)
        {
            t += Time.deltaTime ;
            float normalized = Mathf.Clamp01(t / jumpDuration);

            // horizontal / linear interpolation
            Vector3 basePos = Vector3.Lerp(startPos, endPos, normalized);

            // vertical arc: simple parabola peak at normalized == 0.5
            // arc = 4 * h * s * (1 - s)
            float arc = 4f * jumpArcHeight * normalized * (1f - normalized);

            transform.position = new Vector3(basePos.x, basePos.y + arc, basePos.z);

            // flip at midpoint once
            if (!midpointFlipped && normalized >= 0.5f)
            {
                midpointFlipped = true;

                // Flip sprite by inverting localScale.x on the 'alive' root (if present), otherwise on this transform.
                Transform flipTarget = alive != null ? alive.transform : transform;
                Vector3 s = flipTarget.localScale;
                s.x *= -1f;
                flipTarget.localScale = s;

                // Flip bomb spawn point horizontally only when it's NOT a child.
                // If it's a child, the parent's scale flip already mirrors the child's world position,
                // so we must NOT invert the child's localPosition (that caused the double-invert bug).
                if (bombSpawnPoint != null && !bombIsSelf)
                {
                    if (!spawnIsChild)
                    {
                        Vector3 offset = bombSpawnPoint.position - transform.position;
                        offset.x *= -1f;
                        bombSpawnPoint.position = transform.position + offset;
                    }
                }
            }

            yield return null;
        }

        // ensure exact final pos
        transform.position = endPos;

        // clear jumping flag (landing)
        SetAnimatorBool("IsJumping", false);
    }

    // Replace the existing ThrowBombAtPlayer method with this version
    private void ThrowBombAtPlayer()
    {
        if (bombPrefab == null || _player == null) return;

        // animator handling
        if (animator != null)
        {
            SetAnimatorBool("IsIdle", false);
            SetAnimatorBool("IsJumping", false);
            SetAnimatorBool("IsThrowing", true);
            StartCoroutine(ClearThrowingAfter(throwAnimDuration));
        }

        Vector2 spawnPos = bombSpawnPoint != null ? (Vector2)bombSpawnPoint.position : (Vector2)transform.position;
        Vector2 targetPos = (Vector2)_player.position;

        GameObject go = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        if (go == null) return;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning("Bomb prefab has no Rigidbody2D — cannot apply ballistic velocity.");
            return;
        }

        // Ensure projectile will be affected by gravity (common reason it looks like a straight line)
        if (Mathf.Approximately(rb.gravityScale, 0f))
        {
            Debug.LogWarning($"Bomb Rigidbody2D.gravityScale was 0 — forcing to 1.0. Set correct gravityScale on prefab instead.");
            rb.gravityScale = 1f;
        }

        Vector2 gravityVec = Physics2D.gravity * rb.gravityScale; // world gravity vector used by Rigidbody2D

        // Try analytic solution first (angle-based)
        Vector2 initialVelocity;
        bool ok = CalculateLaunchVelocity(spawnPos, targetPos, bombLaunchAngleDeg, Mathf.Abs(gravityVec.y), out initialVelocity);

        if (!ok)
        {
            // fallback: time-based solver (chooses travel time from horizontal distance)
            float dx = targetPos.x - spawnPos.x;
            float dy = targetPos.y - spawnPos.y;
            float absDx = Mathf.Max(0.001f, Mathf.Abs(dx));

            // choose time: further targets take longer; clamp to avoid huge values
            float time = Mathf.Clamp(absDx / Mathf.Max(1f, bombFallbackSpeed), 0.35f, 2.0f);

            float vx = dx / time;
            // vy needed to reach dy in time 'time' under gravity: dy = vy * t + 0.5 * gravity.y * t^2
            float vy = (dy - 0.5f * gravityVec.y * time * time) / time;

            initialVelocity = new Vector2(vx, vy);
        }

        // after computing initialVelocity (both analytic and fallback branches), apply multiplier:
        initialVelocity *= Mathf.Max(0.0001f, bombSpeedMultiplier);

        rb.velocity = initialVelocity;

        // Debug: log and draw predicted trajectory so you can verify behavior in-game
        Debug.Log($"Bomb launch: spawn={spawnPos} target={targetPos} vel={initialVelocity} gravityVec={gravityVec}");
        DrawDebugTrajectory(spawnPos, initialVelocity, gravityVec, 1.8f, 24);
    }

    // Draw a debug trajectory using Debug.DrawLine for quick visual verification
    private void DrawDebugTrajectory(Vector2 start, Vector2 velocity, Vector2 gravityVec, float duration, int steps)
    {
        Vector2 prev = start;
        for (int i = 1; i <= steps; i++)
        {
            float t = (i / (float)steps) * duration;
            Vector2 pos = start + velocity * t + 0.5f * gravityVec * t * t;
            Debug.DrawLine(prev, pos, Color.green, 1.0f);
            prev = pos;
        }
    }

    /// <summary>
    /// Calculates the initial velocity vector required to launch from start to target
    /// using a specified launch angle (degrees). Returns false when no valid solution exists.
    /// This version handles negative dx and uses absolute horizontal distance when solving.
    /// </summary>
    private bool CalculateLaunchVelocity(Vector2 start, Vector2 target, float launchAngleDeg, float gravity, out Vector2 velocity)
    {
        velocity = Vector2.zero;

        Vector2 d = target - start;
        float dx = d.x;
        float dy = d.y;

        // avoid degenerate case
        if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
            return false;

        float theta = launchAngleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(theta);
        float sin = Mathf.Sin(theta);

        // use absolute horizontal distance for solving v^2
        float dxAbs = Mathf.Abs(dx);

        // denom = dx * tan(theta) - dy  (using dxAbs)
        float denom = (dxAbs * Mathf.Tan(theta) - dy);

        // if denom is non-positive the chosen angle cannot reach the target
        if (denom <= 1e-5f)
            return false;

        float v2 = gravity * dxAbs * dxAbs / (2f * cos * cos * denom);
        if (v2 <= 0f || float.IsNaN(v2))
            return false;

        float v = Mathf.Sqrt(v2);

        // preserve horizontal direction sign
        float vx = v * cos * Mathf.Sign(dx);
        float vy = v * sin;

        velocity = new Vector2(vx, vy);
        return true;
    }

    // Touch damage check adapted from BossEnemyController
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

    // When other systems send a Damage message (project uses float[] attackDetails), support it.
    // Example: hit.SendMessage("Damage", attackDetails);
    public void Damage(float[] attackDetails)
    {
        if (_isDead) return;
        if (attackDetails == null || attackDetails.Length == 0) return;

        float dmg = attackDetails[0];
        bool died = false;

        if (bossStats != null)
        {
            // Use BossStats so UI and other systems remain consistent with BossEnemyController.
            died = bossStats.ApplyDamage(dmg);
            // Note: BossHealthBar behavior depends on your implementation. If the bar doesn't update,
            // ensure BossHealthBar listens to BossStats or add an explicit update method there.
        }
        else
        {
            // fallback local health
            _currentHealth -= dmg;
            if (_currentHealth <= 0f) died = true;
        }

        if (died)
        {
            Die();
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Unregister UI (if registered)
        BossHealthBar.Instance?.UnregisterBoss();

        // drop items if present
        GetComponent<EnemyWeaponDrop>()?.DropNow();

        if (deathParticle) Instantiate(deathParticle, transform.position, Quaternion.identity);

        // optional scene load or destroy
        if (!string.IsNullOrEmpty(sceneToLoadOnDefeat))
        {
            StartCoroutine(LoadSceneAfterDelay(sceneToLoadOnDefeat, sceneLoadDelay));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void OnDrawGizmos()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(pointA.position, 0.12f);
            Gizmos.DrawSphere(pointB.position, 0.12f);
            Gizmos.DrawLine(pointA.position, pointB.position);

            // draw a simple arc preview
            Vector3 a = pointA.position;
            Vector3 b = pointB.position;
            int steps = 12;
            Vector3 prev = a;
            for (int i = 1; i <= steps; i++)
            {
                float s = i / (float)steps;
                Vector3 basePos = Vector3.Lerp(a, b, s);
                float arc = 4f * jumpArcHeight * s * (1f - s);
                Vector3 pos = new Vector3(basePos.x, basePos.y + arc, basePos.z);
                Gizmos.DrawLine(prev, pos);
                prev = pos;
            }
        }

        if (bombSpawnPoint != null && _player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(bombSpawnPoint.position, _player.position);
        }

        // draw touch damage box
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
    }

    private IEnumerator ClearThrowingAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetAnimatorBool("IsThrowing", false);
    }

    private void SetAnimatorBool(string param, bool value)
    {
        if (animator == null) return;
        // Use the AnimatorExtensions helper to check for the parameter safely
        if (!animator.HasParameter(param))
        {
            // If parameter missing, setting still safe but avoid errors in some animator setups
            animator.SetBool(param, value);
            return;
        }
        animator.SetBool(param, value);
    }
}

/// <summary>
/// Extension helper for Animator to check parameter existence (non-reflection fallback).
/// </summary>
public static class AnimatorExtensions
{
    public static bool HasParameter(this Animator animator, string paramName)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == paramName) return true;
        return false;
    }
}