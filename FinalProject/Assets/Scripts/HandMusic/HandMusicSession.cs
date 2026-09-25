using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// A timed Hand Music session (length from Settings): counts correct song notes, saves progress
// and ends on the shared results screen with stars.
public partial class HandMusicController
{
    private string sessionId;
    private float sessionElapsed, remaining, lastNoteAt, songDoneAt;
    private int correctNotes, chimes;
    private bool sessionRunning, sessionOver, sessionSaved;

    private void BeginSession()
    {
        sessionId = Guid.NewGuid().ToString();
        sessionElapsed = lastNoteAt = 0;
        correctNotes = 0;
        remaining = GameSettings.handMusicDuration;
        sessionRunning = true;
        sessionOver = sessionSaved = false;
        song.Reset(); dwell.Reset(); targets.Reset();
        if (CircleMode) NextTarget();
        RefreshCounters();
    }

    // The clock only runs while the camera is tracking and nothing else (song choice, calibration) is open.
    private void TickSession(float delta, bool tracking)
    {
        if (!sessionRunning || !tracking || selectingSong) return;
        if (Mode == HandMusicMode.OpenHand && !calibration.Done) return;
        sessionElapsed += delta;
        remaining -= delta;
        // A finished song starts again after a short pause, until the time is up.
        if (song.Complete && sessionElapsed - songDoneAt > 2)
        {
            song.Reset();
            if (CircleMode) NextTarget();
            RefreshSongGuide();
        }
        RefreshCounters();
        if (remaining <= 0) FinishSession(true);
    }

    private void CountNote()
    {
        correctNotes++;
        lastNoteAt = sessionElapsed;
        RefreshCounters();
    }

    private void RefreshCounters()
    {
        countLabel.text = $"Notes: {correctNotes}";
        int seconds = Mathf.CeilToInt(Mathf.Max(0, sessionRunning || sessionOver ? remaining : GameSettings.handMusicDuration));
        timeLabel.text = $"Time {seconds / 60}:{seconds % 60:00}";
    }

    // Ends the session and shows its results; before anything was played it just returns to the menu.
    private void FinishSession(bool completed)
    {
        if (sessionOver) return;
        if (!sessionRunning) { SceneManager.LoadScene("MenuScene"); return; }
        sessionRunning = false;
        sessionOver = true;
        StopPlaying(); ClearOverlay();
        sweepAge = -1;
        targetShown = false;
        ShowTarget(false);
        sweep.gameObject.SetActive(false);
        var earlier = ProgressStore.LoadHistory();
        var result = SaveProgress(completed);
        // The session is over: a later app pause must not re-save it as unfinished.
        sessionSaved = true;
        if (result == null) { SceneManager.LoadScene("MenuScene"); return; }
        var summary = SessionRewards.Summarize(result, earlier,
            SessionRewards.HandMusicPace(Mode, GameSettings.handMusicHold, GameSettings.handMusicCircle),
            GameSettings.handMusicDuration);
        result.stars = summary.stars;
        ProgressStore.Record(result);
        if (webcam != null) webcam.Pause();
        chimes = 0;
        SessionResultsScreen.Show(summary, "MenuScene", PlayAgain, StarChime);
    }

    private SessionResult SaveProgress(bool completed)
    {
        if (sessionSaved || sessionElapsed < 0.1f) return null;
        var result = new SessionResult
        {
            sessionId = sessionId,
            dateLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            exercise = SessionResult.HandMusicExercise,
            mode = GameSettings.HandMusicModeNames[(int)Mode],
            score = correctNotes,
            avgReactionTime = correctNotes > 0 ? lastNoteAt / correctNotes : 0,
            neglectedSide = TrainLeft ? "Left" : "Right",
            durationSeconds = sessionElapsed,
            reach = CircleMode ? targets.Farthest : 0,
            completed = completed
        };
        ProgressStore.Record(result);
        sessionSaved = completed;
        return result;
    }

    private void PlayAgain()
    {
        if (webcam != null) webcam.Play();
        lastSample = Time.realtimeSinceStartup;
        BeginSession();
    }

    // The results screen's stars ring out as a rising C-E-G.
    private void StarChime() => PlayNote(new[] { 1, 3, 5 }[chimes++ % 3], 0);

    private void OnApplicationQuit() => SaveProgress(false);
}
