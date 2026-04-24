using UnityEngine;

public class DialogueActivator : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueObject dialogueObject;
    [Tooltip("If true the dialogue will automatically start when the player is respawned inside this trigger.")]
    [SerializeField] private bool autoStartOnRespawn = false;

    // track whether a player is currently inside this trigger
    private bool playerInside;
    private PlayerD playerInTrigger;

    private void OnEnable()
    {
        SubscribeToGameManager();
    }

    private void Start()
    {
        // ensure subscription if GameManager wasn't ready in OnEnable
        SubscribeToGameManager();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.PlayerRespawned -= OnPlayerRespawned;
    }

    private void SubscribeToGameManager()
    {
        if (GameManager.Instance != null)
        {
            // avoid double-subscribe
            GameManager.Instance.PlayerRespawned -= OnPlayerRespawned;
            GameManager.Instance.PlayerRespawned += OnPlayerRespawned;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerD player))
        {
            player.Interactable = this;
            playerInside = true;
            playerInTrigger = player;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerD player))
        {
            if (player.Interactable is DialogueActivator dialogueActivator && dialogueActivator == this)
            {
                player.Interactable = null;
            }

            // clear tracking when the player exits
            playerInside = false;
            if (playerInTrigger == player)
                playerInTrigger = null;
        }
    }

    public void Interact(PlayerD player)
    {
        player.DialogueUI.ShowDialogue(dialogueObject);
    }

    // Called when GameManager respawns a player instance
    private void OnPlayerRespawned(GameObject playerObj)
    {
        if (playerObj == null)
            return;

        // If the respawned player is inside this trigger (or a player still tracked here), set interactable / start dialogue
        if (playerInside)
        {
            if (playerObj.TryGetComponent(out PlayerD pd))
            {
                // ensure the new player knows this is an available interactable
                pd.Interactable = this;

                if (autoStartOnRespawn)
                {
                    // start the dialogue immediately for the respawned player
                    pd.DialogueUI.ShowDialogue(dialogueObject);
                }
            }
        }
    }
}
