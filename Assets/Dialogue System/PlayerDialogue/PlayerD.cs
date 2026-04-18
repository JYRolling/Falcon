using UnityEngine;

public class PlayerD : MonoBehaviour
{
    [SerializeField] private DialogueUI dialogueUI;

    public DialogueUI DialogueUI => dialogueUI;

    public IInteractable Interactable { get; set; }
    private void Update()
    {
        if (dialogueUI.IsOpen) return;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
       
        if (Input.GetKeyDown(KeyCode.E))
        {
            Interactable?.Interact(this);
        }
    }
}
