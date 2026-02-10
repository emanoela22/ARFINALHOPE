using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ScanningTherapyManager : MonoBehaviour
{
    [Header("AR")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;

    [Header("Target Prefab")]
    [SerializeField] private GameObject targetPrefab;

    [Header("Target Size")]
    [Tooltip("Real-world-ish size in meters. 0.08 = 8cm.")]
    [SerializeField] private float targetScaleMeters = 0.08f;

    [Header("Spawn Behaviour")]
    [SerializeField] private float heightLiftMeters = 0.05f;      // lift above plane so it doesn't clip
    [SerializeField] private float timeBetweenSpawns = 0.75f;

    [Header("Session")]
    [SerializeField] private float sessionDurationSeconds = 60f;

    [Header("Difficulty (adaptive)")]
    [SerializeField] private float targetSpeed = 0.35f;           // meters per second-ish feel
    [SerializeField] private float horizontalStartOffset = 0.35f; // good side offset (meters)
    [SerializeField] private float horizontalEndOffset = -0.55f;  // neglected side offset (meters)
    [SerializeField] private float allowedReactionTime = 2.5f;    // seconds

    [Header("Neglect Side")]
    [Tooltip("If neglected side is LEFT, target should move from RIGHT to LEFT.")]
    [SerializeField] private bool neglectedSideIsLeft = true;

    [Header("Centering Guard")]
    [SerializeField] private PhoneCenteringGuard centeringGuard;

    [Header("Logging (optional)")]
    [SerializeField] private SessionLogger logger;

    private bool running;
    private float sessionEndTime;
    private float lastSpawnTime;

    private int score;
    private GameObject currentTarget;

    public event Action<int> OnScoreChanged;
    public event Action<float> OnTimeLeftChanged;
    public event Action OnSessionEnded;

    private static readonly List<ARRaycastHit> hits = new();

    private void Reset()
    {
        raycastManager = FindFirstObjectByType<ARRaycastManager>();
        planeManager = FindFirstObjectByType<ARPlaneManager>();
        arCamera = Camera.main;
        centeringGuard = FindFirstObjectByType<PhoneCenteringGuard>();
        logger = FindFirstObjectByType<SessionLogger>();
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

        // Enforce "phone centered" after calibration
        if (centeringGuard != null && centeringGuard.HasCalibration && !centeringGuard.IsCenteredStable)
        {
            // Pause spawning while not centered
            return;
        }

        // Spawn if none active
        if (currentTarget == null && Time.time - lastSpawnTime >= timeBetweenSpawns)
        {
            TrySpawnOnPlane();
        }
    }

    public void StartSession()
    {
        score = 0;
        OnScoreChanged?.Invoke(score);

        running = true;
        sessionEndTime = Time.time + sessionDurationSeconds;
        lastSpawnTime = Time.time - timeBetweenSpawns;

        // Recommended: only detect horizontal planes for rehab (floor/table), avoids ceiling/wall weirdness.
        if (planeManager != null)
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;

        centeringGuard?.CalibrateNow();
        logger?.BeginSession(neglectedSideIsLeft ? "LEFT" : "RIGHT");

        Debug.Log("[ScanningTherapy] Session started");
    }

    public void EndSession()
    {
        running = false;

        if (currentTarget != null)
            Destroy(currentTarget);

        currentTarget = null;

        logger?.EndSession(score);
        OnSessionEnded?.Invoke();

        Debug.Log("[ScanningTherapy] Session ended");
    }

    private void TrySpawnOnPlane()
    {
        if (raycastManager == null || arCamera == null || targetPrefab == null)
        {
            Debug.LogError("[ScanningTherapy] Missing references on ScanningTherapyManager!");
            return;
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // Most robust for plane placement
        bool gotHit = raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon);

        if (!gotHit)
        {
            // Optional fallback: spawn 1m in front of camera if no plane yet (for demo reliability)
            // Comment this out if you want "planes required" only.
            SpawnFallbackInFrontOfCamera();
            return;
        }

        Pose hitPose = hits[0].pose;

        // Compute offsets relative to camera's left/right (flattened on world up)
        Transform camT = arCamera.transform;
        Vector3 rightFlat = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;

        // If neglected side is LEFT: start on RIGHT (+), end on LEFT (-)
        float startX = neglectedSideIsLeft ? +Mathf.Abs(horizontalStartOffset) : -Mathf.Abs(horizontalStartOffset);
        float endX = neglectedSideIsLeft ? -Mathf.Abs(horizontalEndOffset) : +Mathf.Abs(horizontalEndOffset);

        // IMPORTANT FIX: spawn around the plane hit position (not plane + forward distance), and lift slightly.
        Vector3 basePos = hitPose.position + Vector3.up * heightLiftMeters;
        Vector3 startPos = basePos + rightFlat * startX;
        Vector3 endPos = basePos + rightFlat * endX;

        SpawnTarget(startPos, endPos);
    }

    private void SpawnFallbackInFrontOfCamera()
    {
        Transform camT = arCamera.transform;
        Vector3 rightFlat = Vector3.ProjectOnPlane(camT.right, Vector3.up).normalized;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;

        Vector3 basePos = camT.position + forwardFlat * 1.0f + Vector3.up * 0.0f;

        float startX = neglectedSideIsLeft ? +Mathf.Abs(horizontalStartOffset) : -Mathf.Abs(horizontalStartOffset);
        float endX = neglectedSideIsLeft ? -Mathf.Abs(horizontalEndOffset) : +Mathf.Abs(horizontalEndOffset);

        Vector3 startPos = basePos + rightFlat * startX;
        Vector3 endPos = basePos + rightFlat * endX;

        SpawnTarget(startPos, endPos);
    }

    private void SpawnTarget(Vector3 startPos, Vector3 endPos)
    {
        currentTarget = Instantiate(targetPrefab, startPos, Quaternion.identity);

        // Mega-blob fix: force sane scale in AR meters
        currentTarget.transform.localScale = Vector3.one * targetScaleMeters;

        TargetBehaviour tb = currentTarget.GetComponent<TargetBehaviour>();
        if (tb == null) tb = currentTarget.AddComponent<TargetBehaviour>();

        tb.Init(this, startPos, endPos, targetSpeed, allowedReactionTime, arCamera);

        lastSpawnTime = Time.time;

        logger?.LogSpawn(Time.time, startPos, endPos, targetSpeed, allowedReactionTime);
    }

    // Called by TargetBehaviour
    public void ReportHit(float reactionTime)
    {
        score += 1;
        OnScoreChanged?.Invoke(score);

        logger?.LogHit(Time.time, reactionTime, success: true);
        AdaptDifficulty(success: true, reactionTime);

        currentTarget = null;
    }

    public void ReportMiss()
    {
        logger?.LogHit(Time.time, allowedReactionTime, success: false);
        AdaptDifficulty(success: false, allowedReactionTime);

        currentTarget = null;
    }

    private void AdaptDifficulty(bool success, float reactionTime)
    {
        if (success)
        {
            if (reactionTime < allowedReactionTime * 0.55f)
            {
                targetSpeed = Mathf.Min(1.2f, targetSpeed + 0.05f);
                // Push slightly deeper into neglected side
                float delta = 0.05f;
                horizontalEndOffset = neglectedSideIsLeft
                    ? Mathf.Clamp(horizontalEndOffset - delta, -1.2f, -0.1f)
                    : Mathf.Clamp(horizontalEndOffset + delta, 0.1f, 1.2f);
            }

            allowedReactionTime = Mathf.Clamp(allowedReactionTime - 0.05f, 1.2f, 4.0f);
        }
        else
        {
            targetSpeed = Mathf.Max(0.2f, targetSpeed - 0.05f);
            allowedReactionTime = Mathf.Clamp(allowedReactionTime + 0.15f, 1.2f, 4.0f);

            // Make it easier: pull end offset back toward center
            horizontalEndOffset = Mathf.Lerp(horizontalEndOffset, 0f, 0.25f);
        }

        logger?.LogDifficulty(Time.time, targetSpeed, horizontalEndOffset, allowedReactionTime);
    }
}
