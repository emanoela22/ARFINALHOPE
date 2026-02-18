using System;
using UnityEngine;

public class ScanningTherapyManager : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera arCamera;

    [Header("Target Prefab")]
    [SerializeField] private GameObject targetPrefab;

    [Header("Target Size")]
    [SerializeField] private float targetScaleMeters = 0.08f;

    [Header("Spawn Timing")]
    [SerializeField] private float timeBetweenSpawns = 0.9f;
    [SerializeField] private float targetLifetimeSeconds = 2.5f;

    [Header("Motion")]
    [SerializeField] private float targetSpeed = 0.35f;               // m/s along the left-right path
    [SerializeField] private float goodSideOffsetMeters = 0.35f;      // start offset magnitude
    [SerializeField] private float neglectedSideOffsetMeters = 0.55f; // end offset magnitude

    [Header("Depth / Placement (Camera-Stable)")]
    [SerializeField] private float stableDistanceMeters = 1.2f;
    [SerializeField] private float stableHeightOffsetMeters = -0.05f;

    [Header("Session")]
    [SerializeField] private float sessionDurationSeconds = 60f;

    [Header("Neglect Side")]
    [Tooltip("If neglected side is LEFT, target moves from RIGHT to LEFT.")]
    [SerializeField] private bool neglectedSideIsLeft = true;

    [Header("Anti-cheat Guard")]
    [SerializeField] private PhoneCenteringGuard centeringGuard;

    [Tooltip("If true, we PAUSE spawns unless phone is centered.")]
    [SerializeField] private bool blockSpawnsWhenOffCenter = true;

    [Tooltip("If true, a HIT only counts if the phone has scanned into neglected side.")]
    [SerializeField] private bool requireScanForScore = true;

    [Header("Prompt (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pleaseCenterClip;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // Runtime
    private bool running;
    private float sessionEndTime;
    private float lastSpawnTime;

    private int score;
    private GameObject currentTarget;

    // UI hooks (optional)
    public event Action<int> OnScoreChanged;
    public event Action<float> OnTimeLeftChanged;
    public event Action<string> OnStatusChanged;
    public event Action OnSessionEnded;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
    }

    private void Update()
    {
        if (!running) return;

        float timeLeft = Mathf.Max(0f, sessionEndTime - Time.time);
        OnTimeLeftChanged?.Invoke(timeLeft);

        if (timeLeft <= 0f)
        {
            EndSession();
            return;
        }

        // Anti-cheat centering
        if (centeringGuard != null && centeringGuard.HasCalibration)
        {
            if (blockSpawnsWhenOffCenter && !centeringGuard.IsCenteredStable)
            {
                if (centeringGuard.ShouldWarnOffCenter())
                {
                    OnStatusChanged?.Invoke("Please center the phone.");
                    if (audioSource != null && pleaseCenterClip != null && !audioSource.isPlaying)
                        audioSource.PlayOneShot(pleaseCenterClip);
                }
                return; // pause spawning
            }
        }

        if (currentTarget == null && Time.time - lastSpawnTime >= timeBetweenSpawns)
        {
            SpawnCameraStableTarget();
        }
    }

    // Hook this to your UI button
    public void StartSession()
    {
        if (arCamera == null) arCamera = Camera.main;

        if (arCamera == null || targetPrefab == null)
        {
            Debug.LogError("[ScanningTherapy] Missing Camera or Target Prefab.");
            return;
        }

        score = 0;
        OnScoreChanged?.Invoke(score);

        running = true;
        sessionEndTime = Time.time + sessionDurationSeconds;
        lastSpawnTime = Time.time - timeBetweenSpawns;

        // Calibrate midline at start (or call CalibrateNow from a separate button if you want)
        if (centeringGuard != null && !centeringGuard.HasCalibration)
            centeringGuard.CalibrateNow();

        OnStatusChanged?.Invoke("Session started. Tap targets.");

        if (debugLogs) Debug.Log("[ScanningTherapy] Session started");
    }

    // Hook this to UI button if you want a dedicated calibrate
    public void CalibrateMidlineNow()
    {
        if (centeringGuard != null)
        {
            centeringGuard.CalibrateNow();
            OnStatusChanged?.Invoke("Calibrated. Keep phone centered.");
        }
    }

    public void EndSession()
    {
        running = false;

        if (currentTarget != null) Destroy(currentTarget);
        currentTarget = null;

        OnStatusChanged?.Invoke("Session ended.");
        OnSessionEnded?.Invoke();

        if (debugLogs) Debug.Log("[ScanningTherapy] Session ended");
    }

    private void SpawnCameraStableTarget()
    {
        float startSign = neglectedSideIsLeft ? +1f : -1f; // good side
        float endSign = neglectedSideIsLeft ? -1f : +1f; // neglected side

        float startX = startSign * Mathf.Abs(goodSideOffsetMeters);
        float endX = endSign * Mathf.Abs(neglectedSideOffsetMeters);

        currentTarget = Instantiate(targetPrefab, Vector3.zero, Quaternion.identity);
        currentTarget.transform.localScale = Vector3.one * targetScaleMeters;

        var tb = currentTarget.GetComponent<TargetBehaviour>();
        if (tb == null) tb = currentTarget.AddComponent<TargetBehaviour>();

        tb.InitCameraRelative(
            this,
            arCamera,
            stableDistanceMeters,
            startX,
            endX,
            stableHeightOffsetMeters,
            targetSpeed,
            targetLifetimeSeconds
        );

        lastSpawnTime = Time.time;

        if (debugLogs) Debug.Log("[ScanningTherapy] Spawned target");
    }

    // Called by TargetBehaviour
    public void ReportHit(float reactionTime)
    {
        bool counts = true;

        if (requireScanForScore && centeringGuard != null && centeringGuard.HasCalibration)
        {
            counts = centeringGuard.HasScannedIntoNeglected(neglectedSideIsLeft);
        }

        if (counts)
        {
            score += 1;
            OnScoreChanged?.Invoke(score);
            OnStatusChanged?.Invoke($"Hit! RT: {reactionTime:0.00}s");
        }
        else
        {
            OnStatusChanged?.Invoke("Nice try  Scan toward neglected side!");
            // Optional: don’t reward, but also don’t punish
        }

        currentTarget = null;
    }

    public void ReportMiss()
    {
        OnStatusChanged?.Invoke("Miss (timeout)");
        currentTarget = null;
    }
}
