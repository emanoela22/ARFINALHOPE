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
            FinishSession(true);
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

    // Records the session and shows its results; the menu is one tap away from there.
    public void FinishSession(bool completed)
    {
        if (!sessionRunning) return;
        sessionRunning = false;
        var tracker = FindFirstObjectByType<ButterflyAngularTracker>();
        if (tracker != null) tracker.IsPaused = true;
        var earlier = ProgressStore.LoadHistory();
        var result = SaveProgress(completed);
        // The session is over: a later app pause must not re-save it as unfinished.
        saved = true;
        if (result == null)
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }
        var summary = SessionRewards.Summarize(result, earlier,
            SessionRewards.ButterflyPace(GameSettings.holdTime, GameSettings.movementSpeed, GameSettings.movementRange),
            Mathf.Max(10f, GameSettings.sessionDuration));
        result.stars = summary.stars;
        ProgressStore.Record(result);
        SessionResultsScreen.Show(summary, menuSceneName, PlayAgain, tracker.PlayCatchSound);
    }

    public void PlayAgain()
    {
        var tracker = FindFirstObjectByType<ButterflyAngularTracker>();
        if (tracker == null) return;
        tracker.ResetExercise(); // also restarts this timer under a new session id
        tracker.IsPaused = false;
    }

    public SessionResult SaveProgress(bool completed = false)
    {
        if (saved || elapsedTime < 0.1f) return null;
        var tracker = FindFirstObjectByType<ButterflyAngularTracker>();
        if (tracker == null) return null;
        var result = new SessionResult
        {
            sessionId = sessionId,
            dateLocal = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            score = tracker.Score,
            avgReactionTime = tracker.AverageCatchTime,
            neglectedSide = GameSettings.trainLeftSide ? "Left" : "Right",
            durationSeconds = elapsedTime,
            completed = completed
        };
        ProgressStore.Record(result);
        saved = completed;
        return result;
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
