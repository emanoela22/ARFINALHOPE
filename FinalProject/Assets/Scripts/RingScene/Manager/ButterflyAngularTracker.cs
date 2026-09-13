using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ButterflyAngularTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform butterflyRect;
    [SerializeField] private RectTransform ringRect;
    [SerializeField] private RectTransform playAreaRect;
    [SerializeField] private Image ringImage;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private RectTransform ringHitboxRect;

    [Header("Angular Mapping")]
    [SerializeField] private float maxYawDegrees = 20f;
    [SerializeField] private float maxPitchDegrees = 10f;

    [Header("Canvas Relative Range")]
    [SerializeField][Range(0.1f, 1f)] private float horizontalRangePercent = 0.35f;
    [SerializeField][Range(0.1f, 1f)] private float verticalRangePercent = 0.25f;

    [Header("Movement Smoothing")]
    [SerializeField][Range(0.02f, 0.3f)] private float movementSmoothTime = 0.1f;
    [SerializeField][Range(0f, 2f)] private float angularDeadZone = 0.35f;

    [Header("Scoring")]
    [SerializeField] private float ringRadius = 90f;
    [SerializeField] private float holdTimeRequired = 1.0f;

    [Header("Training")]
    [SerializeField] private bool trainLeftSide = true;

    [Header("Adaptive Difficulty")]
    [SerializeField] private float startingMaxTargetYaw = 10f;
    [SerializeField] private float maximumTargetYaw = 22f;
    [SerializeField] private float targetYawStep = 2f;
    [SerializeField] private float startingTargetPitch = 4f;
    [SerializeField] private float maximumTargetPitch = 8f;
    [SerializeField] private float targetPitchStep = 0.75f;
    [SerializeField] private int quickSuccessesPerStep = 2;
    [SerializeField] private float quickSuccessThresholdSeconds = 4f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip targetEnteredClip;
    [SerializeField] private AudioClip targetCompletedClip;

    private bool wasInsideLastFrame = false;
    private float baselineYaw;
    private float baselinePitch;
    private Vector2 currentTargetAngles;
    private float holdTimer;
    private int score;
    private Vector2 movementVelocity;
    private Camera trackingCamera;
    private float currentMaxTargetYaw;
    private float currentTargetPitch;
    private float targetPresentedTime;
    private int quickSuccessStreak;
    private bool nextTargetIsTop;

    public string CurrentQuadrant { get; private set; }
    public bool IsPaused { get; set; }
    public bool NeedsDirectionHint { get; private set; }
    public Vector2 TargetDirection { get; private set; }
    public RectTransform ButterflyRect => butterflyRect;
    public event System.Action TargetCaught;

    public void ApplySettings()
    {
        trainLeftSide = GameSettings.trainLeftSide;
        holdTimeRequired = Mathf.Max(0.1f, GameSettings.holdTime);
        horizontalRangePercent = Mathf.Clamp(GameSettings.movementRange, 0.3f, 0.95f);
        verticalRangePercent = Mathf.Clamp(horizontalRangePercent * 0.6f, 0.18f, 0.65f);
    }

    private void Start()
    {
        trainLeftSide = GameSettings.trainLeftSide;
        holdTimeRequired = GameSettings.holdTime;
        ApplySettings();
        if (GetComponent<ExerciseControls>() == null)
            gameObject.AddComponent<ExerciseControls>();

        trackingCamera = Camera.main;
        currentMaxTargetYaw = Mathf.Clamp(startingMaxTargetYaw, 4f, maximumTargetYaw);
        currentTargetPitch = Mathf.Clamp(startingTargetPitch, 2f, maximumTargetPitch);
        nextTargetIsTop = Random.value >= 0.5f;

        ResetBaseline();
        PickNextTarget();
        UpdateScoreText();
        SetRingColor(Color.white);
    }

    private void LateUpdate()
    {
        if (IsPaused)
        {
            targetPresentedTime += Time.deltaTime;
            return;
        }
        UpdateButterflyPosition();
        CheckRingOverlap();
    }

    public void ResetBaseline()
    {
        if (!TryGetTrackingCamera(out Camera cam))
            return;

        Vector3 euler = cam.transform.eulerAngles;
        baselineYaw = NormalizeAngle(euler.y);
        baselinePitch = NormalizeAngle(euler.x);
        movementVelocity = Vector2.zero;
    }

    private void PickNextTarget()
    {
        // Targets always stay on the selected rehabilitation side. As the
        // player performs well, currentMaxTargetYaw expands farther outward.
        float minimumYaw = Mathf.Max(5f, currentMaxTargetYaw - 5f);
        float chosenMagnitude = Random.Range(minimumYaw, currentMaxTargetYaw);
        float chosenYaw = trainLeftSide ? -chosenMagnitude : chosenMagnitude;

        float pitchMagnitude = Random.Range(
            Mathf.Max(2f, currentTargetPitch - 1.5f),
            currentTargetPitch
        );

        // In this camera mapping, a negative pitch offset appears in the top
        // half of the screen and a positive offset appears in the bottom half.
        float chosenPitch = nextTargetIsTop ? -pitchMagnitude : pitchMagnitude;
        CurrentQuadrant = trainLeftSide
            ? (nextTargetIsTop ? "LT" : "LB")
            : (nextTargetIsTop ? "RT" : "RB");

        // Alternate vertically so a session exercises both quadrants rather
        // than repeatedly choosing one corner by chance.
        nextTargetIsTop = !nextTargetIsTop;
        currentTargetAngles = new Vector2(chosenYaw, chosenPitch);
        targetPresentedTime = Time.time;
    }

    private void UpdateButterflyPosition()
    {
        if (!TryGetTrackingCamera(out Camera cam) || butterflyRect == null || playAreaRect == null)
            return;

        Vector3 euler = cam.transform.eulerAngles;
        float currentYaw = NormalizeAngle(euler.y);
        float currentPitch = NormalizeAngle(euler.x);

        float targetYaw = baselineYaw + currentTargetAngles.x;
        float targetPitch = baselinePitch + currentTargetAngles.y;

        float yawDelta = Mathf.DeltaAngle(currentYaw, targetYaw);
        float pitchDelta = Mathf.DeltaAngle(currentPitch, targetPitch);

        // Ignore tiny AR pose corrections and normal hand tremor.
        if (Mathf.Abs(yawDelta) < angularDeadZone) yawDelta = 0f;
        if (Mathf.Abs(pitchDelta) < angularDeadZone) pitchDelta = 0f;

        float normalizedX = Mathf.Clamp(yawDelta / maxYawDegrees, -1f, 1f);
        float normalizedY = Mathf.Clamp(-pitchDelta / maxPitchDegrees, -1f, 1f);
        TargetDirection = new Vector2(yawDelta / maxYawDegrees, -pitchDelta / maxPitchDegrees);
        // Position is deliberately clamped to keep the target visible. Show a
        // directional hint when the actual target lies beyond that mapped view.
        NeedsDirectionHint = Mathf.Abs(TargetDirection.x) > 1f || Mathf.Abs(TargetDirection.y) > 1f;

        float halfWidth = playAreaRect.rect.width * 0.5f;
        float halfHeight = playAreaRect.rect.height * 0.5f;

        float maxX = halfWidth * horizontalRangePercent;
        float maxY = halfHeight * verticalRangePercent;

        Vector2 targetPosition = new Vector2(
            normalizedX * maxX,
            normalizedY * maxY
        );

        butterflyRect.anchoredPosition = Vector2.SmoothDamp(
            butterflyRect.anchoredPosition,
            targetPosition,
            ref movementVelocity,
            movementSmoothTime / Mathf.Clamp(GameSettings.movementSpeed, 0.25f, 2f)
        );
    }

    private void CheckRingOverlap()
    {
        if (butterflyRect == null || ringHitboxRect == null)
            return;

        Vector3 butterflyCenter = butterflyRect.TransformPoint(butterflyRect.rect.center);

        bool inside =
            RectTransformUtility.RectangleContainsScreenPoint(
                ringHitboxRect,
                butterflyCenter,
                null
            );

        if (inside)
        {
     
            if (!wasInsideLastFrame && audioSource != null && targetEnteredClip != null)
            {
                audioSource.PlayOneShot(targetEnteredClip);
            }

            holdTimer += Time.deltaTime;
            SetRingColor(Color.green);

            if (holdTimer >= holdTimeRequired)
            {

                if (audioSource != null && targetCompletedClip != null)
                {
                    audioSource.PlayOneShot(targetCompletedClip);
                }

                score++;
                TargetCaught?.Invoke();
                RegisterSuccessfulTarget();
                UpdateScoreText();
                holdTimer = 0f;
                SetRingColor(Color.white);

                // The user has physically turned to bring this target into the
                // ring. Make that new facing direction the centre of the next
                // quadrant grid instead of keeping the session's first frame.
                ResetBaseline();
                PickNextTarget();

                wasInsideLastFrame = false;
                return;
            }
        }
        else
        {
            holdTimer = 0f;
            SetRingColor(Color.white);
        }

        wasInsideLastFrame = inside;
    }

    private void SetRingColor(Color color)
    {
        if (ringImage != null)
            ringImage.color = color;
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private void RegisterSuccessfulTarget()
    {
        float completionTime = Time.time - targetPresentedTime;
        if (completionTime > quickSuccessThresholdSeconds)
        {
            quickSuccessStreak = 0;
            return;
        }

        quickSuccessStreak++;
        int successesNeeded = Mathf.Max(1, quickSuccessesPerStep);
        if (quickSuccessStreak < successesNeeded)
            return;

        currentMaxTargetYaw = Mathf.Min(
            maximumTargetYaw,
            currentMaxTargetYaw + targetYawStep
        );
        currentTargetPitch = Mathf.Min(
            maximumTargetPitch,
            currentTargetPitch + targetPitchStep
        );
        quickSuccessStreak = 0;
    }

    private bool TryGetTrackingCamera(out Camera cam)
    {
        if (trackingCamera == null)
            trackingCamera = Camera.main;

        cam = trackingCamera;
        return cam != null;
    }

    public void ResetExercise()
    {
        ApplySettings();
        wasInsideLastFrame = false;
        NeedsDirectionHint = false;
        var session = FindFirstObjectByType<SessionManager>();
        if (session != null) session.RestartSession();
        score = 0;
        holdTimer = 0f;
        quickSuccessStreak = 0;
        currentMaxTargetYaw = Mathf.Clamp(startingMaxTargetYaw, 4f, maximumTargetYaw);
        currentTargetPitch = Mathf.Clamp(startingTargetPitch, 2f, maximumTargetPitch);
        nextTargetIsTop = Random.value >= 0.5f;
        UpdateScoreText();
        ResetBaseline();
        PickNextTarget();
        SetRingColor(Color.white);
    }
}
