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

    private float respawnTimeStart;

    private bool respawn;

    private CinemachineVirtualCamera CVC;

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
    }

    private void Update()
    {
        CheckRespawn();
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

        var bars = Object.FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var hb in bars)
        {
            if (!IsValidPlayerHealthBar(hb))
                continue;

            healthBar = hb;
            return hb;
        }

        return null;
    }
}
