using UnityEngine;

public class FollowCameraAnchor : MonoBehaviour
{
    private Transform cam;

    [SerializeField] private float distance = 1.0f;
    [SerializeField] private float heightOffset = 0f;
    [SerializeField] private float followSpeed = 3f;

    public void Initialize(Transform cameraTransform)
    {
        cam = cameraTransform;
        SnapInFront();
    }

    private void LateUpdate()
    {
        if (cam == null)
            return;

        Vector3 targetPos = cam.position + cam.forward * distance;
        targetPos.y += heightOffset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        transform.forward = cam.forward;
    }

    public void SnapInFront()
    {
        if (cam == null)
            return;

        Vector3 targetPos = cam.position + cam.forward * distance;
        targetPos.y += heightOffset;

        transform.position = targetPos;
        transform.forward = cam.forward;
    }
}