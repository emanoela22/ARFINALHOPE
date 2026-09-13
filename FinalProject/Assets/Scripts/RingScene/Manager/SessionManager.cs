using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionManager : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private string menuSceneName = "MenuScene";

    private float remainingTime;
    private bool sessionRunning = true;
    private string sessionId;
    private float elapsedTime;
    private bool saved;
    public bool IsPaused { get; set; }
    public void HideLegacyTimer() { if (timerText != null) timerText.gameObject.SetActive(false); }

    public void RestartSession()
    {
        sessionId = System.Guid.NewGuid().ToString();
        elapsedTime = 0;
        saved = false;
        remainingTime = Mathf.Max(10f, GameSettings.sessionDuration);
        sessionRunning = true;
        UpdateTimerText();
    }

    private void Start()
    {
        ForceLandscapeOrientation();
        RestartSession();
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
        elapsedTime += Time.deltaTime;

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
        SaveProgress(true);
        SceneManager.LoadScene(menuSceneName);
    }

    public void SaveProgress(bool completed = false)
    {
        if (saved || elapsedTime < 0.1f) return;
        var tracker = FindFirstObjectByType<ButterflyAngularTracker>();
        if (tracker == null) return;
        ProgressStore.Record(new SessionResult
        {
            sessionId = sessionId,
            dateLocal = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            score = tracker.Score,
            avgReactionTime = tracker.AverageCatchTime,
            neglectedSide = GameSettings.trainLeftSide ? "Left" : "Right",
            durationSeconds = elapsedTime,
            completed = completed
        });
        saved = completed;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveProgress();
    }

    private void OnApplicationQuit() => SaveProgress();

    public float GetRemainingTime()
    {
        return remainingTime;
    }
}
