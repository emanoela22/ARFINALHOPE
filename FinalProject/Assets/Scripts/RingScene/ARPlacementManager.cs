using UnityEngine;

public class ARPlacementManager : MonoBehaviour
{
    [SerializeField] private GameObject arContentPrefab;
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

        FollowCameraAnchor anchor = spawnedObject.GetComponent<FollowCameraAnchor>();
        ButterflyTargetController targetController = spawnedObject.GetComponent<ButterflyTargetController>();

        if (anchor != null)
        {
            anchor.Initialize(Camera.main.transform);
        }

       // if (targetController != null && ringTracker != null)
        //{
        //    ringTracker.SetButterflyController(targetController);
        //}
    }
}