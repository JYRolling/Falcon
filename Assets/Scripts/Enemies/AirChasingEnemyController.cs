using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Floating enemy that patrols between two points (Point A / Point B).
// When the player enters chaseRadius the enemy chases the player in both X and Y (if enabled).
// When player leaves the radius the enemy returns to patrolling points.
public class AirChasingEnemyController : MonoBehaviour
{
    private enum State
    {
        Patrolling,
        Chasing,
        Knockback,
        Dead
    }

    private State currentState;

    // Prevent enemies colliding with each other by disabling Enemy-Enemy collisions once.
    private static bool s_enemyCollisionsIgnored = false;

    // New: keep track of BoxCollider2D from all enemies so we can ignore collisions between them
    private static List<BoxCollider2D> s_enemyBoxColliders = new List<BoxCollider2D>();
    private readonly List<BoxCollider2D> _localBoxColliders = new List<BoxCollider2D>();

    [Header("Patrol")]
    [SerializeField] private Transform patrolPointA;
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolReachThreshold = 0.1f;

    [Header("Behavior")]
    [Tooltip("When true the enemy will chase the player within chaseRadius. When false it will only patrol between A and B.")]
    public bool isChasingPlayer = true;

    [Header("Chase")]
    [SerializeField] private float chaseRadius = 4f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private GameObject chaseRadiusObject; // optional visual

    [Header("Combat / Common")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private Vector2 knockbackSpeed;
    [SerializeField] private Transform touchDamageCheck;
    [SerializeField] private float touchDamageCooldown = 1f;
    [SerializeField] private float touchDamage = 1f;
    [SerializeField] private float touchDamageWidth = 0.5f;
    [SerializeField] private float touchDamageHeight = 0.5f;
    [SerializeField] private GameObject hitParticle;
    [SerializeField] private GameObject deathChunkParticle;
    [SerializeField] private GameObject deathBloodParticle;

    // runtime
    private GameObject alive;
    private Rigidbody2D aliveRb;
    private Animator aliveAnim;

    private float currentHealth;
    private float knockbackStartTime;
    private float lastTouchDamageTime;
    private float[] attackDetails = new float[2];

    private Transform currentPatrolTarget;
    private Transform chaseTarget;
    private int facingDirection = 1;
    private int damageDirection = 1;
    private Vector2 movement;
    private Vector2 touchDamageBotLeft;
    private Vector2 touchDamageTopRight;

    private void Awake()
    {
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
                Debug.LogWarning("Layer 'Enemy' not found. Create and assign enemies to that layer to disable enemy-enemy collisions.");
            }
        }

        // Register this enemy's BoxCollider2D components and ignore collisions with existing enemy colliders.
        RegisterEnemyBoxColliders();
    }

    private void OnDestroy()
    {
        UnregisterEnemyBoxColliders();
    }

    private void RegisterEnemyBoxColliders()
    {
        _localBoxColliders.Clear();
        var boxes = GetComponentsInChildren<BoxCollider2D>();
        foreach (var b in boxes)
        {
            if (b == null) continue;
            _localBoxColliders.Add(b);

            // ignore collision with already-registered enemy boxes
            foreach (var other in s_enemyBoxColliders)
            {
                if (other != null)
                    Physics2D.IgnoreCollision(b, other, true);
            }

            // add to global list if not already present
            if (!s_enemyBoxColliders.Contains(b))
                s_enemyBoxColliders.Add(b);
        }
    }

    private void UnregisterEnemyBoxColliders()
    {
        foreach (var b in _localBoxColliders)
        {
            if (b != null)
                s_enemyBoxColliders.Remove(b);
        }
        _localBoxColliders.Clear();
    }

    private void Start()
    {
        alive = transform.Find("Alive")?.gameObject ?? gameObject;
        aliveRb = alive.GetComponent<Rigidbody2D>();
        aliveAnim = alive.GetComponent<Animator>();

        currentHealth = maxHealth;

        // For floating behaviour we want no gravity so movement in Y is predictable.
        if (aliveRb != null)
            aliveRb.gravityScale = 0f;

        // Ensure patrol points exist. If not provided create ephemeral points at offsets.
        if (patrolPointA == null)
        {
            var go = new GameObject(name + "_PatrolA");
            go.transform.position = transform.position + Vector3.left * 2f;
            patrolPointA = go.transform;
            go.hideFlags = HideFlags.DontSave;
        }
        if (patrolPointB == null)
        {
            var go = new GameObject(name + "_PatrolB");
            go.transform.position = transform.position + Vector3.right * 2f;
            patrolPointB = go.transform;
            go.hideFlags = HideFlags.DontSave;
        }

        currentPatrolTarget = patrolPointB;

        // visual for chase radius (only if chasing behaviour enabled)
        if (chaseRadiusObject != null)
        {
            if (isChasingPlayer)
                chaseRadiusObject.transform.localScale = Vector3.one * chaseRadius * 2f;
            else
                chaseRadiusObject.SetActive(false);
        }

        SwitchState(State.Patrolling);
    }

    private void Update()
    {
        switch (currentState)
        {
            case State.Patrolling:
                UpdatePatrollingState();
                break;
            case State.Chasing:
                UpdateChasingState();
                break;
            case State.Knockback:
                UpdateKnockbackState();
                break;
            case State.Dead:
                UpdateDeadState();
                break;
        }
    }

    // --- PATROL -----------------------------------------------------------------------------------
    private void EnterPatrollingState()
    {
        chaseTarget = null;
    }

    private void UpdatePatrollingState()
    {
        // check for player to start chase only if chasing behaviour is enabled
        if (isChasingPlayer)
        {
            Collider2D playerHit = Physics2D.OverlapCircle(alive.transform.position, chaseRadius, whatIsPlayer);
            if (playerHit != null)
            {
                chaseTarget = playerHit.transform;
                SwitchState(State.Chasing);
                return;
            }
        }

        // move toward current patrol target in both X and Y
        Vector2 toTarget = (Vector2)currentPatrolTarget.position - (Vector2)alive.transform.position;
        float distance = toTarget.magnitude;
        Vector2 dir = distance > 0.0001f ? toTarget.normalized : Vector2.zero;

        movement = dir * patrolSpeed;
        if (aliveRb != null) aliveRb.velocity = movement;

        // adjust facing based on horizontal component
        if (Mathf.Abs(toTarget.x) > 0.01f)
            facingDirection = toTarget.x >= 0f ? 1 : -1;
        ApplyFacing();

        // reached target?
        if (distance <= patrolReachThreshold)
            SwapPatrolTarget();
    }

    private void ExitPatrollingState() { }

    private void SwapPatrolTarget()
    {
        currentPatrolTarget = currentPatrolTarget == patrolPointA ? patrolPointB : patrolPointA;
    }

    // --- CHASE ------------------------------------------------------------------------------------
    private void EnterChasingState()
    {
        // optionally set chase animation
    }

    private void UpdateChasingState()
    {
        // if chasing was disabled mid-play, return to patrolling
        if (!isChasingPlayer)
        {
            SwitchState(State.Patrolling);
            return;
        }

        if (chaseTarget == null)
        {
            // try reacquire
            Collider2D p = Physics2D.OverlapCircle(alive.transform.position, chaseRadius, whatIsPlayer);
            if (p != null) chaseTarget = p.transform;
            else { SwitchState(State.Patrolling); return; }
        }

        float dist = Vector2.Distance(alive.transform.position, chaseTarget.position);
        if (dist > chaseRadius)
        {
            // out of radius -> return to patrol
            chaseTarget = null;
            SwitchState(State.Patrolling);
            return;
        }

        // move toward the player in both X and Y
        Vector2 toPlayer = (Vector2)chaseTarget.position - (Vector2)alive.transform.position;
        Vector2 dirToPlayer = toPlayer.magnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;

        movement = dirToPlayer * chaseSpeed;
        if (aliveRb != null) aliveRb.velocity = movement;

        // update facing based on horizontal direction
        if (Mathf.Abs(toPlayer.x) > 0.01f)
            facingDirection = toPlayer.x >= 0f ? 1 : -1;
        ApplyFacing();

        // still allow contact damage checks
        CheckTouchDamage();
    }

    private void ExitChasingState() { }

    // --- KNOCKBACK --------------------------------------------------------------------------------
    private void EnterKnockbackState()
    {
        knockbackStartTime = Time.time;
        movement.Set(knockbackSpeed.x * damageDirection, knockbackSpeed.y);
        if (aliveRb != null) aliveRb.velocity = movement;
        if (aliveAnim != null) aliveAnim.SetBool("Knockback", true);
    }

    private void UpdateKnockbackState()
    {
        if (Time.time >= knockbackStartTime + knockbackDuration)
        {
            SwitchState(State.Patrolling);
        }
    }

    private void ExitKnockbackState()
    {
        if (aliveAnim != null) aliveAnim.SetBool("Knockback", false);
    }

    // --- DEAD -------------------------------------------------------------------------------------
    private void EnterDeadState()
    {
        Instantiate(deathChunkParticle, alive.transform.position, deathChunkParticle.transform.rotation);
        Instantiate(deathBloodParticle, alive.transform.position, deathBloodParticle.transform.rotation);
        Destroy(gameObject);
    }

    private void UpdateDeadState() { }
    private void ExitDeadState() { }

    // --- COMMON -----------------------------------------------------------------------------------
    private void Damage(float[] attackDetailsIn)
    {
        currentHealth -= attackDetailsIn[0];

        Instantiate(hitParticle, alive.transform.position, Quaternion.Euler(0.0f, 0.0f, Random.Range(0.0f, 360.0f)));

        damageDirection = attackDetailsIn[1] > alive.transform.position.x ? -1 : 1;

        if (currentHealth > 0.0f)
            SwitchState(State.Knockback);
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

    private void ApplyFacing()
    {
        // Keep same flipping approach as other enemies
        if ((facingDirection == 1 && alive.transform.localScale.x < 0) || (facingDirection == -1 && alive.transform.localScale.x > 0))
            alive.transform.Rotate(0.0f, 180.0f, 0.0f);
    }

    private void SwitchState(State state)
    {
        // exit current
        switch (currentState)
        {
            case State.Patrolling: ExitPatrollingState(); break;
            case State.Chasing: ExitChasingState(); break;
            case State.Knockback: ExitKnockbackState(); break;
            case State.Dead: ExitDeadState(); break;
        }

        // enter new
        switch (state)
        {
            case State.Patrolling: EnterPatrollingState(); break;
            case State.Chasing: EnterChasingState(); break;
            case State.Knockback: EnterKnockbackState(); break;
            case State.Dead: EnterDeadState(); break;
        }

        currentState = state;
    }

    private void OnDrawGizmos()
    {
        if (patrolPointA != null && patrolPointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);
            Gizmos.DrawSphere(patrolPointA.position, 0.08f);
            Gizmos.DrawSphere(patrolPointB.position, 0.08f);
        }

        // chase radius only when chasing behaviour is enabled
        if (isChasingPlayer)
        {
            Gizmos.color = Color.red;
            if (alive != null)
                Gizmos.DrawWireSphere(alive.transform.position, chaseRadius);
            else
                Gizmos.DrawWireSphere(transform.position, chaseRadius);
        }
    }
}