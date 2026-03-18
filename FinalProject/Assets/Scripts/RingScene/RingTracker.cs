using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RingTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform ringRectTransform;
    [SerializeField] private Image ringImage;
    [SerializeField] private ButterflyUIManager butterflyUIManager;
    [SerializeField] private TMP_Text scoreText;

    [Header("Tracking Settings")]
    [SerializeField] private float ringRadius = 80f;
    [SerializeField] private float holdTimeRequired = 1.0f;

    private float holdTimer = 0f;
    private int score = 0;

    private void Start()
    {
        UpdateScoreText();
        SetRingColor(Color.white);
    }

    private void Update()
    {
        RectTransform butterflyRect = butterflyUIManager != null
            ? butterflyUIManager.GetCurrentButterflyRect()
            : null;

        if (butterflyRect == null)
        {
            holdTimer = 0f;
            SetRingColor(Color.white);
            return;
        }

        float distance = Vector2.Distance(
            butterflyRect.position,
            ringRectTransform.position
        );

        if (distance <= ringRadius)
        {
            holdTimer += Time.deltaTime;
            SetRingColor(Color.green);

            if (holdTimer >= holdTimeRequired)
            {
                score++;
                UpdateScoreText();
                holdTimer = 0f;
                SetRingColor(Color.white);

                butterflyUIManager.ShowNextButterfly();
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

    public void ResetScore()
    {
        score = 0;
        holdTimer = 0f;
        UpdateScoreText();
        SetRingColor(Color.white);

        if (butterflyUIManager != null)
        {
            butterflyUIManager.ShowNextButterfly();
        }
    }
}