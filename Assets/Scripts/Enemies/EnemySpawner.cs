using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple enemy spawner: spawns a prefab at configured spawn points every spawnInterval seconds.
/// Features:
/// - multiple spawn points (or spawns at this.transform if none assigned)
/// - limit concurrent spawned instances
/// - spawn N per wave
/// - Start/Stop control and SpawnNow() API
/// - Optionally destroy spawned instances when this spawner is destroyed
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Prefab & Spawn")]
    [Tooltip("Enemy prefab to spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Spawn points. If empty the spawner GameObject transform is used.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Timing")]
    [Tooltip("Seconds between spawn waves")]
    [SerializeField] private float spawnInterval = 5f;
    [Tooltip("Number of enemies spawned each wave")]
    [SerializeField] private int spawnPerWave = 1;
    [Tooltip("Start automatically on Awake/Start")]
    [SerializeField] private bool startOnAwake = true;

    [Header("Limits & Behavior")]
    [Tooltip("Maximum concurrent spawned enemies allowed. Set <= 0 for unlimited.")]
    [SerializeField] private int maxConcurrent = 10;
    [Tooltip("Randomize spawn position around chosen spawn point within this radius")]
    [SerializeField] private float spawnRadius = 0f;
    [Tooltip("If true choose spawn point randomly, otherwise use round-robin")]
    [SerializeField] private bool randomizeSpawnPoint = true;

    [Header("Cleanup")]
    [Tooltip("When true, all spawned instances created by this spawner will be destroyed when the spawner is destroyed.")]
    [SerializeField] private bool destroySpawnedWhenDestroyed = true;
    [Tooltip("Optional delay (seconds) applied when destroying spawned instances. 0 = immediate.")]
    [SerializeField] private float destroySpawnedDelay = 0f;

    // runtime
    private Coroutine _spawnRoutine;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private int _roundRobinIndex = 0;

    private void Start()
    {
        if (startOnAwake)
            StartSpawner();
    }

    private void OnDisable()
    {
        StopSpawner();
    }

    private void OnDestroy()
    {
        // Ensure spawner stopped
        StopSpawner();

        if (destroySpawnedWhenDestroyed)
        {
            // remove nulls first
            _spawned.RemoveAll(item => item == null);

            foreach (var go in _spawned)
            {
                if (go == null) continue;
                // optionally avoid destroying DontDestroyOnLoad objects
                // if (go.scene == null) continue; // not necessary, keep simple
                if (destroySpawnedDelay > 0f)
                    Destroy(go, destroySpawnedDelay);
                else
                    Destroy(go);
            }
        }

        _spawned.Clear();
    }

    /// <summary>
    /// Starts the automatic spawner loop.
    /// </summary>
    public void StartSpawner()
    {
        if (_spawnRoutine == null)
            _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>
    /// Stops automatic spawning.
    /// </summary>
    public void StopSpawner()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    /// <summary>
    /// Immediately spawn one wave (spawnPerWave enemies) regardless of timer.
    /// </summary>
    public void SpawnNow()
    {
        StartCoroutine(SpawnWaveOnce());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return StartCoroutine(SpawnWaveOnce());
            yield return new WaitForSeconds(Mathf.Max(0.01f, spawnInterval));
        }
    }

    private IEnumerator SpawnWaveOnce()
    {
        // cleanup destroyed references
        _spawned.RemoveAll(item => item == null);

        for (int i = 0; i < Mathf.Max(1, spawnPerWave); i++)
        {
            // enforce concurrent limit
            if (maxConcurrent > 0 && _spawned.Count >= maxConcurrent)
                yield break;

            SpawnOne();
        }

        yield return null;
    }

    private void SpawnOne()
    {
        if (enemyPrefab == null) return;

        Transform sp = GetSpawnTransform();
        Vector3 spawnPos = sp != null ? sp.position : transform.position;

        if (spawnRadius > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            spawnPos += new Vector3(offset.x, offset.y, 0f);
        }

        GameObject go = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        _spawned.Add(go);
    }

    private Transform GetSpawnTransform()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return this.transform;

        if (randomizeSpawnPoint)
        {
            int idx = Random.Range(0, spawnPoints.Length);
            return spawnPoints[Mathf.Clamp(idx, 0, spawnPoints.Length - 1)];
        }
        else
        {
            var t = spawnPoints[_roundRobinIndex % spawnPoints.Length];
            _roundRobinIndex = (_roundRobinIndex + 1) % Mathf.Max(1, spawnPoints.Length);
            return t;
        }
    }

    // Utility: expose current active spawn count
    public int CurrentActiveCount
    {
        get
        {
            _spawned.RemoveAll(item => item == null);
            return _spawned.Count;
        }
    }

    // Optional editor helper: quickly spawn a single enemy (useful from inspector with a custom button)
    public GameObject SpawnOneNow()
    {
        SpawnOne();
        return _spawned.Count > 0 ? _spawned[_spawned.Count - 1] : null;
    }
}