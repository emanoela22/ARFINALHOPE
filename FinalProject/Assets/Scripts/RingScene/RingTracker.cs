using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RingTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private RectTransform ringRectTransform;
    [SerializeField] private Image ringImage;
    [SerializeField] private ButterflyMovement butterfly;
    [SerializeField] private TMP_Text scoreText;

    [Header("Tracking Settings")]
    [SerializeField] private float ringRadius = 75f;
    [SerializeField] private float holdTimeRequired = 1.0f;

    private float holdTimer = 0f;
    private int score = 0;

    private void Update()
    {
        if (butterfly == null || arCamera == null)
            return;

        Vector3 screenPos = arCamera.WorldToScreenPoint(butterfly.GetWorldPosition());

        bool isVisible = screenPos.z > 0f;
        if (!isVisible)
        {
            SetRingColor(Color.white);
            holdTimer = 0f;
            return;
        }

        float distanceToCenter = Vector2.Distance(screenPos, ringRectTransform.position);

        if (distanceToCenter <= ringRadius)
        {
            holdTimer += Time.deltaTime;
            SetRingColor(Color.green);

            if (holdTimer >= holdTimeRequired)
            {
                score++;
                UpdateScoreText();
                butterfly.ResetButterflyPosition();
                holdTimer = 0f;
            }
        }
        else
        {
            holdTimer = 0f;
            SetRingColor(Color.white);
        }
    }

    private void SetRingColor(Color color)
    {
        if (ringImage != null)
        {
            ringImage.color = color;
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    public void SetButterfly(ButterflyMovement newButterfly)
    {
        butterfly = newButterfly;
    }

    public void ResetScore()
    {
        score = 0;
        holdTimer = 0f;
        UpdateScoreText();
        SetRingColor(Color.white);
    }
}