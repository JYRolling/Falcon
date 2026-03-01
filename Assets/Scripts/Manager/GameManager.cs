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
        // Optionally persist across scenes:
        // DontDestroyOnLoad(gameObject);
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
                if (CVC != null)
                    CVC.m_Follow = playerTemp.transform;
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
}
