using UnityEngine;

public class PhoneCenteringGuard : MonoBehaviour
{
    [Header("Tolerance")]
    [SerializeField] private float yawToleranceDeg = 18f;
    [SerializeField] private float stableTimeToResume = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    private float calibratedYaw;
    private bool hasCalibration;

    private bool isCentered;
    private float centeredSince;

    public bool HasCalibration => hasCalibration;
    public bool IsCenteredStable => isCentered && (Time.time - centeredSince) >= stableTimeToResume;
    public float YawDriftDeg { get; private set; }

    private void Awake()
    {
        if (!SystemInfo.supportsGyroscope)
        {
            Debug.LogWarning("[PhoneCenteringGuard] Gyro not supported.");
            return;
        }
        Input.gyro.enabled = true;
    }

    private void Update()
    {
        if (!hasCalibration) return;

        float currentYaw = GetYawDeg();
        YawDriftDeg = Mathf.DeltaAngle(calibratedYaw, currentYaw);

        bool within = Mathf.Abs(YawDriftDeg) <= yawToleranceDeg;

        if (within)
        {
            if (!isCentered)
            {
                isCentered = true;
                centeredSince = Time.time;
                if (logStateChanges) Debug.Log("[PhoneCenteringGuard] Centered");
            }
        }
        else
        {
            if (isCentered)
            {
                isCentered = false;
                if (logStateChanges) Debug.Log("[PhoneCenteringGuard] Not centered");
            }
        }
    }

    public void CalibrateNow()
    {
        if (!SystemInfo.supportsGyroscope)
        {
            hasCalibration = false;
            return;
        }

        calibratedYaw = GetYawDeg();
        hasCalibration = true;

        isCentered = true;
        centeredSince = Time.time;

        if (logStateChanges) Debug.Log("[PhoneCenteringGuard] Calibrated yaw=" + calibratedYaw.ToString("0.0"));
    }

    private float GetYawDeg()
    {
        Quaternion q = Input.gyro.attitude;
        Quaternion converted = new Quaternion(q.x, q.y, -q.z, -q.w);
        return converted.eulerAngles.y;
    }
}
