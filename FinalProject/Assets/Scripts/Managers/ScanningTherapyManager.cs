using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScanningTherapyManager : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera arCamera;

    [Header("Stabilization Frame (REQUIRED)")]
    [SerializeField] private StabilizedFrame stabilizedFrame;

    [Header("Target Prefab")]
    [SerializeField] private GameObject targetPrefab;

    [Header("Target Size (meters-ish)")]
    [SerializeField] private float targetScaleMeters = 0.15f;

    [Header("Spawn Timing")]
    [SerializeField] private float timeBetweenSpawns = 0.9f;
    [SerializeField] private float targetLifetimeSeconds = 2.5f;

    [Header("Motion")]
    [SerializeField] private float targetSpeed = 0.35f;
    [SerializeField] private float goodSideOffsetMeters = 0.15f;
    [SerializeField] private float neglectedSideOffsetMeters = 0.25f;

    [Header("Placement (local to stabilized frame)")]
    [SerializeField] private float stableDistanceMeters = 1.5f;
    [SerializeField] private float stableHeightOffsetMeters = 0.10f;

    [Header("Session")]
    [SerializeField] private float sessionDurationSeconds = 60f;

    [Header("Neglect Side")]
    [SerializeField] private bool neglectedSideIsLeft = true;

    [Header("Anti-cheat / Boundaries (optional)")]
    [SerializeField] private PhoneCenteringGuard centeringGuard;
    [SerializeField] private bool blockSpawnsWhenOffCenter = true;
    [SerializeField] private bool requireScanForScore = false;

    [Header("Audio prompt (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pleaseCenterClip;
    [SerializeField] private float offCenterVoiceCooldown = 3f;

    [Header("Menu Return")]
    [SerializeField] private bool returnToMenuOnEnd = true;
    [SerializeField] private string menuSceneName = "MenuScene";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool running;
    private bool ending;

    private float sessionEndTime;
    private float lastSpawnTime;

    private int score;
    private GameObject currentTarget;
    private float nextVoiceTime;

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
        if (!running || ending) return;

        float timeLeft = Mathf.Max(0f, sessionEndTime - Time.time);
        OnTimeLeftChanged?.Invoke(timeLeft);

        if (timeLeft <= 0f)
        {
            EndSession();
            return;
        }

        // Off-center warning + (optional) block spawns
        if (centeringGuard != null && centeringGuard.HasCalibration)
        {
            bool offCenter = !centeringGuard.IsCenteredStable;

            if (offCenter && centeringGuard.ShouldWarnOffCenter())
            {
                OnStatusChanged?.Invoke("Please center the phone.");

                if (audioSource != null && pleaseCenterClip != null && Time.time >= nextVoiceTime)
                {
                    audioSource.PlayOneShot(pleaseCenterClip);
                    nextVoiceTime = Time.time + offCenterVoiceCooldown;
                }
            }

            if (blockSpawnsWhenOffCenter && offCenter)
                return;
        }

        if (currentTarget == null && Time.time - lastSpawnTime >= timeBetweenSpawns)
        {
            // Only gate spawns, not the whole session update
            if (blockSpawnsWhenOffCenter && centeringGuard != null && centeringGuard.HasCalibration)
            {
                if (!centeringGuard.IsCenteredStable)
                {
                    OnStatusChanged?.Invoke("Please center the phone.");
                    TryPlayOffCenterAudio();
                    return;
                }
            }

            SpawnStabilizedTarget();
        }
    }

    private void TryPlayOffCenterAudio()
    {
        if (audioSource == null || pleaseCenterClip == null) return;
        if (Time.time < nextVoiceTime) return;

        audioSource.PlayOneShot(pleaseCenterClip);
        nextVoiceTime = Time.time + offCenterVoiceCooldown;
    }

    public void StartSession()
    {
        if (arCamera == null) arCamera = Camera.main;

        if (arCamera == null || targetPrefab == null || stabilizedFrame == null)
        {
            Debug.LogError("[ScanningTherapy] Missing Camera, Target Prefab, or StabilizedFrame.");
            return;
        }

        ending = false;

        stabilizedFrame.SetSourceCamera(arCamera);
        stabilizedFrame.SnapNow();

        if (centeringGuard != null)
            centeringGuard.CalibrateNow();

        nextVoiceTime = 0f;

        score = 0;
        OnScoreChanged?.Invoke(score);

        running = true;
        sessionEndTime = Time.time + sessionDurationSeconds;
        lastSpawnTime = Time.time - timeBetweenSpawns;

        OnStatusChanged?.Invoke("Session started. Tap targets.");

        if (debugLogs) Debug.Log("[ScanningTherapy] Session started");
    }

    private void SpawnStabilizedTarget()
    {
        if (ending) return;
        if (stabilizedFrame == null) return;

        Transform frame = stabilizedFrame.transform;

        // Base in LOCAL space of stabilized frame (clean, consistent)
        Vector3 baseLocal = new Vector3(0f, stableHeightOffsetMeters, stableDistanceMeters);

        float startSign = neglectedSideIsLeft ? +1f : -1f; // good side
        float endSign = neglectedSideIsLeft ? -1f : +1f; // neglected side

        Vector3 startLocal = baseLocal + Vector3.right * (startSign * Mathf.Abs(goodSideOffsetMeters));
        Vector3 endLocal = baseLocal + Vector3.right * (endSign * Mathf.Abs(neglectedSideOffsetMeters));

        // Convert ONCE to world points
        Vector3 startWorld = frame.TransformPoint(startLocal);
        Vector3 endWorld = frame.TransformPoint(endLocal);

        currentTarget = Instantiate(targetPrefab, startWorld, Quaternion.identity);
        currentTarget.transform.localScale = Vector3.one * targetScaleMeters;

        var tb = currentTarget.GetComponent<TargetBehaviour>();
        if (tb == null) tb = currentTarget.AddComponent<TargetBehaviour>();

        tb.InitWorldPath(
            manager: this,
            arCamera: arCamera,
            startWorldPos: startWorld,
            endWorldPos: endWorld,
            speedMetersPerSec: targetSpeed,
            lifeSeconds: targetLifetimeSeconds
        );

        lastSpawnTime = Time.time;

        if (debugLogs) Debug.Log("[ScanningTherapy] Spawned target.");
    }

    public void ReportHit(float reactionTime)
    {
        if (ending) return;

        bool counts = true;

        if (requireScanForScore && centeringGuard != null && centeringGuard.HasCalibration)
            counts = centeringGuard.HasScannedIntoNeglected(neglectedSideIsLeft);

        if (counts)
        {
            score += 1;
            OnScoreChanged?.Invoke(score);
            OnStatusChanged?.Invoke($"Hit! RT: {reactionTime:0.00}s");
        }
        else
        {
            OnStatusChanged?.Invoke("Scan further toward neglected side.");
        }

        currentTarget = null;
    }

    public void ReportMiss()
    {
        if (ending) return;
        OnStatusChanged?.Invoke("Miss (timeout)");
        currentTarget = null;
    }

    public void EndSession()
    {
        if (ending) return;
        ending = true;
        running = false;

        // Stop + destroy target safely
        if (currentTarget != null)
        {
            var tb = currentTarget.GetComponent<TargetBehaviour>();
            if (tb != null) tb.ForceStop();
            Destroy(currentTarget);
            currentTarget = null;
        }

        OnSessionEnded?.Invoke();

        if (returnToMenuOnEnd && !string.IsNullOrEmpty(menuSceneName))
            StartCoroutine(LoadMenuEndOfFrame());
    }

    private IEnumerator LoadMenuEndOfFrame()
    {
        // Wait end-of-frame so Destroy() completes cleanly
        yield return new WaitForEndOfFrame();
        SceneManager.LoadScene(menuSceneName);
    }
}