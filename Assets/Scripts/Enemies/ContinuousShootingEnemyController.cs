using System.Collections;
using UnityEngine;

public class ContinuousShootingEnemyController : MonoBehaviour
{
    [Header("Shooting")]
    [Tooltip("Prefab for the projectile. Prefer using the existing BossBullet prefab.")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("One or more spawn points. All spawn points will fire simultaneously each shot.")]
    [SerializeField] private Transform[] bulletSpawnPoints;
    [Tooltip("Bullets per second. Set to 0 to disable.")]
    [SerializeField] private float fireRate = 2f;
    [Tooltip("Delay before the first shot (seconds).")]
    [SerializeField] private float initialDelay = 0f;
    [Tooltip("When true, bullets will use their own 'aimAtPlayer' setting; otherwise bullets will use spawn point rotation.")]
    [SerializeField] private bool bulletsUseTheirOwnAimSetting = true;
    [Tooltip("Start shooting automatically on Enable/Start.")]
    [SerializeField] private bool startOnAwake = true;

    [Header("Direction / Alternation")]
    [Tooltip("Control how bullet direction is chosen. Uses TurretBullet.FireMode so you can pick cardinal directions, AimAtPlayer, InitialDirection or ExplicitTarget.")]
    [SerializeField] private TurretBullet.FireMode directionMode = TurretBullet.FireMode.InitialDirection;

    private Coroutine _shootRoutine;
    private bool _shooting;

    private void OnEnable()
    {
        if (startOnAwake)
            StartShooting();
    }

    private void OnDisable()
    {
        StopShooting();
    }

    private void OnValidate()
    {
        // Clamp to sensible values in inspector
        if (fireRate < 0f) fireRate = 0f;
        if (initialDelay < 0f) initialDelay = 0f;
    }

    // Start continuous shooting (public so other scripts can trigger it)
    public void StartShooting()
    {
        if (_shooting) return;
        if (bulletPrefab == null || bulletSpawnPoints == null || bulletSpawnPoints.Length == 0) return;

        _shooting = true;
        _shootRoutine = StartCoroutine(ShootRoutine());
    }

    // Stop continuous shooting
    public void StopShooting()
    {
        if (!_shooting) return;
        _shooting = false;
        if (_shootRoutine != null)
        {
            StopCoroutine(_shootRoutine);
            _shootRoutine = null;
        }
    }

    // Change fire rate at runtime (bullets per second). Passing <= 0 stops shooting.
    public void SetFireRate(float bulletsPerSecond)
    {
        fireRate = Mathf.Max(0f, bulletsPerSecond);

        // Restart routine to apply new interval immediately
        if (_shooting)
        {
            StopShooting();
            if (fireRate > 0f)
                StartShooting();
        }
    }

    private IEnumerator ShootRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        float interval = (fireRate > 0f) ? 1f / fireRate : Mathf.Infinity;

        while (_shooting)
        {
            if (bulletPrefab == null) break;

            // Spawn one "shot" which instantiates a bullet at every spawn point
            for (int i = 0; i < bulletSpawnPoints.Length; i++)
            {
                Transform sp = bulletSpawnPoints[i];
                if (sp == null) continue;

                GameObject b = Instantiate(bulletPrefab, sp.position, sp.rotation);

                // Prefer TurretBullet: set its FireMode directly when present
                var turret = b.GetComponent<TurretBullet>();
                if (turret != null)
                {
                    // Apply chosen direction mode to turret bullet
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
                        // no explicit target available in continuous mode; fallback to AimAtPlayer
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

                // Otherwise (no known bullet component) apply old fallback:
                if (!bulletsUseTheirOwnAimSetting)
                {
                    var t = b.transform;
                    var e = t.eulerAngles;

                    // NOTE: use the existing TurretBullet.FireMode values (Left/Right/Up/Down)
                    if (directionMode == TurretBullet.FireMode.Left || directionMode == TurretBullet.FireMode.Left)
                    {
                        t.eulerAngles = new Vector3(e.x, 180f, e.z);
                    }
                    else if (directionMode == TurretBullet.FireMode.Right || directionMode == TurretBullet.FireMode.Right)
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
                    // else UseSpawnRotation / AimAtPlayer / InitialDirection — leave spawn rotation
                }
            }

            if (interval == Mathf.Infinity) yield break;
            if (interval <= 0f) yield return null;
            else yield return new WaitForSeconds(interval);
        }
    }
}