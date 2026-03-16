using UnityEngine;

public class ARPlacementManager : MonoBehaviour
{
    [SerializeField] private GameObject arContentPrefab;
    [SerializeField] private GameUIController gameUIController;
    [SerializeField] private float spawnDistance = 1.0f;
    [SerializeField] private float verticalOffset = -0.1f;

    private GameObject spawnedObject;

    private void Start()
    {
        SpawnExercise();
    }

    public void SpawnExercise()
    {
        if (spawnedObject != null)
            return;

        if (Camera.main == null)
        {
            Debug.LogError("Main Camera not found.");
            return;
        }

        Vector3 spawnPosition =
            Camera.main.transform.position +
            Camera.main.transform.forward * spawnDistance +
            Vector3.up * verticalOffset;

        spawnedObject = Instantiate(arContentPrefab, spawnPosition, Quaternion.identity);

        ButterflyMovement butterfly = spawnedObject.GetComponentInChildren<ButterflyMovement>();

        if (butterfly != null && gameUIController != null)
        {
            gameUIController.RegisterButterfly(butterfly);
            Debug.Log("Butterfly registered successfully.");
        }
        else
        {
            Debug.LogWarning("ButterflyMovement or GameUIController missing.");
        }
    }

    public void ResetExercisePosition()
    {
        if (spawnedObject == null || Camera.main == null)
            return;

        Vector3 newPosition =
            Camera.main.transform.position +
            Camera.main.transform.forward * spawnDistance +
            Vector3.up * verticalOffset;

        spawnedObject.transform.position = newPosition;
    }
}