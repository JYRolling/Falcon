using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossEnemyController : MonoBehaviour
{
    private enum State
    {
        Moving,
        Attacking,
        Dead
    }

    private State currentState;

    // track Collider2D for pairwise IgnoreCollision (works with Box/Circle/Composite/Tilemap etc.)
    private static List<Collider2D> s_enemyColliders = new List<Collider2D>();
    private readonly List<Collider2D> _localColliders = new List<Collider2D>();

    [Header("Core")]
    [SerializeField] private float groundCheckDistance = 1f;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float movementSpeed = 2f;

    // Now reference a BossStats component (attachable in inspector)
    [SerializeField] private BossStats bossStats;

    [SerializeField] private float lastTouchDamageTime = 0f;

    [Header("Boss pattern (tune in inspector)")]
    [Tooltip("When true the boss performs its attack pattern")]
    public bool isBoss = true;

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

    // --- continuous shooting (EnemyShooting-like) ---
    [Tooltip("When true the boss will auto-shoot while the player is within triggerRange")]
    [SerializeField] private bool continuousShooting = false;
    [SerializeField] private float triggerRange = 4f;
    [SerializeField] private float fireInterval = 2f;    // seconds between shots
    [SerializeField] private float bulletSpeed = 10f;    // used if bullet prefab uses Rigidbody2D velocity
    private float _shootTimer = 0f;

    [Header("Checks & FX")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform touchDamageCheck;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private GameObject hitParticle;

    [Header("Optional: scene transition on boss defeat")]
    [Tooltip("Name of scene to load when this boss is defeated. Leave empty to disable.")]
    [SerializeField] private string sceneToLoadOnDefeat = "";
    [Tooltip("Delay (seconds) before loading the scene after boss death.")]
    [SerializeField] private float sceneLoadDelay = 1.0f;

    private float currentHealth;

    private float[] attackDetails = new float[2];

    // movement direction — used for patrol and chasing logic
    private int facingDirection = 1;

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

    // debug/stuck-detection fields (kept but not used in this movement style)
    private Vector2 _prevRbPos;

    private void Awake()
    {
    }

    private void OnDestroy()
    {
        BossHealthBar.Instance?.UnregisterBoss();
    }

    private void Start()
    {
        alive = transform.Find("Alive")?.gameObject ?? gameObject;
        if (alive == null) Debug.LogWarning("BossEnemyController: 'Alive' child not found; using root.");

        aliveRb = alive.GetComponent<Rigidbody2D>() ?? GetComponent<Rigidbody2D>();
        if (aliveRb == null) Debug.LogWarning("BossEnemyController: Rigidbody2D missing on Alive or root. Physics movement not available.");

        aliveAnim = alive.GetComponent<Animator>();

        // ensure bossStats is a component attached to the same GameObject (attach in inspector)
        if (bossStats == null)
        {
            bossStats = GetComponent<BossStats>();
            if (bossStats == null)
            {
                // auto-add so code can run, but prefer inspector assignment
                bossStats = gameObject.AddComponent<BossStats>();
                Debug.LogWarning("BossEnemyController: BossStats was not assigned. A BossStats component was auto-added — consider configuring it in the inspector.");
            }
        }

        // initialize health via BossStats
        bossStats.Initialize();

        // register with BossHealthBar (UI will become visible)
        BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;
        else Debug.LogWarning("Player with tag 'Player' not found. Boss will not face player.");

        // ensure facingDirection default
        facingDirection = 1;

        SwitchState(State.Moving);

        if (isBoss)
        {
            StartCoroutine(BossPatternLoop());
        }
    }

    private void Update()
    {
        FacePlayerVisual();

        // continuous shooting behavior:
        // run only when enabled and boss is not currently performing volley attacks (attacking state)
        if (continuousShooting && playerTransform != null && currentState == State.Moving)
            HandleContinuousShooting();

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

    // FixedUpdate handles dash physics separately; chasing & patrol movement use Rigidbody2D.velocity like ChasingEnemyController.
    private void FixedUpdate()
    {
        if (_isDashing)
        {
            ApplyDashMovement();
        }
    }

    //-- MOVING (idle / chase detection) ------------------------------------------

    private void EnterMovingState()
    {
        // start ready to chase/patrol
        _chaseTarget = null;
        facingDirection = 1;
    }

    private void UpdateMovingState()
    {
        // ground/wall checks (used for simple patrol when not chasing)
        if (groundCheck != null)
            groundDetected = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, whatIsGround);
        else
            groundDetected = true;

        if (wallCheck != null)
            wallDetected = Physics2D.Raycast(wallCheck.position, transform.right, wallCheckDistance, whatIsGround);
        else
            wallDetected = false;

        // Keep touch-damage checks
        CheckTouchDamage();

        // Chase detection (only when enabled)
        if (canChase && playerTransform != null)
        {
            Collider2D playerHit = Physics2D.OverlapCircle(alive.transform.position, chaseRadius, whatIsPlayer);
            if (playerHit != null)
            {
                // Set chase target
                _chaseTarget = playerHit.transform;
            }
            else
            {
                _chaseTarget = null;
            }
        }
        else
        {
            _chaseTarget = null;
        }

        // If we have a chase target, behave like ChasingEnemyController: move toward player using velocity.
        if (_chaseTarget != null)
        {
            // If target moved out of radius stop chasing
            float dist = Vector2.Distance(alive.transform.position, _chaseTarget.position);
            if (dist > chaseRadius)
            {
                _chaseTarget = null;
                return;
            }

            float dir = Mathf.Sign(_chaseTarget.position.x - alive.transform.position.x);
            facingDirection = dir >= 0 ? 1 : -1;

            // Move horizontally toward player
            if (aliveRb != null)
            {
                movement.Set(chaseSpeed * facingDirection, aliveRb.velocity.y);
                aliveRb.velocity = movement;
            }
            else
            {
                // Transform fallback: horizontal only
                Vector3 moveDir = (facingDirection == 1) ? Vector3.right : Vector3.left;
                transform.Translate(moveDir * chaseSpeed * Time.deltaTime, Space.World);
            }

            // consider dashing if within trigger distance
            float horizDist = Mathf.Abs(_chaseTarget.position.x - alive.transform.position.x);
            TryStartAutoDash(horizDist);

            // still allow touch damage while chasing (already called above)
            return;
        }

        // Not chasing: simple patrol behaviour (if desired)
        if (!groundDetected || wallDetected)
        {
            FlipForMovement();
        }
        else
        {
            if (aliveRb != null)
            {
                movement.Set(movementSpeed * facingDirection, aliveRb.velocity.y);
                aliveRb.velocity = movement;
            }
            else
            {
                Vector3 dir = (facingDirection == 1) ? Vector3.right : Vector3.left;
                transform.Translate(dir * movementSpeed * Time.deltaTime, Space.World);
            }
        }
    }

    private void ExitMovingState() { }

    //-- ATTACKING ---------------------------------------------------------------

    private void EnterAttackingState()
    {
    }

    private void UpdateAttackingState()
    {
    }

    private void ExitAttackingState() { }

    //-- DEAD --------------------------------------------------------------------

    private void EnterDeadState()
    {
        if (bossStats.deathChunkParticle) Instantiate(bossStats.deathChunkParticle, alive.transform.position, bossStats.deathChunkParticle.transform.rotation);
        if (bossStats.deathBloodParticle) Instantiate(bossStats.deathBloodParticle, alive.transform.position, bossStats.deathBloodParticle.transform.rotation);

        // unregister UI (may hide if no other bosses registered)
        BossHealthBar.Instance?.UnregisterBoss();

        // optional scene load
        if (!string.IsNullOrEmpty(sceneToLoadOnDefeat))
        {
            StartCoroutine(LoadSceneAfterDelay(sceneToLoadOnDefeat, sceneLoadDelay));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdateDeadState() { }

    private void ExitDeadState() { }

    //-- OTHER FUNCTIONS ---------------------------------------------------------

    private void Damage(float[] attackDetails)
    {
        bool died = bossStats.ApplyDamage(attackDetails[0]);

        if (hitParticle) Instantiate(hitParticle, alive.transform.position, Quaternion.Euler(0.0f, 0.0f, Random.Range(0.0f, 360.0f)));

        if (!died)
            SwitchState(State.Moving);
        else
            SwitchState(State.Dead);
    }

    private void CheckTouchDamage()
    {
        if (Time.time >= lastTouchDamageTime + bossStats.touchDamageCooldown && touchDamageCheck != null)
        {
            touchDamageBotLeft.Set(touchDamageCheck.position.x - (bossStats.touchDamageWidth / 2), touchDamageCheck.position.y - (bossStats.touchDamageHeight / 2));
            touchDamageTopRight.Set(touchDamageCheck.position.x + (bossStats.touchDamageWidth / 2), touchDamageCheck.position.y + (bossStats.touchDamageHeight / 2));

            Collider2D hit = Physics2D.OverlapArea(touchDamageBotLeft, touchDamageTopRight, whatIsPlayer);

            if (hit != null)
            {
                lastTouchDamageTime = Time.time;
                attackDetails[0] = bossStats.touchDamage;
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
                yield return new WaitForSeconds(timeBetweenBullets);
            }
            yield return new WaitForSeconds(timeBetweenSets);
        }
    }

    // Continuous shooting helpers
    private void HandleContinuousShooting()
    {
        if (playerTransform == null) return;

        float distance = Vector2.Distance(alive.transform.position, playerTransform.position);
        if (distance <= triggerRange)
        {
            _shootTimer += Time.deltaTime;
            if (_shootTimer >= fireInterval)
            {
                _shootTimer = 0f;
                ShootAtPlayer();
            }
        }
        else
        {
            // keep timer capped so when player re-enters it's predictable
            _shootTimer = Mathf.Min(_shootTimer, fireInterval);
        }
    }

    private void ShootAtPlayer()
    {
        if (bulletPrefab == null || bulletSpawnPoints == null || bulletSpawnPoints.Length == 0 || playerTransform == null)
            return;

        Vector2 targetPos = playerTransform.position;

        for (int i = 0; i < bulletSpawnPoints.Length; i++)
        {
            Transform sp = bulletSpawnPoints[i];
            if (sp == null) continue;

            Vector2 spawnPos = sp.position;
            Vector2 dir = (targetPos - spawnPos);
            if (dir.sqrMagnitude < 0.0001f) dir = (facingDirection == 1) ? Vector2.right : Vector2.left;
            dir.Normalize();

            // rotation similar to your bullets
            float rot = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg;
            Quaternion rotQuat = Quaternion.Euler(0f, 0f, rot + 90f);

            GameObject go = Instantiate(bulletPrefab, spawnPos, rotQuat);
            if (go == null) continue;

            // If the prefab uses BossBullet (preferred) pass the explicit target
            var bossBullet = go.GetComponent<BossBullet>();
            if (bossBullet != null)
            {
                bossBullet.SetTargetPosition(targetPos);
                // optionally override speed if you want
                bossBullet.speed = bulletSpeed;
                continue;
            }

            // Otherwise try to set Rigidbody2D velocity
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = dir * bulletSpeed;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (touchDamageCheck != null)
        {
            // use bossStats touch damage size values (was previously using undefined local names)
            float width = 1f;
            float height = 1f;
            if (bossStats != null)
            {
                width = bossStats.touchDamageWidth;
                height = bossStats.touchDamageHeight;
            }

            Vector2 botLeft = new Vector2(touchDamageCheck.position.x - (width / 2), touchDamageCheck.position.y - (height / 2));
            Vector2 botRight = new Vector2(touchDamageCheck.position.x + (width / 2), touchDamageCheck.position.y - (height / 2));
            Vector2 topRight = new Vector2(touchDamageCheck.position.x + (width / 2), touchDamageCheck.position.y + (height / 2));
            Vector2 topLeft = new Vector2(touchDamageCheck.position.x - (width / 2), touchDamageCheck.position.y + (height / 2));

            Gizmos.DrawLine(botLeft, botRight);
            Gizmos.DrawLine(botRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, botLeft);
        }

        // draw chase radius when chasing enabled
        if (canChase)
        {
            if (alive != null)
                Gizmos.DrawWireSphere(alive.transform.position, chaseRadius);
            else
                Gizmos.DrawWireSphere(transform.position, chaseRadius);
        }

        // draw ground/wall checks
        if (groundCheck != null)
            Gizmos.DrawLine(groundCheck.position, new Vector2(groundCheck.position.x, groundCheck.position.y - groundCheckDistance));
        if (wallCheck != null)
            Gizmos.DrawLine(wallCheck.position, new Vector2(wallCheck.position.x + wallCheckDistance, wallCheck.position.y));
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

    // Flip for movement only (visual facing is controlled separately by FacePlayerVisual)
    private void FlipForMovement()
    {
        facingDirection *= -1;
    }

    // coroutine to load a scene by name after a delay
    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}