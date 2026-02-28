using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Single enemy controller that can behave as Basic or Chasing enemy.
// Toggle "isChasingPlayer" in the Inspector to enable chasing behaviour.
public class ChasingEnemyController : MonoBehaviour
{
    private enum State
    {
        Moving,
        Chasing,
        Knockback,
        Dead
    }

    private State currentState;

    // Prevent enemies colliding with each other by disabling Enemy-Enemy collisions once.
    private static bool s_enemyCollisionsIgnored = false;

    // Track Collider2D for pairwise IgnoreCollision (works with Box/Circle/Composite/Tilemap etc.)
    private static List<Collider2D> s_enemyColliders = new List<Collider2D>();
    private readonly List<Collider2D> _localColliders = new List<Collider2D>();

    [SerializeField]
    private float
        groundCheckDistance,
        wallCheckDistance,
        movementSpeed,
        maxHealth,
        knockbackDuration,
        lastTouchDamageTime,
        touchDamageCooldown,
        touchDamage,
        touchDamageWidth,
        touchDamageHeight;

    [Header("Behavior")]
    [Tooltip("When true the enemy will chase the player within chaseRadius. When false it behaves like the basic patrolling enemy.")]
    public bool isChasingPlayer = false;

    [Header("Chase")]
    [SerializeField]
    private float chaseRadius = 4f;             // radius to start chasing
    [SerializeField]
    private float chaseSpeed = 3.5f;            // horizontal speed while chasing
    [SerializeField]
    private GameObject chaseRadiusObject;       // optional visual GameObject (will be scaled at Start)

    [SerializeField]
    private Transform
        groundCheck,
        wallCheck,
        touchDamageCheck;
    [SerializeField]
    private LayerMask
        whatIsGround,
        whatIsPlayer;
    [SerializeField]
    private Vector2 knockbackSpeed;
    [SerializeField]
    private GameObject
        hitParticle,
        deathChunkParticle,
        deathBloodParticle;

    private float
        currentHealth,
        knockbackStartTime;

    private float[] attackDetails = new float[2];

    private int
        facingDirection,
        damageDirection;

    private Vector2
        movement,
        touchDamageBotLeft,
        touchDamageTopRight;

    private bool
        groundDetected,
        wallDetected;

    private GameObject alive;
    private Rigidbody2D aliveRb;
    private Animator aliveAnim;

    // runtime chase target
    private Transform chaseTarget;

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

        // Register this enemy's Collider2D components to ignore collisions with other enemies
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
        alive = transform.Find("Alive").gameObject;
        aliveRb = alive.GetComponent<Rigidbody2D>();
        aliveAnim = alive.GetComponent<Animator>();

        currentHealth = maxHealth;
        facingDirection = 1;

        // if a visual GameObject is assigned and chasing is enabled, set its scale so it represents the chase radius
        if (isChasingPlayer && chaseRadiusObject != null)
        {
            // set scale so that diameter == 2 * chaseRadius (assumes the object's sprite/visual is 1 unit size)
            chaseRadiusObject.transform.localScale = Vector3.one * chaseRadius * 2f;
        }
        else if (chaseRadiusObject != null)
        {
            // hide/disable visual when not using chase behaviour
            chaseRadiusObject.SetActive(false);
        }

        // start in Moving state
        SwitchState(State.Moving);
    }

    private void Update()
    {
        switch (currentState)
        {
            case State.Moving:
                UpdateMovingState();
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

    //--WALKING STATE--------------------------------------------------------------------------------

    private void EnterMovingState()
    {
        chaseTarget = null;
    }

    private void UpdateMovingState()
    {
        groundDetected = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, whatIsGround);
        wallDetected = Physics2D.Raycast(wallCheck.position, transform.right, wallCheckDistance, whatIsGround);

        CheckTouchDamage();

        // If chasing behaviour is enabled, check for player inside chase radius and switch to Chasing
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

        if(!groundDetected || wallDetected)
        {
            Flip();
        }
        else
        {
            movement.Set(movementSpeed * facingDirection, aliveRb.velocity.y);
            aliveRb.velocity = movement;
        }
    }

    private void ExitMovingState()
    {

    }

    //--CHASING STATE--------------------------------------------------------------------------------

    private void EnterChasingState()
    {
        // optionally play an anim parameter here e.g. aliveAnim.SetBool("Chasing", true);
    }

    private void UpdateChasingState()
    {
        // if chasing was disabled mid-play, return to moving
        if (!isChasingPlayer)
        {
            SwitchState(State.Moving);
            return;
        }

        // If there's no valid chase target try to reacquire one
        if (chaseTarget == null)
        {
            Collider2D playerHit = Physics2D.OverlapCircle(alive.transform.position, chaseRadius, whatIsPlayer);
            if (playerHit != null)
                chaseTarget = playerHit.transform;
            else
            {
                // nothing to chase -> return to moving (patrol)
                SwitchState(State.Moving);
                return;
            }
        }

        // If target moved out of radius, stop chasing
        float dist = Vector2.Distance(alive.transform.position, chaseTarget.position);
        if (dist > chaseRadius)
        {
            chaseTarget = null;
            SwitchState(State.Moving);
            return;
        }

        // Move horizontally toward player
        float dir = Mathf.Sign(chaseTarget.position.x - alive.transform.position.x);
        facingDirection = dir >= 0 ? 1 : -1;
        // ensure sprite faces the correct direction
        if ((facingDirection == 1 && alive.transform.localScale.x < 0) || (facingDirection == -1 && alive.transform.localScale.x > 0))
        {
            // If your sprite flipping logic relies on child rotation like the original Flip(), call Flip() instead.
            alive.transform.Rotate(0.0f, 180.0f, 0.0f);
        }
        movement.Set(chaseSpeed * facingDirection, aliveRb.velocity.y);
        aliveRb.velocity = movement;

        // still allow touch damage while chasing
        CheckTouchDamage();
    }

    private void ExitChasingState()
    {
        // aliveAnim.SetBool("Chasing", false);
    }

    //--KNOCKBACK STATE-------------------------------------------------------------------------------

    private void EnterKnockbackState()
    {
        knockbackStartTime = Time.time;
        movement.Set(knockbackSpeed.x * damageDirection, knockbackSpeed.y);
        aliveRb.velocity = movement;
        aliveAnim.SetBool("Knockback", true);
    }

    private void UpdateKnockbackState()
    {
        if(Time.time >= knockbackStartTime + knockbackDuration)
        {
            SwitchState(State.Moving);
        }
    }

    private void ExitKnockbackState()
    {
        aliveAnim.SetBool("Knockback", false);
    }

    //--DEAD STATE---------------------------------------------------------------------------------------

    private void EnterDeadState()
    {
        Instantiate(deathChunkParticle, alive.transform.position, deathChunkParticle.transform.rotation);
        Instantiate(deathBloodParticle, alive.transform.position, deathBloodParticle.transform.rotation);
        Destroy(gameObject);
    }

    private void UpdateDeadState()
    {

    }

    private void ExitDeadState()
    {

    }

    //--OTHER FUNCTIONS--------------------------------------------------------------------------------

    private void Damage(float[] attackDetails)
    {
        currentHealth -= attackDetails[0];

        Instantiate(hitParticle, alive.transform.position, Quaternion.Euler(0.0f, 0.0f, Random.Range(0.0f, 360.0f)));

        if (attackDetails[1] > alive.transform.position.x)
        {
            damageDirection = -1;
        }
        else
        {
            damageDirection = 1;
        }

        if(currentHealth > 0.0f)
        {
            SwitchState(State.Knockback);
        }
        else if(currentHealth <= 0.0f)
        {
            SwitchState(State.Dead);
        }
    }

    private void CheckTouchDamage()
    {
        if (Time.time >= lastTouchDamageTime + touchDamageCooldown)
        {
            touchDamageBotLeft.Set(touchDamageCheck.position.x - (touchDamageWidth / 2), touchDamageCheck.position.y - (touchDamageHeight / 2));
            touchDamageTopRight.Set(touchDamageCheck.position.x + (touchDamageWidth / 2), touchDamageCheck.position.y + (touchDamageHeight / 2));

            Collider2D hit = Physics2D.OverlapArea(touchDamageBotLeft, touchDamageTopRight, whatIsPlayer);

            if(hit != null)
            {
                lastTouchDamageTime = Time.time;
                attackDetails[0] = touchDamage;
                attackDetails[1] = alive.transform.position.x;
                hit.SendMessage("Damage", attackDetails);
            }
        }
    }

    private void Flip()
    {
        facingDirection *= -1;
        alive.transform.Rotate(0.0f, 180.0f, 0.0f);

    }

    private void SwitchState(State state)
    {
        switch (currentState)
        {
            case State.Moving:
                ExitMovingState();
                break;
            case State.Chasing:
                ExitChasingState();
                break;
            case State.Knockback:
                ExitKnockbackState();
                break;
            case State.Dead:
                ExitDeadState();
                break;
        }

        switch (state)
        {
            case State.Moving:
                EnterMovingState();
                break;
            case State.Chasing:
                EnterChasingState();
                break;
            case State.Knockback:
                EnterKnockbackState();
                break;
            case State.Dead:
                EnterDeadState();
                break;
        }

        currentState = state;
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
            Gizmos.DrawLine(groundCheck.position, new Vector2(groundCheck.position.x, groundCheck.position.y - groundCheckDistance));
        if (wallCheck != null)
            Gizmos.DrawLine(wallCheck.position, new Vector2(wallCheck.position.x + wallCheckDistance, wallCheck.position.y));

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

        // draw chase radius only when chasing behaviour is enabled
        if (isChasingPlayer)
        {
            if (alive != null)
                Gizmos.DrawWireSphere(alive.transform.position, chaseRadius);
            else
                Gizmos.DrawWireSphere(transform.position, chaseRadius);
        }
    }
}