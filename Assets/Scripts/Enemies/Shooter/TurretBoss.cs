using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boss-capable turret implemented as a standalone burst shooter (single-shot / continuous turret behaviour removed).
/// Attach this to the turret GameObject and configure burst fields in the inspector (assign the prefab/spawn points).
/// </summary>
public class TurretBoss : MonoBehaviour
{
    [Header("Boss integration")]
    [SerializeField] private BossStats bossStats;
    [Tooltip("Optional particle when boss is hit.")]
    [SerializeField] private GameObject hitParticle;
    [Header("Optional: scene transition on boss defeat")]
    [Tooltip("Name of scene to load when this boss is defeated. Leave empty to disable.")]
    [SerializeField] private string sceneToLoadOnDefeat = "";
    [Tooltip("Delay (seconds) before loading the scene after boss death.")]
    [SerializeField] private float sceneLoadDelay = 1.0f;

    [Header("Burst shooting (TurretBoss)")]
    [Tooltip("Prefab for the projectile used by the burst shooter.")]
    [SerializeField] private GameObject burstBulletPrefab;
    [Tooltip("One or more spawn points. All spawn points will fire simultaneously for each shot in a burst.")]
    [SerializeField] private Transform[] burstBulletSpawnPoints;
    [Tooltip("Number of shots per burst. Each shot fires the bullets from all spawn points.")]
    [SerializeField] private int shotsPerBurst = 3;
    [Tooltip("Delay between individual shots inside a burst (seconds).")]
    [SerializeField] private float timeBetweenShots = 0.12f;
    [Tooltip("Delay between bursts (seconds).")]
    [SerializeField] private float timeBetweenBursts = 1.2f;
    [Tooltip("Delay before the first burst (seconds).")]
    [SerializeField] private float burstInitialDelay = 0f;
    [Tooltip("Start bursting automatically on Start.")]
    [SerializeField] private bool burstStartOnAwake = true;

    [Header("Direction / Alternation")]
    [Tooltip("Control how bullet direction is chosen. Uses TurretBullet.FireMode so you can pick cardinal directions, AimAtPlayer, InitialDirection or ExplicitTarget.")]
    [SerializeField] private TurretBullet.FireMode directionMode = TurretBullet.FireMode.InitialDirection;
    [Tooltip("When true, bullets will use their own 'aimAtPlayer' setting; otherwise bullets will use spawn point rotation or forced cardinal direction.")]
    [SerializeField] private bool bulletsUseTheirOwnAimSetting = true;

    private GameObject alive;
    private Coroutine _burstRoutine;
    private bool _bursting;
    private bool _isDead;

    private void Start()
    {
        alive = transform.Find("Alive")?.gameObject ?? gameObject;

        // Ensure bossStats component exists (prefer inspector assignment)
        if (bossStats == null)
        {
            bossStats = GetComponent<BossStats>();
            if (bossStats == null)
            {
                bossStats = gameObject.AddComponent<BossStats>();
                Debug.LogWarning("TurretBoss: BossStats was not assigned. A BossStats component was auto-added — consider configuring it in the inspector.");
            }
        }

        // Initialize and register UI
        bossStats.Initialize();
        BossHealthBar.Instance?.RegisterBoss(bossStats.maxHealth, bossStats.currentHealth);

        // Start burst shooter if requested
        if (burstStartOnAwake)
            StartBurst();
    }

    private void OnDestroy()
    {
        BossHealthBar.Instance?.UnregisterBoss();
    }

    /// <summary>
    /// Public API to start burst shooting.
    /// </summary>
    public void StartBurst()
    {
        if (_isDead || _bursting) return;
        if (burstBulletPrefab == null || burstBulletSpawnPoints == null || burstBulletSpawnPoints.Length == 0) return;

        _bursting = true;
        _burstRoutine = StartCoroutine(BurstRoutine());
    }

    /// <summary>
    /// Public API to stop burst shooting.
    /// </summary>
    public void StopBurst()
    {
        if (!_bursting) return;
        _bursting = false;
        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }
    }

    private IEnumerator BurstRoutine()
    {
        if (burstInitialDelay > 0f)
            yield return new WaitForSeconds(burstInitialDelay);

        while (_bursting && !_isDead)
        {
            for (int s = 0; s < shotsPerBurst && _bursting && !_isDead; s++)
            {
                // Spawn one "shot" which instantiates a bullet at every spawn point
                for (int i = 0; i < burstBulletSpawnPoints.Length; i++)
                {
                    Transform sp = burstBulletSpawnPoints[i];
                    if (sp == null) continue;

                    GameObject b = Instantiate(burstBulletPrefab, sp.position, sp.rotation);

                    // Prefer TurretBullet: set its FireMode directly when present
                    var turret = b.GetComponent<TurretBullet>();
                    if (turret != null)
                    {
                        turret.fireMode = directionMode;
                        continue;
                    }

                    // If the bullet has a BossBullet component, configure its aiming/direction.
                    var bossBullet = b.GetComponent<BossBullet>();
                    if (bossBullet != null)
                    {
                        // Respect bulletsUseTheirOwnAimSetting unless directionMode requests cardinal forcing.
                        if (directionMode == TurretBullet.FireMode.AimAtPlayer)
                        {
                            bossBullet.aimAtPlayer = true;
                        }
                        else if (directionMode == TurretBullet.FireMode.InitialDirection)
                        {
                            bossBullet.aimAtPlayer = bulletsUseTheirOwnAimSetting;
                            // leave initialDirection as-is (prefab)
                        }
                        else if (directionMode == TurretBullet.FireMode.ExplicitTarget)
                        {
                            // no explicit target available in burst mode; fallback to AimAtPlayer
                            bossBullet.aimAtPlayer = true;
                        }
                        else
                        {
                            // Force cardinal direction
                            bossBullet.aimAtPlayer = false;
                            switch (directionMode)
                            {
                                case TurretBullet.FireMode.Left:
                                    bossBullet.initialDirection = Vector2.left;
                                    break;
                                case TurretBullet.FireMode.Right:
                                    bossBullet.initialDirection = Vector2.right;
                                    break;
                                case TurretBullet.FireMode.Up:
                                    bossBullet.initialDirection = Vector2.up;
                                    break;
                                case TurretBullet.FireMode.Down:
                                    bossBullet.initialDirection = Vector2.down;
                                    break;
                            }
                        }

                        continue;
                    }

                    // Otherwise (no known bullet component) apply fallback:
                    if (!bulletsUseTheirOwnAimSetting)
                    {
                        var t = b.transform;
                        var e = t.eulerAngles;

                        // Force cardinal direction by manipulating rotation similar to previous logic
                        if (directionMode == TurretBullet.FireMode.Left)
                        {
                            t.eulerAngles = new Vector3(e.x, 180f, e.z);
                        }
                        else if (directionMode == TurretBullet.FireMode.Right)
                        {
                            t.eulerAngles = new Vector3(e.x, 0f, e.z);
                        }
                        else if (directionMode == TurretBullet.FireMode.Up)
                        {
                            t.eulerAngles = new Vector3(e.x, e.y, 90f);
                        }
                        else if (directionMode == TurretBullet.FireMode.Down)
                        {
                            t.eulerAngles = new Vector3(e.x, e.y, 270f);
                        }
                        // else AimAtPlayer / InitialDirection / ExplicitTarget — leave spawn rotation
                    }
                }

                // wait between shots in the same burst
                if (timeBetweenShots <= 0f)
                    yield return null;
                else
                    yield return new WaitForSeconds(timeBetweenShots);
            }

            // wait between bursts
            if (timeBetweenBursts <= 0f)
                yield return null;
            else
                yield return new WaitForSeconds(timeBetweenBursts);
        }
    }

    /// <summary>
    /// Called by bullets or other damage sources via SendMessage("Damage", attackDetails).
    /// attackDetails[0] = damage amount (float).
    /// </summary>
    private void Damage(float[] attackDetails)
    {
        if (_isDead) return;
        if (bossStats == null || attackDetails == null || attackDetails.Length == 0) return;

        bool died = bossStats.ApplyDamage(attackDetails[0]);

        if (hitParticle)
            Instantiate(hitParticle, alive.transform.position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

        // Update UI handled by BossStats.ApplyDamage -> BossHealthBar.Instance.UpdateHealth

        if (died)
        {
            _isDead = true;

            // stop burst shooting
            StopBurst();

            // Death FX from BossStats (if assigned)
            if (bossStats.deathChunkParticle) Instantiate(bossStats.deathChunkParticle, alive.transform.position, bossStats.deathChunkParticle.transform.rotation);
            if (bossStats.deathBloodParticle) Instantiate(bossStats.deathBloodParticle, alive.transform.position, bossStats.deathBloodParticle.transform.rotation);

            BossHealthBar.Instance?.UnregisterBoss();

            if (!string.IsNullOrEmpty(sceneToLoadOnDefeat))
                StartCoroutine(LoadSceneAfterDelay(sceneToLoadOnDefeat, sceneLoadDelay));
            else
                Destroy(gameObject);
        }
    }

    private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}