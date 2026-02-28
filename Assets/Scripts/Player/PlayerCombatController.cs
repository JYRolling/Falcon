using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerCombatController
// Stripped of all melee input / hitbox / animation logic.
// Now provides simple damage calculation helpers and still receives damage via `Damage(float[])`.
public class PlayerCombatController : MonoBehaviour
{
    [Header("Damage configuration (used by other systems)")]
    [Tooltip("Base damage value used when constructing attack details")]
    [SerializeField]
    private float attack1Damage = 1f;

    // Player receive-damage dependencies
    private PlayerController PC;
    private PlayerStats PS;

    private void Start()
    {
        PC = GetComponent<PlayerController>();
        PS = GetComponent<PlayerStats>();
    }

    // Public API - build the float[] attackDetails used across the project:
    // [0] = damage amount, [1] = attacker X position (used to determine knockback direction)
    public float[] CreateAttackDetails()
    {
        float[] details = new float[2];
        details[0] = attack1Damage;
        details[1] = transform.position.x;
        return details;
    }

    // Convenience accessors
    public float GetAttackDamage() => attack1Damage;
    public void SetAttackDamage(float value) => attack1Damage = value;

    // Receive damage handler (kept so other systems can SendMessage("Damage", float[]))
    // Maintains original behavior: decrease health, apply knockback unless dashing.
    private void Damage(float[] attackDetails)
    {
        if (PC == null || PS == null)
        {
            // fallback safety: try to acquire components
            PC = GetComponent<PlayerController>();
            PS = GetComponent<PlayerStats>();
            if (PC == null || PS == null) return;
        }

        if (!PC.GetDashStatus())
        {
            int direction;

            PS.DecreaseHealth(attackDetails[0]);

            if (attackDetails[1] < transform.position.x)
            {
                direction = 1;
            }
            else
            {
                direction = -1;
            }

            PC.Knockback(direction);
        }
    }
}
