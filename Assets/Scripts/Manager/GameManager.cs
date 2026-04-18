using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField]
    private Transform respawnPoint;
    [SerializeField]
    private GameObject player;
    [SerializeField]
    private float respawnTime;

    // Optional: assign the HealthBar in the inspector to avoid runtime Find calls
    [SerializeField]
    private HealthBar healthBar;
    public HealthBar PlayerHealthBar => healthBar;

    // Teleport target for F1. If null, falls back to respawnPoint.
    [Header("Debug / Teleport")]
    [Tooltip("Optional target to teleport the Player to when pressing F1. If null uses Respawn Point.")]
    [SerializeField] private Transform teleportTarget;

    private float respawnTimeStart;

    private bool respawn;

    private CinemachineVirtualCamera CVC;

    // Track current active player instance in scene (may be a spawned prefab)
    private GameObject currentPlayer;
    public GameObject CurrentPlayer => currentPlayer;

    // Event fired when a player GameObject has been spawned / respawned.
    public event Action<GameObject> PlayerRespawned;

    private void Awake()
    {
        // singleton so other scripts (checkpoints) can access GameManager
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        var camObj = GameObject.Find("Player Camera");
        if (camObj != null)
            CVC = camObj.GetComponent<CinemachineVirtualCamera>();
        else
            Debug.LogWarning("GameManager: 'Player Camera' GameObject not found in scene.");

        // Try to find the active player in scene at start
        currentPlayer = GameObject.FindGameObjectWithTag("Player");
        if (currentPlayer == null)
            Debug.Log("GameManager: No active Player found at Start. Will set when player is spawned.");
    }

    private void Update()
    {
        CheckRespawn();
        CheckTeleportInput();
        CheckInvulnerabilityInput();
    }

    public void Respawn()
    {
        respawnTimeStart = Time.time;
        respawn = true;
    }

    private void CheckRespawn()
    {
        if (Time.time >= respawnTimeStart + respawnTime && respawn)
        {
            if (player != null && respawnPoint != null)
            {
                var playerTemp = Instantiate(player, respawnPoint.position, respawnPoint.rotation);

                // remember current player instance
                currentPlayer = playerTemp;

                // Ensure Cinemachine follows new player
                if (CVC != null)
                    CVC.m_Follow = playerTemp.transform;

                // Assign existing scene HealthBar (if present) to the new player so the UI updates
                var ps = playerTemp.GetComponent<PlayerStats>();
                if (ps != null)
                {
                    HealthBar hb = GetScenePlayerHealthBar();

                    if (hb != null)
                    {
                        // assign on next frame to allow player's Start() to run
                        StartCoroutine(AssignHealthBarNextFrame(playerTemp, hb));
                    }
                    else
                    {
                        Debug.LogWarning("GameManager: No HealthBar found in scene to assign to respawned player.");
                    }
                }
                else
                {
                    Debug.LogWarning("GameManager: Spawned player prefab does not contain PlayerStats component.");
                }

                // Notify subscribers that a new player has been spawned
                PlayerRespawned?.Invoke(playerTemp);
            }
            else
            {
                Debug.LogWarning("GameManager: player or respawnPoint is not assigned.");
            }

            respawn = false;
        }
    }

    // Called by checkpoints to update where the player will respawn
    public void SetRespawnPoint(Transform newRespawnPoint)
    {
        if (newRespawnPoint == null)
        {
            Debug.LogWarning("GameManager.SetRespawnPoint called with null transform.");
            return;
        }

        respawnPoint = newRespawnPoint;
        Debug.Log($"Respawn point updated to '{newRespawnPoint.name}' at {newRespawnPoint.position}");
    }

    private IEnumerator AssignHealthBarNextFrame(GameObject playerObj, HealthBar hb)
    {
        yield return null;

        if (playerObj == null || hb == null)
        {
            Debug.LogWarning("AssignHealthBarNextFrame: missing playerObj or hb");
            yield break;
        }

        if (!IsValidPlayerHealthBar(hb))
        {
            hb = GetScenePlayerHealthBar();
            if (hb == null)
            {
                Debug.LogWarning("AssignHealthBarNextFrame: no valid scene HealthBar found.");
                yield break;
            }
        }

        // Walk up and enable all parent GameObjects so activeInHierarchy becomes true.
        Transform t = hb.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }

        // Ensure any CanvasGroup on root is visible
        var rootCanvas = hb.GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            var cg = rootCanvas.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha <= 0f)
                cg.alpha = 1f;
        }

        var ps = playerObj.GetComponent<PlayerStats>();
        if (ps != null)
        {
            ps.AssignHealthBar(hb);
            Debug.Log($"AssignHealthBarNextFrame: Assigned HealthBar '{hb.gameObject.name}' to player '{playerObj.name}' activeInHierarchy={hb.gameObject.activeInHierarchy}");
        }
        else
        {
            Debug.LogWarning("AssignHealthBarNextFrame: PlayerStats not found on spawned player.");
        }
    }

    private bool IsValidPlayerHealthBar(HealthBar hb)
    {
        if (hb == null) return false;
        if (hb is BossHealthBar) return false;
        return hb.gameObject.scene.IsValid();
    }

    private HealthBar GetScenePlayerHealthBar()
    {
        if (IsValidPlayerHealthBar(healthBar))
            return healthBar;

        // qualify UnityEngine.Object to avoid ambiguity with System.Object
        var bars = UnityEngine.Object.FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hb in bars)
        {
            if (!IsValidPlayerHealthBar(hb))
                continue;

            healthBar = hb;
            return hb;
        }

        return null;
    }

    // Teleport helpers

    // Called when F1 is pressed
    private void CheckTeleportInput()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Transform target = teleportTarget != null ? teleportTarget : respawnPoint;
            if (target == null)
            {
                Debug.LogWarning("GameManager: No teleport target or respawnPoint assigned.");
                return;
            }

            TeleportPlayerTo(target.position);
        }
    }

    // Teleport current active player to a world position (safe for Rigidbody2D).
    public void TeleportPlayerTo(Vector3 worldPosition)
    {
        // Ensure we have a reference to the active player
        if (currentPlayer == null)
            currentPlayer = GameObject.FindGameObjectWithTag("Player");

        if (currentPlayer == null)
        {
            Debug.LogWarning("GameManager.TeleportPlayerTo: No active player found to teleport.");
            return;
        }

        // If player uses Rigidbody2D, move it safely and clear velocity
        var rb2d = currentPlayer.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
            rb2d.position = worldPosition;
            // Also set transform to match
            currentPlayer.transform.position = worldPosition;
        }
        else
        {
            // Fallback: set transform directly
            currentPlayer.transform.position = worldPosition;
        }

        // If using Cinemachine, ensure camera target remains correct (m_Follow normally unchanged)
        if (CVC != null)
            CVC.m_Follow = currentPlayer.transform;

        Debug.Log($"GameManager: Teleported player '{currentPlayer.name}' to {worldPosition}");
    }

    // Optional: expose teleport to a Transform
    public void TeleportPlayerTo(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("TeleportPlayerTo called with null target.");
            return;
        }
        TeleportPlayerTo(target.position);
    }

    // Invulnerability input (F5)
    private void CheckInvulnerabilityInput()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            // Ensure we have a reference to the active player
            if (currentPlayer == null)
                currentPlayer = GameObject.FindGameObjectWithTag("Player");

            if (currentPlayer == null)
            {
                Debug.LogWarning("GameManager: No active Player found to toggle invulnerability.");
                return;
            }

            var ps = currentPlayer.GetComponent<PlayerStats>();
            if (ps == null)
            {
                Debug.LogWarning("GameManager: PlayerStats component not found on current player.");
                return;
            }

            ps.ToggleInvulnerability();
            Debug.Log($"GameManager: Toggled invulnerability on player '{currentPlayer.name}' -> {ps.IsInvulnerable()}");
        }
    }
}
