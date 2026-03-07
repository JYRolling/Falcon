using UnityEngine;

public class Spike : MonoBehaviour
{
    [SerializeField] private float touchDamage = 10f;
    [SerializeField] private string playerTag = "Player";

    // Reused shape from project: float[0] = damage, float[1] = attackerX (used to compute knockback direction)
    private readonly float[] attackDetails = new float[2];

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        attackDetails[0] = touchDamage;
        attackDetails[1] = transform.position.x;

        // Use SendMessage so it integrates with the existing Damage(float[]) on the player
        other.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);
    }

    // If your spike uses a non-trigger collider, uncomment the method below instead of OnTriggerEnter2D
    /*
    private void OnCollisionEnter2D(Collision2D collision)
    {
        var other = collision.collider;
        if (!other.CompareTag(playerTag)) return;

        attackDetails[0] = touchDamage;
        attackDetails[1] = transform.position.x;
        other.SendMessage("Damage", attackDetails, SendMessageOptions.DontRequireReceiver);
    }
    */
}