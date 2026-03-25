using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuUIController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject homePanel;
    public GameObject todaySessionPanel;
    public GameObject todayNotCompletedPanel;
    public GameObject lastSessionPanel;
    public GameObject howToPlayPanel;
    public GameObject settingsPanel;

    [Header("Home UI")]
    public TMP_Text welcomeText;
    public TMP_Text streakText;

    [Header("Today Panel UI")]
    public TMP_Text todayScoreText;
    public TMP_Text todayAvgRtText;
    public TMP_Text todayHitRateText;

    [Header("Last Panel UI")]
    public TMP_Text lastScoreText;
    public TMP_Text lastAvgRtText;
    public TMP_Text lastHitRateText;
    public TMP_Text lastDateText;

    [Header("Scene Names")]
    public string arSceneName = "ARScene";
    public string menuSceneName = "MenuScene";

    void Start()
    {
        ShowHome();
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (welcomeText != null) welcomeText.text = "Welcome back, User";
        if (streakText != null) streakText.text = $"Streak:\nDay {Mathf.Max(1, ProgressStore.GetStreak())}";

        var today = ProgressStore.LoadToday();
        if (today != null)
        {
            SetTodayTexts(today);
        }

        var last = ProgressStore.LoadLast();
        if (last != null)
        {
            SetLastTexts(last);
        }
    }

    void SetTodayTexts(SessionResult r)
    {
        if (todayScoreText != null) todayScoreText.text = $"Score: {r.score}";
        if (todayAvgRtText != null) todayAvgRtText.text = $"Avg Reaction Time: {r.avgReactionTime:0.00}s";
        if (todayHitRateText != null) todayHitRateText.text = $"Hit Rate: {r.hitRate:P0}";
    }

    void SetLastTexts(SessionResult r)
    {
        if (lastScoreText != null) lastScoreText.text = $"Score: {r.score}";
        if (lastAvgRtText != null) lastAvgRtText.text = $"Avg Reaction Time: {r.avgReactionTime:0.00}s";
        if (lastHitRateText != null) lastHitRateText.text = $"Hit Rate: {r.hitRate:P0}";
        if (lastDateText != null) lastDateText.text = $"Date: {r.dateLocal}";
    }

    // ---------- Button Hooks ----------
    public void OnStartSessionPressed()
    {
        ShowHowToPlay();
    }

    public void OnContinueFromHowToPlayPressed()
    {
        SceneManager.LoadScene(arSceneName);
    }

    public void OnTodaySessionPressed()
    {
        if (ProgressStore.HasTodaySession())
            ShowTodaySession();
        else
            ShowTodayNotCompleted();
    }

    public void OnLastSessionPressed()
    {
        ShowLastSession();
    }

    public void OnGoBackPressed()
    {
        ShowHome();
    }

    // ---------- Settings Hooks ----------
    public void SetTrainingSide(int sideIndex)
    {
        // 0 = Left, 1 = Right
        GameSettings.trainLeftSide = (sideIndex == 0);
    }

    public void SetSessionDuration(int durationIndex)
    {
        // 0 = 30s, 1 = 60s
        GameSettings.sessionDuration = durationIndex == 0 ? 30f : 60f;
    }

    public void SetHoldTime(float value)
    {
        GameSettings.holdTime = value;
    }

    public void SetMovementRange(float value)
    {
        GameSettings.movementRange = value;
    }

    // ---------- Panel helpers ----------
    void HideAll()
    {
        if (homePanel) homePanel.SetActive(false);
        if (todaySessionPanel) todaySessionPanel.SetActive(false);
        if (todayNotCompletedPanel) todayNotCompletedPanel.SetActive(false);
        if (lastSessionPanel) lastSessionPanel.SetActive(false);
        if (howToPlayPanel) howToPlayPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void ShowHome()
    {
        HideAll();
        if (homePanel) homePanel.SetActive(true);
    }

    public void ShowTodaySession()
    {
        HideAll();
        if (todaySessionPanel) todaySessionPanel.SetActive(true);
    }

    public void ShowTodayNotCompleted()
    {
        HideAll();
        if (todayNotCompletedPanel) todayNotCompletedPanel.SetActive(true);
    }

    public void ShowLastSession()
    {
        HideAll();
        if (lastSessionPanel) lastSessionPanel.SetActive(true);
    }

    public void ShowSettings()
    {
        HideAll();
        if (settingsPanel) settingsPanel.SetActive(true);
    }


    public void ShowHowToPlay()
    {
        HideAll();
        if (howToPlayPanel) howToPlayPanel.SetActive(true);
    }
}
