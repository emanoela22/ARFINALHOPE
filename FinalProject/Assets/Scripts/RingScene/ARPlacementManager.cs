using UnityEngine;

public class ARPlacementManager : MonoBehaviour
{
    [SerializeField] private GameObject arContentPrefab;
    [SerializeField] private GameUIController gameUIController;
    [SerializeField] private RingTracker ringTracker;

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

        Vector3 spawnPosition = Camera.main.transform.position + Camera.main.transform.forward * 1.0f;
        Quaternion spawnRotation = Quaternion.LookRotation(Camera.main.transform.forward);

        spawnedObject = Instantiate(arContentPrefab, spawnPosition, spawnRotation);

        ButterflyMovement butterfly = spawnedObject.GetComponentInChildren<ButterflyMovement>();
        FollowCameraAnchor anchor = spawnedObject.GetComponent<FollowCameraAnchor>();

        if (anchor != null)
        {
            anchor.Initialize(Camera.main.transform);
        }

        if (butterfly != null)
        {
            if (gameUIController != null)
                gameUIController.RegisterButterfly(butterfly);

            if (ringTracker != null)
                ringTracker.SetButterfly(butterfly);

            Debug.Log("Butterfly registered successfully.");
        }
        else
        {
            Debug.LogWarning("ButterflyMovement not found in spawned prefab.");
        }
    }
}