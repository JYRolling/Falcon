using System.Collections;
using UnityEngine;

// Simple continuous shooter enemy.
// - Attach to an enemy GameObject.
// - Assign a bullet prefab (e.g. existing `BossBullet`), one or more `bulletSpawnPoints` and set `fireRate` (bullets per second).
 // - The bullet prefab must handle collisions (the provided `BossBullet` already damages Player and disappears on Ground).
public class ContinuousShootingEnemyController : MonoBehaviour
{
    private enum FireDirectionMode
    {
        UseSpawnRotation,
        ForceLeft,
        ForceRight,
        ForceUp,
        ForceDown
    }

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
    [Tooltip("Control how bullet direction is chosen.")]
    [SerializeField] private FireDirectionMode directionMode = FireDirectionMode.UseSpawnRotation;

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

                // If the bullet has a BossBullet component, configure its aiming/direction directly.
                var bossBullet = b.GetComponent<BossBullet>();
                if (bossBullet != null)
                {
                    // Default behavior: let bullet use its own aim setting unless we override below.
                    bossBullet.aimAtPlayer = bulletsUseTheirOwnAimSetting;

                    bool forceDir = false;
                    Vector2 forcedDirection = Vector2.right;

                    switch (directionMode)
                    {
                        case FireDirectionMode.UseSpawnRotation:
                            // do not force direction - keep spawn rotation or bullet's own behavior
                            forceDir = false;
                            break;
                        case FireDirectionMode.ForceLeft:
                            forceDir = true;
                            forcedDirection = Vector2.left;
                            break;
                        case FireDirectionMode.ForceRight:
                            forceDir = true;
                            forcedDirection = Vector2.right;
                            break;
                        case FireDirectionMode.ForceUp:
                            forceDir = true;
                            forcedDirection = Vector2.up;
                            break;
                        case FireDirectionMode.ForceDown:
                            forceDir = true;
                            forcedDirection = Vector2.down;
                            break;
                    }

                    if (forceDir)
                    {
                        bossBullet.aimAtPlayer = false;
                        bossBullet.initialDirection = forcedDirection;
                    }
                }
                else
                {
                    // If BossBullet not found and we want to force direction, fall back to rotating the instantiated object.
                    if (!bulletsUseTheirOwnAimSetting)
                    {
                        var t = b.transform;
                        var e = t.eulerAngles;

                        bool wantLeft = (directionMode == FireDirectionMode.ForceLeft);
                        bool wantRight = (directionMode == FireDirectionMode.ForceRight);
                        bool wantUp = (directionMode == FireDirectionMode.ForceUp);
                        bool wantDown = (directionMode == FireDirectionMode.ForceDown);

                        if (wantLeft && !wantUp && !wantDown)
                        {
                            // Flip horizontally (keeps same z)
                            t.eulerAngles = new Vector3(e.x, 180f, e.z);
                        }
                        else if (wantRight && !wantUp && !wantDown)
                        {
                            // Ensure no horizontal flip
                            t.eulerAngles = new Vector3(e.x, 0f, e.z);
                        }
                        else if (wantUp && !wantLeft && !wantRight)
                        {
                            // Rotate to point up (z = 90)
                            t.eulerAngles = new Vector3(e.x, e.y, 90f);
                        }
                        else if (wantDown && !wantLeft && !wantRight)
                        {
                            // Rotate to point down (z = 270)
                            t.eulerAngles = new Vector3(e.x, e.y, 270f);
                        }
                        // If multiple flags are true or none matched, keep spawn rotation (do nothing)
                    }
                }
            }

            if (interval == Mathf.Infinity) yield break;
            if (interval <= 0f) yield return null;
            else yield return new WaitForSeconds(interval);
        }
    }
}