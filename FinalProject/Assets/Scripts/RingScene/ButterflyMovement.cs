using UnityEngine;

public class ButterflyMovement : MonoBehaviour
{
    public enum Direction
    {
        Left,
        Right
    }

    [SerializeField] private Direction moveDirection = Direction.Left;
    [SerializeField] private float speed = 0.1f;
    [SerializeField] private float distance = 0.15f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool goingOut = true;

    private void Start()
    {
        startPos = transform.localPosition;
        ResetPath();
    }

    private void Update()
    {
        Vector3 target = goingOut ? targetPos : startPos;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.localPosition, target) < 0.005f)
        {
            goingOut = !goingOut;

            if (goingOut)
                ResetPath();
        }
    }

    private void ResetPath()
    {
        float x = moveDirection == Direction.Left ? -distance : distance;

        targetPos = new Vector3(
            startPos.x + x,
            startPos.y,
            startPos.z
        );
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public void ResetButterflyPosition()
    {
        transform.localPosition = startPos;
        ResetPath();
    }
}