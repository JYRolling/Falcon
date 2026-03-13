using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private CharacterController2D controller;
    [SerializeField] private float jumpBufferTime = 0.12f;

    public float runSpeed = 40f;

    private float horizontalMove = 0f;
    private float jumpBufferCounter = 0f;

    private void Awake()
    {
        // Auto-find controller on the same GameObject if not assigned in Inspector.
        if (controller == null)
        {
            controller = GetComponent<CharacterController2D>();
        }
    }

    private void Update()
    {
        horizontalMove = Input.GetAxisRaw("Horizontal") * runSpeed;

        // Support both Input Manager mapping and direct Space key as a fallback.
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        if (controller == null)
        {
            Debug.LogError("CharacterController2D is missing on PlayerMovement.");
            return;
        }

        bool jumpThisFrame = jumpBufferCounter > 0f;
        controller.Move(horizontalMove * Time.fixedDeltaTime, false, jumpThisFrame);

        if (jumpThisFrame)
        {
            jumpBufferCounter = 0f;
        }
    }
}