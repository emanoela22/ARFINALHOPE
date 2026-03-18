using UnityEngine;

public class ButterflyTargetController : MonoBehaviour
{
    public enum TrainingSide
    {
        Left,
        Right
    }

    [Header("References")]
    [SerializeField] private Transform butterflyVisual;

    [Header("Training Settings")]
    [SerializeField] private TrainingSide trainingSide = TrainingSide.Left;

    [Header("Target Positions")]
    [SerializeField] private float nearOffset = 0.08f;
    [SerializeField] private float midOffset = 0.16f;
    [SerializeField] private float farOffset = 0.24f;
    [SerializeField] private float verticalOffset = 0f;

    [Header("Optional")]
    [SerializeField] private bool includeCenterPosition = true;

    private Vector3[] availablePositions;

    private void Start()
    {
        BuildPositions();
        MoveToNextTarget();
    }

    private void BuildPositions()
    {
        float sideMultiplier = trainingSide == TrainingSide.Left ? -1f : 1f;

        if (includeCenterPosition)
        {
            availablePositions = new Vector3[]
            {
                new Vector3(0f, verticalOffset, 0f),
                new Vector3(sideMultiplier * nearOffset, verticalOffset, 0f),
                new Vector3(sideMultiplier * midOffset, verticalOffset, 0f),
                new Vector3(sideMultiplier * farOffset, verticalOffset, 0f)
            };
        }
        else
        {
            availablePositions = new Vector3[]
            {
                new Vector3(sideMultiplier * nearOffset, verticalOffset, 0f),
                new Vector3(sideMultiplier * midOffset, verticalOffset, 0f),
                new Vector3(sideMultiplier * farOffset, verticalOffset, 0f)
            };
        }
    }

    public void MoveToNextTarget()
    {
        if (butterflyVisual == null || availablePositions == null || availablePositions.Length == 0)
            return;

        int randomIndex = Random.Range(0, availablePositions.Length);
        butterflyVisual.localPosition = availablePositions[randomIndex];
    }

    public void SetTrainingSide(int sideIndex)
    {
        trainingSide = (TrainingSide)sideIndex;
        BuildPositions();
        MoveToNextTarget();
    }

    public Vector3 GetButterflyWorldPosition()
    {
        if (butterflyVisual == null)
            return Vector3.zero;

        return butterflyVisual.position;
    }

    public Transform GetButterflyVisual()
    {
        return butterflyVisual;
    }
}