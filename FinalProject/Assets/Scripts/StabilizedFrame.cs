using UnityEngine;

public class StabilizedFrame : MonoBehaviour
{
    [Header("Source Camera (recommended)")]
    [SerializeField] private Camera sourceCamera;

    [Header("Smoothing")]
    [SerializeField] private float posSmoothTime = 0.08f;  // 0.06–0.15
    [SerializeField] private float rotSpeedDeg = 420f;     // deg/sec

    [Header("Anti-teleport clamp")]
    [SerializeField] private float maxPosStepMeters = 0.04f; // 4cm/frame

    private Vector3 posVel;

    /// Call this from your manager (best)
    public void SetSourceCamera(Camera cam)
    {
        sourceCamera = cam;
    }

    private void Awake()
    {
        if (sourceCamera == null) sourceCamera = Camera.main;
        SnapNow();
    }

    public void SnapNow()
    {
        var camT = GetSafeCamTransform();
        if (camT == null) return;

        transform.position = camT.position;
        transform.rotation = camT.rotation;
        posVel = Vector3.zero;
    }

    private void LateUpdate()
    {
        var camT = GetSafeCamTransform();
        if (camT == null) return;

        // SmoothDamp position
        Vector3 targetPos = camT.position;
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetPos, ref posVel, posSmoothTime);

        // Clamp big steps
        Vector3 step = smoothed - transform.position;
        float d = step.magnitude;
        if (d > maxPosStepMeters)
            smoothed = transform.position + step.normalized * maxPosStepMeters;

        transform.position = smoothed;

        // Rate-limited rotation
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            camT.rotation,
            rotSpeedDeg * Time.deltaTime
        );
    }

    private Transform GetSafeCamTransform()
    {
        // Reacquire if AR recreated camera
        if (sourceCamera == null) sourceCamera = Camera.main;
        if (sourceCamera == null) return null;

        // This is the key: we always read current camera.transform (not a cached Transform)
        return sourceCamera.transform;
    }
}