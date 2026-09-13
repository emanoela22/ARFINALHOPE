using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionManager : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private string menuSceneName = "MenuScene";

    private float remainingTime;
    private bool sessionRunning = true;
    public bool IsPaused { get; set; }

    public void RestartSession()
    {
        remainingTime = Mathf.Max(10f, GameSettings.sessionDuration);
        sessionRunning = true;
        UpdateTimerText();
    }

    private void Start()
    {
        ForceLandscapeOrientation();
        remainingTime = GameSettings.sessionDuration;
        UpdateTimerText();
    }

    private static void ForceLandscapeOrientation()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    private void Update()
    {
        if (!sessionRunning || IsPaused)
            return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            sessionRunning = false;
            EndSession();
        }

        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            timerText.text = "Time: " + seconds + "s";
        }
    }

    private void EndSession()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    public float GetRemainingTime()
    {
        return remainingTime;
    }
}
