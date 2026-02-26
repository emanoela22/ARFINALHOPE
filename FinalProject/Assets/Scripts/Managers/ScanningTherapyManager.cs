using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private float targetSpeed = 0.35f;             
    [SerializeField] private float goodSideOffsetMeters = 0.35f;      
    [SerializeField] private float neglectedSideOffsetMeters = 0.55f; 

    [Header("Depth / Placement (Camera-Stable)")]
    [SerializeField] private float stableDistanceMeters = 1.2f;
    [SerializeField] private float stableHeightOffsetMeters = -0.05f;

    [Header("Session")]
    [SerializeField] private float sessionDurationSeconds = 60f;

    [Header("Neglect Side")]
    [Tooltip("If neglected side is LEFT, target moves from RIGHT to LEFT.")]
    [SerializeField] private bool neglectedSideIsLeft = true;

    [Header("Anti-cheat Guard (optional)")]
    [SerializeField] private PhoneCenteringGuard centeringGuard;

    [Tooltip("If true, we PAUSE spawns unless phone is centered.")]
    [SerializeField] private bool blockSpawnsWhenOffCenter = true;

    [Header("Prompt (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pleaseCenterClip;
    [SerializeField] private float offCenterVoiceCooldown = 3.0f;

    [Header("Menu Return")]
    [SerializeField] private string menuSceneName = "MenuScene";

    [Header("Stats + Logging (optional)")]
    [SerializeField] private SessionStats stats;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool running;
    private float sessionEndTime;
    private float lastSpawnTime;
    private float lastOffCenterVoiceTime;

    private int score;
    private GameObject currentTarget;

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
            EndSessionAndReturnToMenu();
            return;
        }

        if (centeringGuard != null && centeringGuard.HasCalibration)
        {
            if (blockSpawnsWhenOffCenter && !centeringGuard.IsCenteredStable)
            {
                OnStatusChanged?.Invoke("Please center the phone.");

                if (audioSource != null && pleaseCenterClip != null)
                {
                    if (Time.time - lastOffCenterVoiceTime >= offCenterVoiceCooldown)
                    {
                        audioSource.PlayOneShot(pleaseCenterClip);
                        lastOffCenterVoiceTime = Time.time;
                    }
                }

                return;
            }
        }

        if (currentTarget == null && Time.time - lastSpawnTime >= timeBetweenSpawns)
        {
            SpawnCameraStableTarget();
        }
    }

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

        stats?.ResetStats();

        running = true;
        sessionEndTime = Time.time + sessionDurationSeconds;
        lastSpawnTime = Time.time - timeBetweenSpawns;

        if (centeringGuard != null && !centeringGuard.HasCalibration)
            centeringGuard.CalibrateNow();

        OnStatusChanged?.Invoke("Session started. Tap targets.");

        if (debugLogs) Debug.Log("[ScanningTherapy] Session started");
    }

    public void CalibrateMidlineNow()
    {
        if (centeringGuard != null)
        {
            centeringGuard.CalibrateNow();
            OnStatusChanged?.Invoke("Calibrated. Keep phone centered.");
        }
    }

    private void SpawnCameraStableTarget()
    {
        float startSign = neglectedSideIsLeft ? +1f : -1f;
        float endSign = neglectedSideIsLeft ? -1f : +1f;

        float startX = startSign * Mathf.Abs(goodSideOffsetMeters);
        float endX = endSign * Mathf.Abs(neglectedSideOffsetMeters);

        currentTarget = Instantiate(targetPrefab, Vector3.zero, Quaternion.identity);
        currentTarget.transform.localScale = Vector3.one * targetScaleMeters;

        var tb = currentTarget.GetComponent<TargetBehaviour>();
        if (tb == null) tb = currentTarget.AddComponent<TargetBehaviour>();

        tb.InitCameraRelative(
            manager: this,
            arCamera: arCamera,
            distanceMeters: stableDistanceMeters,
            startOffsetXMeters: startX,
            endOffsetXMeters: endX,
            heightOffsetMeters: stableHeightOffsetMeters,
            speedMetersPerSec: targetSpeed,
            maxLifeTimeSeconds: targetLifetimeSeconds
        );

        lastSpawnTime = Time.time;

        if (debugLogs) Debug.Log("[ScanningTherapy] Spawned target");
    }

    public void ReportHit(float reactionTime)
    {
        score += 1;
        OnScoreChanged?.Invoke(score);
        OnStatusChanged?.Invoke($"Hit! RT: {reactionTime:0.00}s");

        stats?.RegisterHit(reactionTime);

        currentTarget = null;
    }

    public void ReportMiss()
    {
        OnStatusChanged?.Invoke("Miss (timeout)");

        stats?.RegisterMiss();

        currentTarget = null;
    }

    public void EndSessionAndReturnToMenu()
    {
        running = false;

        if (currentTarget != null) Destroy(currentTarget);
        currentTarget = null;

        var r = new SessionResult
        {
            dateLocal = ProgressStore.TodayDateLocal(),
            score = score,
            attempts = stats != null ? stats.Attempts : 0,
            avgReactionTime = stats != null ? stats.AvgReactionTime : 0f,
            hitRate = stats != null ? stats.HitRate : 0f,
            neglectedSide = neglectedSideIsLeft ? "LEFT" : "RIGHT"
        };

        ProgressStore.SaveLast(r);
        ProgressStore.SaveToday(r);

        OnSessionEnded?.Invoke();

        if (debugLogs) Debug.Log("[ScanningTherapy] Session ended. Returning to menu.");

        SceneManager.LoadScene(menuSceneName);
    }
}