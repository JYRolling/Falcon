using UnityEngine;

// Attach this to a GameObject with a Collider2D (Is Trigger = true).
// Assign an optional spawnPoint transform (an empty GameObject marking the exact respawn pose).
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Optional: empty GameObject marking the exact respawn transform. If null, this GameObject's transform is used.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Tag to identify the player collider that activates this checkpoint")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("If true the checkpoint only activates once")]
    [SerializeField] private bool activateOnce = true;

    private bool _activated = false;

    private void Reset()
    {
        // try to ensure the collider is a trigger to avoid common setup mistakes
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_activated && activateOnce) return;
        if (other == null) return;

        if (other.CompareTag(playerTag))
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("Checkpoint: GameManager.Instance not found. Ensure GameManager exists in the scene.");
                return;
            }

            Transform target = spawnPoint != null ? spawnPoint : transform;
            gm.SetRespawnPoint(target);

            // Optional: visual feedback (e.g. change sprite, play SFX) can go here

            _activated = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        var p = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.DrawWireSphere(p, 0.25f);
    }
}