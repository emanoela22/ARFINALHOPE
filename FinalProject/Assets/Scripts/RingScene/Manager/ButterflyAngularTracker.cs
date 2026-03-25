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

    [Header("Scoring")]
    [SerializeField] private float ringRadius = 90f;
    [SerializeField] private float holdTimeRequired = 1.0f;

    [Header("Training")]
    [SerializeField] private bool trainLeftSide = true;
    [SerializeField][Range(0f, 1f)] private float neglectedSideBias = 0.8f;

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

    private void Start()
    {
        trainLeftSide = GameSettings.trainLeftSide;
        holdTimeRequired = GameSettings.holdTime;
        horizontalRangePercent = GameSettings.movementRange;
        verticalRangePercent = GameSettings.movementRange * 0.6f;

        ResetBaseline();
        PickNextTarget();
        UpdateScoreText();
        SetRingColor(Color.white);
    }

    private void Update()
    {
        UpdateButterflyPosition();
        CheckRingOverlap();
    }

    public void ResetBaseline()
    {
        Vector3 euler = Camera.main.transform.eulerAngles;
        baselineYaw = NormalizeAngle(euler.y);
        baselinePitch = NormalizeAngle(euler.x);
    }

    private void PickNextTarget()
    {
        float[] leftSlots = { -8f, -14f, -20f };
        float[] centerSlots = { -2f, 0f, 2f };
        float[] rightSlots = { 8f, 14f, 20f };

        bool chooseNeglected = Random.value < neglectedSideBias;
        float chosenYaw;

        if (trainLeftSide)
        {
            if (chooseNeglected)
                chosenYaw = leftSlots[Random.Range(0, leftSlots.Length)];
            else
                chosenYaw = centerSlots[Random.Range(0, centerSlots.Length)];
        }
        else
        {
            if (chooseNeglected)
                chosenYaw = rightSlots[Random.Range(0, rightSlots.Length)];
            else
                chosenYaw = centerSlots[Random.Range(0, centerSlots.Length)];
        }

        float chosenPitch = Random.Range(-4f, 4f);
        currentTargetAngles = new Vector2(chosenYaw, chosenPitch);
    }

    private void UpdateButterflyPosition()
    {
        if (Camera.main == null || butterflyRect == null || playAreaRect == null)
            return;

        Vector3 euler = Camera.main.transform.eulerAngles;
        float currentYaw = NormalizeAngle(euler.y);
        float currentPitch = NormalizeAngle(euler.x);

        float targetYaw = baselineYaw + currentTargetAngles.x;
        float targetPitch = baselinePitch + currentTargetAngles.y;

        float yawDelta = Mathf.DeltaAngle(currentYaw, targetYaw);
        float pitchDelta = Mathf.DeltaAngle(currentPitch, targetPitch);

        float normalizedX = Mathf.Clamp(yawDelta / maxYawDegrees, -1f, 1f);
        float normalizedY = Mathf.Clamp(-pitchDelta / maxPitchDegrees, -1f, 1f);

        float halfWidth = playAreaRect.rect.width * 0.5f;
        float halfHeight = playAreaRect.rect.height * 0.5f;

        float maxX = halfWidth * horizontalRangePercent;
        float maxY = halfHeight * verticalRangePercent;

        butterflyRect.anchoredPosition = new Vector2(
            normalizedX * maxX,
            normalizedY * maxY
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
                UpdateScoreText();
                holdTimer = 0f;
                SetRingColor(Color.white);
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

    public void ResetExercise()
    {
        score = 0;
        holdTimer = 0f;
        UpdateScoreText();
        ResetBaseline();
        PickNextTarget();
        SetRingColor(Color.white);
    }
}