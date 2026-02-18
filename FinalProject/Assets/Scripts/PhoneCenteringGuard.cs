using UnityEngine;

public class PhoneCenteringGuard : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera arCamera;

    [Header("Centering Rules")]
    [SerializeField] private float maxYawDegFromCenter = 20f;
    [SerializeField] private float stableTimeRequired = 0.6f;
    [SerializeField] private float badHoldTimeToWarn = 1.0f;

    [Header("Scan Rules")]
    [SerializeField] private float requiredScanIntoNeglectedDeg = 12f;

    public bool HasCalibration { get; private set; }
    public bool IsCenteredStable { get; private set; }
    public float YawFromCenterDeg { get; private set; }

    private Vector3 calibratedForwardFlat;
    private float centeredTimer;
    private float badTimer;

    public void CalibrateNow()
    {
        if (arCamera == null) arCamera = Camera.main;

        Vector3 f = arCamera.transform.forward;
        f.y = 0f;
        calibratedForwardFlat = (f.sqrMagnitude < 0.0001f) ? Vector3.forward : f.normalized;

        HasCalibration = true;
        centeredTimer = 0f;
        badTimer = 0f;
        IsCenteredStable = false;
    }

    private void Update()
    {
        if (!HasCalibration)
        {
            IsCenteredStable = false;
            return;
        }

        if (arCamera == null) arCamera = Camera.main;
        if (arCamera == null) return;

        Vector3 f = arCamera.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) return;
        f.Normalize();

        YawFromCenterDeg = Vector3.SignedAngle(calibratedForwardFlat, f, Vector3.up);

        float absYaw = Mathf.Abs(YawFromCenterDeg);
        bool centered = absYaw <= maxYawDegFromCenter;

        if (centered)
        {
            badTimer = 0f;
            centeredTimer += Time.deltaTime;
            IsCenteredStable = centeredTimer >= stableTimeRequired;
        }
        else
        {
            centeredTimer = 0f;
            IsCenteredStable = false;
            badTimer += Time.deltaTime;
        }
    }

    public bool ShouldWarnOffCenter()
        => HasCalibration && badTimer >= badHoldTimeToWarn;

    public bool HasScannedIntoNeglected(bool neglectedIsLeft)
    {
        if (!HasCalibration) return true;

        return neglectedIsLeft
            ? (YawFromCenterDeg <= -requiredScanIntoNeglectedDeg)
            : (YawFromCenterDeg >= requiredScanIntoNeglectedDeg);
    }
}
