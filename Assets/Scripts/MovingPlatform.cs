using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField] float moveSpeed = 1f;
    [SerializeField] float distanceToStop = 5f;

    [SerializeField] bool isMovingUp = true;

    Vector3 startPosition;
    Vector3 endPosition;

    void Start()
    {
        startPosition = transform.position;
        endPosition = transform.position + Vector3.up * distanceToStop;
    }

    void Update()
    {
        if (isMovingUp)
        {
            transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, endPosition) < 0.1f)
            {
                isMovingUp = false;
            }
        }
        else
        {
            transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, startPosition) < 0.1f)
            {
                isMovingUp = true;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.parent = transform;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.parent = null;
        }
    }
}