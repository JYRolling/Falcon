using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy pattern (updated):
/// - Shoot configurable spread (spreadCount) for configurable rounds (roundsPerShoot)
///   each round spawns the whole spread simultaneously.
/// - Move to next patrol point
/// - Repeat (no laser sweep)
/// - Optional BossStats registration for BossHealthBar.
/// - No touch damage.
/// </summary>
public class PatrolSpreadLaserEnemy : MonoBehaviour
{
    [Header("Patrol Points")]
    [Tooltip("Assign one or more points. Enemy will move sequentially and loop.")]
    [SerializeField] private Transform[] patrolPoints;
    [Tooltip("Movement speed when moving between points (units/sec)")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Pause after reaching a point (seconds)")]
    [SerializeField] private float pauseOnPoint = 0.25f;

    [Header("Spread Shooting")]
    [Tooltip("Prefab for bullets (prefer Rigidbody2D on prefab)")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Spawn transform (child) where bullets originate")]
    [SerializeField] private Transform bulletSpawnPoint;
    [Tooltip("Number of projectiles per spread (e.g. 3)")]
    [SerializeField] private int spreadCount = 3;
    [Tooltip("Total cone angle in degrees for the spread (e.g. 30 => +/-15)")]
    [SerializeField] private float spreadConeAngle = 30f;
    [Tooltip("How many spread rounds to fire at each shoot step")]
    [SerializeField] private int roundsPerShoot = 3;
    [Tooltip("Delay between each spread round (seconds)")]
    [SerializeField] private float timeBetweenRounds = 0.25f;
    [Tooltip("Initial speed applied to spawned bullets")]
    [SerializeField] private float bulletSpeed = 10f;
    [Tooltip("Optional multiplier applied to all spawned bullet velocities")]
    [SerializeField] private float bulletSpeedMultiplier = 1f;

    [Header("Behavior")]
    [Tooltip("Start the pattern automatically")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("Initial delay before first loop")]
    [SerializeField] private float initialDelay = 0.2f;

    [Header("Boss UI (optional)")]
    [Tooltip("Assign BossStats to enable BossHealthBar registration")]
    [SerializeField] private BossStats bossStats;

    [Header("Death / Drops")]
    [SerializeField] private GameObject deathParticle;
    [SerializeField] private string sceneToLoadOnDefeat = "";
    [SerializeField] private float sceneLoadDelay = 1f;

    // runtime
    private Transform _player;
    private Vector3 _lastKnownPlayerPos; // used when player reference is temporarily lost (respawn)
    private int _currentPatrolIndex = 0;
    private bool _isDead = false;
    private float[] _attackDetails = new float[2];

    private void Start()
    {
        // find player
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            _player = p.transform;
            _lastKnownPlayerPos = _player.position;
        }

        if (bulletSpawnPoint == null)
            bulletSpawnPoint = transform;

        // BossStats optional
        if (bossStats == null)
            bossStats = GetComponent<BossStats>();

        if (bossStats != null)
        {
            bossStats.Initialize();
            BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);
        }

        if (startOnAwake)
            StartCoroutine(MainPatternLoop(initialDelay));
    }

    private void Update()
    {
        // If player was destroyed (respawn in progress) try to reacquire each frame.
        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                _player = p.transform;
                _lastKnownPlayerPos = _player.position;
            }
        }

        // Keep bullet spawn aiming at the player at all times (if assigned).
        // If the Player reference is missing use _lastKnownPlayerPos to keep aiming stable.
        if (bulletSpawnPoint != null)
        {
            Vector2 targetPos = (_player != null) ? (Vector2)_player.position : (Vector2)_lastKnownPlayerPos;
            Vector2 dir = targetPos - (Vector2)bulletSpawnPoint.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                bulletSpawnPoint.right = dir.normalized; // orient spawn's X axis toward player
            }
        }

        // update last known if player exists
        if (_player != null)
            _lastKnownPlayerPos = _player.position;
    }

    private IEnumerator MainPatternLoop(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // guard: require at least one patrol point
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning($"{nameof(PatrolSpreadLaserEnemy)}: No patrol points assigned. Enemy will stay in place and perform attacks.");
        }

        while (!_isDead)
        {
            // 1) shoot spread rounds aimed at player
            yield return StartCoroutine(DoSpreadRounds());

            // 2) move to next point
            yield return StartCoroutine(MoveToNextPoint());

            // 3) shoot again at arrival (keeps aggressive pacing)
            yield return StartCoroutine(DoSpreadRounds());

            // 4) move to next point again
            yield return StartCoroutine(MoveToNextPoint());

            // small rest before next cycle (optional)
            yield return null;
        }
    }

    // Spread shooting coroutine: each round spawns the full spread simultaneously
    private IEnumerator DoSpreadRounds()
    {
        if (bulletPrefab == null || bulletSpawnPoint == null)
            yield break;

        for (int r = 0; r < Mathf.Max(1, roundsPerShoot); r++)
        {
            // ensure we have a target position: try reacquire if needed
            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    _player = p.transform;
                    _lastKnownPlayerPos = _player.position;
                }
            }

            // compute base direction toward player from spawn (spawn is kept aimed in Update())
            Vector2 spawnPos = bulletSpawnPoint.position;
            Vector2 toPlayer = (_player != null) ? (Vector2)(_player.position) - spawnPos : (Vector2)_lastKnownPlayerPos - spawnPos;
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = Vector2.right;

            float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

            // spawn entire spread at once
            SpawnSpreadAtAngle(baseAngle);

            // wait between rounds
            if (timeBetweenRounds > 0f)
                yield return new WaitForSeconds(timeBetweenRounds);
            else
                yield return null;
        }
    }

    private void SpawnSpreadAtAngle(float baseAngle)
    {
        if (spreadCount <= 1)
        {
            SpawnBulletAtAngle(baseAngle);
            return;
        }

        float half = spreadConeAngle * 0.5f;
        float step = (spreadCount == 1) ? 0f : (spreadConeAngle / (spreadCount - 1));
        for (int i = 0; i < spreadCount; i++)
        {
            float angle = baseAngle - half + step * i;
            SpawnBulletAtAngle(angle);
        }
    }

    private void SpawnBulletAtAngle(float angleDeg)
    {
        if (bulletPrefab == null || bulletSpawnPoint == null) return;

        Vector2 spawnPos = bulletSpawnPoint.position;
        Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg - 90f); // match rotation style used elsewhere
        GameObject go = Instantiate(bulletPrefab, spawnPos, rot);
        if (go == null) return;

        // if prefab contains a guided script (like BossBullet) try to pass target (use last-known if needed)
        var bossBullet = go.GetComponent<BossBullet>();
        if (bossBullet != null)
        {
            Vector3 target = (_player != null) ? _player.position : _lastKnownPlayerPos;
            bossBullet.SetTargetPosition(target);
            // if BossBullet handles its own speed, leave it
            return;
        }

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            dir.Normalize();
            float speed = Mathf.Max(0.0001f, bulletSpeed) * Mathf.Max(0.0001f, bulletSpeedMultiplier);
            rb.velocity = dir * speed;
        }
    }

    // Move to next patrol point coroutine (linear move)
    private IEnumerator MoveToNextPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            // if no points, just pause a bit
            if (pauseOnPoint > 0f) yield return new WaitForSeconds(pauseOnPoint);
            yield break;
        }

        Transform target = patrolPoints[_currentPatrolIndex];
        if (target == null)
        {
            // advance index anyway
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
            yield break;
        }

        Vector3 start = transform.position;
        Vector3 end = target.position;
        float dist = Vector3.Distance(start, end);
        if (dist <= 0.01f)
        {
            // arrived instantly; advance index and pause
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
            if (pauseOnPoint > 0f) yield return new WaitForSeconds(pauseOnPoint);
            yield break;
        }

        float travelTime = dist / Mathf.Max(0.001f, moveSpeed);
        float t = 0f;
        while (t < travelTime && !_isDead)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / travelTime);
            transform.position = Vector3.Lerp(start, end, normalized);
            yield return null;
        }

        transform.position = end;
        // advance index for next target
        _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;

        if (pauseOnPoint > 0f)
            yield return new WaitForSeconds(pauseOnPoint);
    }

    // Receive Damage via project's float[] attackDetails (same as BossEnemyController)
    public void Damage(float[] attackDetails)
    {
        if (_isDead) return;
        if (attackDetails == null || attackDetails.Length == 0) return;

        float dmg = attackDetails[0];
        bool died = false;

        if (bossStats != null)
        {
            died = bossStats.ApplyDamage(dmg);
        }
        else
        {
            // local health fallback if BossStats not provided
            _currentLocalHealth -= dmg;
            if (_currentLocalHealth <= 0f) died = true;
        }

        if (died) Die();
    }

    // local fallback health (used only when bossStats not assigned)
    [SerializeField] private float _currentLocalHealth = 50f;

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // unregister health UI if any
        BossHealthBar.Instance?.UnregisterBoss();

        // drop items if present
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
}