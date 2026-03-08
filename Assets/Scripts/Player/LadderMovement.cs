using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LadderMovement : MonoBehaviour
{
    private float vertical;
    private float horizontal;

    [SerializeField] private float climbSpeed = 8f;
    [SerializeField] private float climbHorizontalSpeed = 4f;
    [SerializeField] [Range(0f, 1f)] private float climbSmoothing = 0.2f;

    private bool isLadder;
    private bool isClimbing;

    [SerializeField] private Rigidbody2D rb;

    private float _originalGravity;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        _originalGravity = rb != null ? rb.gravityScale : 1f;
    }

    // Update is called once per frame
    void Update()
    {
        vertical = Input.GetAxisRaw("Vertical");
        horizontal = Input.GetAxisRaw("Horizontal");

        // Start climbing if on ladder and player provides vertical OR horizontal input
        if (isLadder && (Mathf.Abs(vertical) > 0.1f || Mathf.Abs(horizontal) > 0.1f))
        {
            isClimbing = true;
        }

        // If player leaves input while on ladder, remain clamped (no slip) until they exit or explicitly stop
        if (!isLadder)
        {
            isClimbing = false;
        }
    }

    private void FixedUpdate()
    {
        if (isClimbing)
        {
            // disable gravity while climbing
            rb.gravityScale = 0f;

            // Compute target velocity: allow controlled horizontal movement on ladder with no inertia
            Vector2 targetVel = new Vector2(horizontal * climbHorizontalSpeed, vertical * climbSpeed);

            // Smooth toward target to avoid abrupt changes (small smoothing reduces "slippery" feel)
            rb.velocity = Vector2.Lerp(rb.velocity, targetVel, climbSmoothing);
        }
        else
        {
            // restore original gravity when not climbing
            rb.gravityScale = _originalGravity;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            isLadder = true;
            // Optionally zero horizontal velocity on first contact so player doesn't slide in
            // rb.velocity = new Vector2(0f, rb.velocity.y);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            isLadder = false;
            isClimbing = false;
            // Restore gravity explicitly
            rb.gravityScale = _originalGravity;
        }
    }
}
