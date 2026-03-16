using UnityEngine;

public class SpawnTest : MonoBehaviour
{
    [SerializeField] private GameObject testPrefab;

    private void Start()
    {
        if (testPrefab == null)
        {
            Debug.LogError("Test Prefab is not assigned.");
            return;
        }

        Vector3 spawnPosition = new Vector3(0f, 0f, 1f);
        Instantiate(testPrefab, spawnPosition, Quaternion.identity);

        Debug.Log("Spawned test prefab at (0,0,1)");
    }
}