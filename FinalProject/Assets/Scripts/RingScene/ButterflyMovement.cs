using UnityEngine;

public class ButterflyMovement : MonoBehaviour
{
    public enum MovementDirection
    {
        Left,
        Right,
        Both,
        Center
    }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 0.2f;
    [SerializeField] private float moveRangeX = 0.15f;
    [SerializeField] private float moveRangeY = 0.1f;
    [SerializeField] private float targetReachDistance = 0.02f;
    [SerializeField] private float waitTimeAtPoint = 0.4f;

    [Header("Direction Control")]
    [SerializeField] private MovementDirection movementDirection = MovementDirection.Both;

    private Vector3 centerPosition;
    private Vector3 targetPosition;
    private float waitTimer;

    private void Start()
    {
        centerPosition = transform.localPosition;
        PickNewTargetPosition();
    }

    private void Update()
    {
        MoveToTarget();
    }

    private void MoveToTarget()
    {
        if (Vector3.Distance(transform.localPosition, targetPosition) > targetReachDistance)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
        }
        else
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTimeAtPoint)
            {
                waitTimer = 0f;
                PickNewTargetPosition();
            }
        }
    }

    private void PickNewTargetPosition()
    {
        float randomX = 0f;
        float randomY = Random.Range(-moveRangeY, moveRangeY);

        switch (movementDirection)
        {
            case MovementDirection.Left:
                randomX = Random.Range(-moveRangeX, 0f);
                break;

            case MovementDirection.Right:
                randomX = Random.Range(0f, moveRangeX);
                break;

            case MovementDirection.Both:
                randomX = Random.Range(-moveRangeX, moveRangeX);
                break;

            case MovementDirection.Center:
                randomX = Random.Range(-moveRangeX * 0.25f, moveRangeX * 0.25f);
                randomY = Random.Range(-moveRangeY * 0.25f, moveRangeY * 0.25f);
                break;
        }

        targetPosition = centerPosition + new Vector3(randomX, randomY, 0f);
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    public void SetMoveRange(float newRange)
    {
        moveRangeX = newRange;
        moveRangeY = newRange * 0.6f;
    }

    public void SetDirection(int directionIndex)
    {
        movementDirection = (MovementDirection)directionIndex;
        PickNewTargetPosition();
    }

    public void ResetButterflyPosition()
    {
        transform.localPosition = centerPosition;
        waitTimer = 0f;
        PickNewTargetPosition();
    }
}