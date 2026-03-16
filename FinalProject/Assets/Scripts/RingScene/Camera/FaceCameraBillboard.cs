using UnityEngine;

public class FaceCameraBillboard : MonoBehaviour
{
    private Transform cam;

    private void Start()
    {
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
            return;

        transform.LookAt(cam);
    }
}