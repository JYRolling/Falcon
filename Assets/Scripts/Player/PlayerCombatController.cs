using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [SerializeField]
    private bool combatEnabled;
    [SerializeField]
    private float inputTimer, attack1Radius, attack1Damage;
    [SerializeField]
    private Transform attack1HitBoxPos;
    [SerializeField]
    private LayerMask whatIsDamageable;
    [SerializeField]
    private float attack1Duration = 0.4f; // Duration used when no Animator is present

    private bool gotInput, isAttacking, isFirstAttack;

    private float lastInputTime = Mathf.NegativeInfinity;

    private float[] attackDetails = new float[2];

    private Animator anim;

    private PlayerController PC;
    private PlayerStats PS;

    private void Start()
    {
        anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("canAttack", combatEnabled);
        }
        PC = GetComponent<PlayerController>();
        PS = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        CheckCombatInput();
        CheckAttacks();
    }

    private void CheckCombatInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (combatEnabled)
            {
                //Attempt combat
                gotInput = true;
                lastInputTime = Time.time;
            }
        }
    }

    private void CheckAttacks()
    {
        if (gotInput)
        {
            //Perform Attack1
            if (!isAttacking)
            {
                gotInput = false;
                isAttacking = true;
                isFirstAttack = !isFirstAttack;

                if (anim != null)
                {
                    anim.SetBool("attack1", true);
                    anim.SetBool("firstAttack", isFirstAttack);
                    anim.SetBool("isAttacking", isAttacking);
                }
                else
                {
                    // No Animator: handle attack flow in code
                    StartCoroutine(PerformAttackWithoutAnimator());
                }
            }
        }

        if (Time.time >= lastInputTime + inputTimer)
        {
            //Wait for new input
            gotInput = false;
        }
    }

    private IEnumerator PerformAttackWithoutAnimator()
    {
        // Optional: simulate a small wind-up before the hitbox is active.
        // Here we wait half the attack duration, then trigger the hit, then finish.
        float hitTime = attack1Duration * 0.5f;
        yield return new WaitForSeconds(hitTime);

        CheckAttackHitBox();

        yield return new WaitForSeconds(attack1Duration - hitTime);

        FinishAttack1();
    }

    private void CheckAttackHitBox()
    {
        Collider2D[] detectedObjects = Physics2D.OverlapCircleAll(attack1HitBoxPos.position, attack1Radius, whatIsDamageable);

        attackDetails[0] = attack1Damage;
        attackDetails[1] = transform.position.x;

        foreach (Collider2D collider in detectedObjects)
        {
            // Send damage to parent (keeps existing project convention)
            collider.transform.parent.SendMessage("Damage", attackDetails);
            //Instantiate hit particle (kept as comment because original code left it commented)
        }
    }

    private void FinishAttack1()
    {
        isAttacking = false;
        if (anim != null)
        {
            anim.SetBool("isAttacking", isAttacking);
            anim.SetBool("attack1", false);
        }
    }

    private void Damage(float[] attackDetails)
    {
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

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(attack1HitBoxPos.position, attack1Radius);
    }

}
