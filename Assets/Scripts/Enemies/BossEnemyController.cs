using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// BossEnemyController
// Boss moveset:
// 1) shoot 3 bullets per set, 3 sets
// 2) can chase the player when in range (optional, configurable)
// 3) auto-dash toward player with cooldown (new)
// Boss always visually faces the GameObject tagged "Player".
public class BossEnemyController : MonoBehaviour
{
    private enum State
    {
        Moving,
        Attacking,
        Dead
    }

    private State currentState;

    // Prevent enemies colliding with each other by disabling Enemy-Enemy collisions once.
    private static bool s_enemyCollisionsIgnored = false;

    // track Collider2D for pairwise IgnoreCollision (works with Box/Circle/Composite/Tilemap etc.)
    private static List<Collider2D> s_enemyColliders = new List<Collider2D>();
    private readonly List<Collider2D> _localColliders = new List<Collider2D>();

    [Header("Core")]
    [SerializeField] private float groundCheckDistance = 1f;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float movementSpeed = 2f;      // kept for tuning / future use (unused now)
    [SerializeField] private float maxHealth = 200f;
    [SerializeField] private float lastTouchDamageTime = 0f;
    [SerializeField] private float touchDamageCooldown = 1f;
    [SerializeField] private float touchDamage = 10f;
    [SerializeField] private float touchDamageWidth = 1f;
    [SerializeField] private float touchDamageHeight = 1f;

    [Header("Boss pattern (tune in inspector)")]
    [Tooltip("When true the boss performs its attack pattern")]
    public bool isBoss = true;
    // runDuration/runSpeed left in inspector for compatibility but not used

    [Header("Chase (optional)")]
    [Tooltip("When true the boss will chase the player when inside chaseRadius")]
    [SerializeField] private bool canChase = true;
    [SerializeField] private float chaseRadius = 6f;
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Dash (auto)")]
    [Tooltip("Enable auto dash when chasing the player")]
    [SerializeField] private bool autoDashEnabled = true;
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashTime = 0.25f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashTriggerDistance = 3f; // horizontal distance to trigger dash

    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform[] bulletSpawnPoints;    // assign one or more spawn points
    [SerializeField] private int bulletsPerSet = 3;
    [SerializeField] private int setsPerVolley = 3;
    [SerializeField] private float timeBetweenBullets = 0.15f;
    [SerializeField] private float timeBetweenSets = 0.6f;
    [SerializeField] private float timeBetweenVolleys = 1.0f; // pause after full volley

    [Header("Checks & FX")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform touchDamageCheck;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private GameObject hitParticle;
    [SerializeField] private GameObject deathChunkParticle;
    [SerializeField] private GameObject deathBloodParticle;

    private float currentHealth;

    private float[] attackDetails = new float[2];

    // movement direction previously used for running — kept but unused
    private int moveDirection = 1;

    private Vector2 movement;
    private Vector2 touchDamageBotLeft;
    private Vector2 touchDamageTopRight;

    private bool groundDetected;
    private bool wallDetected;

    private GameObject alive;
    private Rigidbody2D aliveRb;
    private Animator aliveAnim;

    // Player reference for visual facing
    private Transform playerTransform;

    // chase runtime target
    private Transform _chaseTarget;

    // dash runtime state
    private bool _isDashing = false;
    private float _dashTimeLeft = 0f;
    private float _lastDashTime = -999f;
    private int _dashDirection = 1;

    // debug/stuck-detection fields (add near other private fields)
    private Vector2 _prevRbPos;
    private int _stuckFrameCount = 0;
    private int _stuckFrameThreshold = 6;

    private void Awake()
    {
        // Run once: disable collisions between objects on the "Enemy" layer.
        if (!s_enemyCollisionsIgnored)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
                s_enemyCollisionsIgnored = true;
            }
            else
            {
                Debug.LogWarning("Layer 'Enemy' not found. Create layer 'Enemy' and assign your enemy prefabs to it to disable enemy-enemy collisions.");
            }
        }

        RegisterEnemyColliders();
    }

    private void OnDestroy()
    {
        UnregisterEnemyColliders();
    }

    private void RegisterEnemyColliders()
    {
        _localColliders.Clear();
        var cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols)
        {
            if (c == null) continue;
            _localColliders.Add(c);

            foreach (var other in s_enemyColliders)
            {
                if (other != null)
                    Physics2D.IgnoreCollision(c, other, true);
            }

            if (!s_enemyColliders.Contains(c))
                s_enemyColliders.Add(c);
        }
    }

    private void UnregisterEnemyColliders()
    {
        foreach (var c in _localColliders)
        {
            if (c != null)
                s_enemyColliders.Remove(c);
        }
        _localColliders.Clear();
    }

    private void Start()
    {
        alive = transform.Find("Alive")?.gameObject ?? gameObject;
        if (alive == null) Debug.LogWarning("BossEnemyController: 'Alive' child not found; using root.");

        aliveRb = alive.GetComponent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();
        if (aliveRb == null) Debug.LogWarning("BossEnemyController: Rigidbody2D missing on Alive or root. Physics movement not available.");

        aliveAnim = alive.GetComponent<Animator>();

        currentHealth = maxHealth;

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;
        else Debug.LogWarning("Player with tag 'Player' not found. Boss will not face player.");

        SwitchState(State.Moving);

        if (isBoss)
        {
            StartCoroutine(BossPatternLoop());
        }
    }

    private void Update()
    {
        FacePlayerVisual();

        switch (currentState)
        {
            case State.Moving:
                UpdateMovingState();
                break;
            case State.Attacking:
                UpdateAttackingState();
                break;
            case State.Dead:
                UpdateDeadState();
                break;
        }
    }

    // Replace FixedUpdate with this debug-friendly version
    private void FixedUpdate()
    {
        // If currently dashing, apply dash movement in FixedUpdate
        if (_isDashing)
        {
            ApplyDashMovement();
            return;
        }

        // Apply chase movement (physics) in FixedUpdate for consistent physics behavior
        if (currentState == State.Moving && canChase && _chaseTarget != null)
        {
            if (aliveRb != null)
            {
                Vector2 currentPos = aliveRb.position;
                Vector2 targetPos = new Vector2(_chaseTarget.position.x, currentPos.y);
                float step = chaseSpeed * Time.fixedDeltaTime;
                Vector2 newPos = Vector2.MoveTowards(currentPos, targetPos, step);

                aliveRb.MovePosition(newPos);

                // stuck detection: if MovePosition didn't change position for several frames, nudge using velocity
                float moved = Vector2.Distance(currentPos, newPos);
                if (moved < 0.001f)
                {
                    _stuckFrameCount++;
                    if (_stuckFrameCount >= _stuckFrameThreshold)
                    {
                        Debug.LogWarning($"Boss appears stuck. nudge velocity. rb.bodyType={aliveRb.bodyType} pos={currentPos} targetX={targetPos.x}");
                        // nudge toward target so physics can resolve overlaps
                        float dir = Mathf.Sign(targetPos.x - currentPos.x);
                        aliveRb.velocity = new Vector2(dir * chaseSpeed, aliveRb.velocity.y);
                        _stuckFrameCount = 0;
                    }
                }
                else
                {
                    _stuckFrameCount = 0;
                }

                _prevRbPos = newPos;
            }
            else
            {
                // Transform fallback: horizontal only
                Vector3 dir = (_chaseTarget.position.x >= transform.position.x) ? Vector3.right : Vector3.left;
                transform.Translate(dir * chaseSpeed * Time.fixedDeltaTime, Space.World);
            }
        }
    }

    //-- MOVING (idle / chase detection) ------------------------------------------

    private void EnterMovingState()
    {
        // Boss idle or ready to chase
    }

    private void UpdateMovingState()
    {
        // Keep touch-damage checks and optional idle animation hooks.
        CheckTouchDamage();

        // Chase detection (only when enabled)
        if (canChase && playerTransform != null)
        {
            // Use overlap circle to find player within radius
            Collider2D playerHit = Physics2D.OverlapCircle(alive.transform.position, chaseRadius, whatIsPlayer);
            if (playerHit != null)
            {
                // Set chase target (no stop distance - boss will keep pursuing)
                _chaseTarget = playerHit.transform;
                // consider dashing if within trigger distance
                float horizDist = Mathf.Abs(_chaseTarget.position.x - alive.transform.position.x);
                TryStartAutoDash(horizDist);
            }
            else
            {
                _chaseTarget = null;
            }
        }

        // Optionally play idle animation here:
        if (aliveAnim != null)
        {
            // aliveAnim.SetBool("Idle", true); // enable if you have an Idle param
        }
    }

    private void ExitMovingState() { }

    //-- ATTACKING ---------------------------------------------------------------

    private void EnterAttackingState()
    {
        // Stop any residual movement
        if (aliveRb != null)
            aliveRb.velocity = Vector2.zero;
        if (aliveAnim != null)
            aliveAnim.SetBool("Attacking", true);
    }

    private void UpdateAttackingState()
    {
        // Attacks are executed by the BossPatternLoop coroutine (DoShootVolley).
    }

    private void ExitAttackingState()
    {
        if (aliveAnim != null)
            aliveAnim.SetBool("Attacking", false);
    }

    //-- DEAD --------------------------------------------------------------------

    private void EnterDeadState()
    {
        if (deathChunkParticle) Instantiate(deathChunkParticle, alive.transform.position, deathChunkParticle.transform.rotation);
        if (deathBloodParticle) Instantiate(deathBloodParticle, alive.transform.position, deathBloodParticle.transform.rotation);
        Destroy(gameObject);
    }

    private void UpdateDeadState() { }

    private void ExitDeadState() { }

    //-- OTHER FUNCTIONS ---------------------------------------------------------

    private void Damage(float[] attackDetails)
    {
        currentHealth -= attackDetails[0];

        if (hitParticle) Instantiate(hitParticle, alive.transform.position, Quaternion.Euler(0.0f, 0.0f, Random.Range(0.0f, 360.0f)));

        if (currentHealth > 0.0f)
            SwitchState(State.Moving);
        else
            SwitchState(State.Dead);
    }

    private void CheckTouchDamage()
    {
        if (Time.time >= lastTouchDamageTime + touchDamageCooldown && touchDamageCheck != null)
        {
            touchDamageBotLeft.Set(touchDamageCheck.position.x - (touchDamageWidth / 2), touchDamageCheck.position.y - (touchDamageHeight / 2));
            touchDamageTopRight.Set(touchDamageCheck.position.x + (touchDamageWidth / 2), touchDamageCheck.position.y + (touchDamageHeight / 2));

            Collider2D hit = Physics2D.OverlapArea(touchDamageBotLeft, touchDamageTopRight, whatIsPlayer);

            if (hit != null)
            {
                lastTouchDamageTime = Time.time;
                attackDetails[0] = touchDamage;
                attackDetails[1] = alive.transform.position.x;
                hit.SendMessage("Damage", attackDetails);
            }
        }
    }

    // Visual facing toward player
    private void FacePlayerVisual()
    {
        if (playerTransform == null || alive == null) return;

        float desiredY = (playerTransform.position.x >= alive.transform.position.x) ? 0f : 180f;
        Vector3 e = alive.transform.eulerAngles;
        if (!Mathf.Approximately(e.y, desiredY))
            alive.transform.eulerAngles = new Vector3(e.x, desiredY, e.z);
    }

    // State transition helper
    private void SwitchState(State state)
    {
        switch (currentState)
        {
            case State.Moving: ExitMovingState(); break;
            case State.Attacking: ExitAttackingState(); break;
            case State.Dead: ExitDeadState(); break;
        }

        switch (state)
        {
            case State.Moving: EnterMovingState(); break;
            case State.Attacking: EnterAttackingState(); break;
            case State.Dead: EnterDeadState(); break;
        }

        currentState = state;
    }

    //-- BOSS PATTERN LOOP & ATTACKS --------------------------------------------

    private IEnumerator BossPatternLoop()
    {
        // Pattern: shoot volley -> pause -> repeat
        while (currentState != State.Dead)
        {
            SwitchState(State.Attacking);
            yield return StartCoroutine(DoShootVolley());
            SwitchState(State.Moving);
            yield return new WaitForSeconds(timeBetweenVolleys);
        }
    }

    private IEnumerator DoShootVolley()
    {
        if (aliveAnim) aliveAnim.SetTrigger("Telegraph");
        yield return new WaitForSeconds(0.25f);

        for (int s = 0; s < setsPerVolley; s++)
        {
            for (int b = 0; b < bulletsPerSet; b++)
            {
                if (bulletSpawnPoints != null && bulletPrefab != null)
                {
                    for (int i = 0; i < bulletSpawnPoints.Length; i++)
                    {
                        Transform sp = bulletSpawnPoints[i];
                        Instantiate(bulletPrefab, sp.position, sp.rotation);
                    }
                }
                if (aliveAnim) aliveAnim.SetTrigger("Shoot");
                yield return new WaitForSeconds(timeBetweenBullets);
            }
            yield return new WaitForSeconds(timeBetweenSets);
        }
    }

    private void OnDrawGizmos()
    {
        if (touchDamageCheck != null)
        {
            Vector2 botLeft = new Vector2(touchDamageCheck.position.x - (touchDamageWidth / 2), touchDamageCheck.position.y - (touchDamageHeight / 2));
            Vector2 botRight = new Vector2(touchDamageCheck.position.x + (touchDamageWidth / 2), touchDamageCheck.position.y - (touchDamageHeight / 2));
            Vector2 topRight = new Vector2(touchDamageCheck.position.x + (touchDamageWidth / 2), touchDamageCheck.position.y + (touchDamageHeight / 2));
            Vector2 topLeft = new Vector2(touchDamageCheck.position.x - (touchDamageWidth / 2), touchDamageCheck.position.y + (touchDamageHeight / 2));

            Gizmos.DrawLine(botLeft, botRight);
            Gizmos.DrawLine(botRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, botLeft);
        }
    }

    // --- DASH HELPERS ---------------------------------------------------------

    private void TryStartAutoDash(float horizontalDistance)
    {
        if (!autoDashEnabled || _isDashing) return;

        // only dash when within trigger distance and cooldown passed
        if (horizontalDistance <= dashTriggerDistance && Time.time >= _lastDashTime + dashCooldown)
        {
            StartDash();
        }
    }

    private void StartDash()
    {
        if (_chaseTarget == null) return;

        _isDashing = true;
        _dashTimeLeft = dashTime;
        _lastDashTime = Time.time;
        _dashDirection = (_chaseTarget.position.x >= alive.transform.position.x) ? 1 : -1;

        // optional: play dash VFX/animation
        if (aliveAnim != null)
            aliveAnim.SetTrigger("Dash");

        // if you want to zero vertical velocity during dash:
        if (aliveRb != null)
            aliveRb.velocity = new Vector2(dashSpeed * _dashDirection, 0f);
    }

    private void ApplyDashMovement()
    {
        if (!_isDashing) return;

        float dt = Time.fixedDeltaTime;
        _dashTimeLeft -= dt;

        if (aliveRb != null)
        {
            // move via MovePosition for consistent physics interactions
            Vector2 target = aliveRb.position + Vector2.right * (_dashDirection * dashSpeed * dt);
            aliveRb.MovePosition(target);
        }
        else
        {
            transform.Translate(Vector3.right * (_dashDirection * dashSpeed * dt), Space.World);
        }

        // end dash when time elapsed
        if (_dashTimeLeft <= 0f)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        _isDashing = false;
        _dashTimeLeft = 0f;

        // resume chase (do not clear _chaseTarget)
        if (aliveRb != null)
            aliveRb.velocity = Vector2.zero;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isDashing && collision != null)
        {
            // Only consider collisions against whatIsGround (respect layer mask)
            int otherLayer = collision.gameObject.layer;
            if ((whatIsGround.value & (1 << otherLayer)) == 0) return;

            // Inspect contact normals — stop dash if we hit roughly horizontally (a wall)
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (Mathf.Abs(contact.normal.x) > 0.5f)
                {
                    EndDash();
                    break;
                }
            }
        }
    }

    // Add this to log collisions that may block the boss
    private void OnCollisionStay2D(Collision2D collision)
    {
        // only log a few times to avoid spam
        if (collision == null) return;

        // check if collision blocks horizontally
        foreach (ContactPoint2D cp in collision.contacts)
        {
            if (Mathf.Abs(cp.normal.x) > 0.5f)
            {
                Debug.Log($"Boss collision with '{collision.gameObject.name}' layer={collision.gameObject.layer} normal={cp.normal} at {Time.time}");
                break;
            }
        }
    }
}