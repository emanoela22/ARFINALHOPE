using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

// Unity -batchmode -quit -projectPath <project> -executeMethod SessionResultsCheck.Run [-sessionResultsPreview <folder>]
public static class SessionResultsCheck
{
    private static readonly float Pace = SessionRewards.ButterflyPace(1, 1, .5f);

    public static void Run()
    {
        CheckStars();
        CheckPersonalBest();
        CheckHandMusicRewards();
        CheckStreaks();
        CheckMessages();
        CheckTools.KeepSavedData(CheckSessionFlow);
        CheckLayout(CheckTools.Argument("-sessionResultsPreview"));
        Debug.Log("SESSION_RESULTS_CHECK_PASS: stars, personal best, streaks, comparison text, session flow and layout.");
    }

    private static void CheckStars()
    {
        Require(Mathf.Approximately(ButterflyAngularTracker.TravelSeconds(1), 0.55f), "travel time");
        Require(Mathf.Approximately(Pace, SessionRewards.ButterflySeconds), "defaults run at the calibrated pace");
        var none = new List<SessionResult>();
        // At the defaults (30 s, 1 s hold, 1x speed, 50% range) a person with neglect earns 3 stars with 5 catches, 2 with 3,
        // 1 with any catch, and nothing with none.
        Require(SessionRewards.Targets(30, Pace) == (3, 5), "default targets");
        int[] expected = { 0, 1, 1, 2, 2, 3, 3, 3, 3 };
        for (int score = 0; score < expected.Length; score++)
            Require(SessionRewards.Summarize(Session("now", score, 30.02f), none, Pace, 30).stars == expected[score],
                "default stars for " + score);
        // Every difficulty setting moves the target: harder needs fewer catches, easier needs more.
        int Three(float hold, float speed, float range, float seconds = 30) =>
            SessionRewards.Targets(seconds, SessionRewards.ButterflyPace(hold, speed, range)).three;
        Require(Three(1, 1, .8f) < 5 && Three(1, 1, .3f) > 5, "movement range scales the target");
        Require(Three(2, 1, .5f) < 5 && Three(.5f, 1, .5f, 60) > 10, "hold time scales the target");
        Require(Three(1, .25f, .5f, 60) < 10 && Three(1, 2, .5f, 120) > 20, "movement speed scales the target");
        Require(Three(1, 1, .5f, 60) == 10, "a longer session scales in proportion");
        var slow = SessionRewards.Summarize(Session("now", 1, 15f), none, SessionRewards.ButterflyPace(4, 0.25f, .5f), 15);
        Require(slow.twoStarCatches == 1 && slow.threeStarCatches == 2 && slow.stars == 2, "slow settings lower the targets");
        Require(SessionRewards.Summarize(Session("now", 0, 15f), none, SessionRewards.ButterflyPace(4, 0.25f, .5f), 15).stars == 0,
            "nothing caught earns no stars");
        var marathon = SessionRewards.Summarize(Session("now", 40, 300.01f), none, Pace, 300);
        Require(marathon.threeStarCatches == 50 && marathon.twoStarCatches == 25 && marathon.stars == 2, "long session targets");
        // Ending early keeps the full session's targets, so stopping can never make stars easier.
        var early = SessionRewards.Summarize(Session("now", 3, 18f, completed: false), none, Pace, 30);
        Require(early.threeStarCatches == 5 && early.stars == 2, "ending early keeps the full targets");
        var quickExit = SessionRewards.Summarize(Session("now", 1, 3f, completed: false), none, Pace, 30);
        Require(quickExit.stars == 1, "one catch and a quick exit is still one star");
    }

    private static void CheckPersonalBest()
    {
        var first = SessionRewards.Summarize(Session("now", 5, 30.02f), new List<SessionResult>(), Pace, 30);
        Require(first.lastSession == null && first.previousBest < 0 && !first.newPersonalBest, "first session has nothing to beat");
        var history = new List<SessionResult>
        {
            Session("a", 6, 30.01f),
            Session("b", 10, 29.6f, completed: false), // ended early: never a best
            Session("c", 20, 60.02f), // another length
            Session("d", 7, 30.03f),
            Music("m", 40, 30.02f, "Fingers"), // Hand Music notes are never compared with catches
            Session("now", 3, 12f, completed: false) // saved when the app was paused mid-session
        };
        var beat = SessionRewards.Summarize(Session("now", 8, 30.02f), history, Pace, 30);
        Require(beat.previousBest == 7 && beat.newPersonalBest && !beat.matchedPersonalBest, "beats best of the same length");
        Require(beat.lastSession.sessionId == "d", "last session skips Hand Music and this session's own partial save");
        var tie = SessionRewards.Summarize(Session("now", 7, 30.02f), history, Pace, 30);
        Require(!tie.newPersonalBest && tie.matchedPersonalBest, "tie matches the best");
        var lower = SessionRewards.Summarize(Session("now", 5, 30.02f), history, Pace, 30);
        Require(!lower.newPersonalBest && !lower.matchedPersonalBest, "lower score is not a best");
        var early = SessionRewards.Summarize(Session("now", 30, 20f, completed: false), history, Pace, 30);
        Require(!early.newPersonalBest && early.previousBest < 0, "ended early cannot earn a best");
        var newLength = SessionRewards.Summarize(Session("now", 9, 45.01f), history, Pace, 45);
        Require(!newLength.newPersonalBest && newLength.previousBest < 0, "first session of a new length");
        var zero = SessionRewards.Summarize(Session("now", 0, 30f), new List<SessionResult> { Session("a", 0, 30f) }, Pace, 30);
        Require(!zero.newPersonalBest && !zero.matchedPersonalBest, "matching zero is not celebrated");
        // Older records have no exercise or mode saved; they still count as butterfly sessions.
        var legacy = new SessionResult { sessionId = "old", score = 5, durationSeconds = 30.01f, completed = true, exercise = null, mode = null };
        var fromLegacy = SessionRewards.Summarize(new SessionResult { sessionId = "now", score = 6, durationSeconds = 30.02f, completed = true, exercise = "", mode = "" },
            new List<SessionResult> { legacy }, Pace, 30);
        Require(fromLegacy.newPersonalBest && fromLegacy.lastSession == legacy, "records saved before Hand Music still compare");
    }

    private static void CheckHandMusicRewards()
    {
        float hold = GameSettings.DefaultHandMusicHold, circle = GameSettings.DefaultHandMusicCircle;
        // At the defaults (2 minutes, 0.6 s hold, 80% circle): 16 notes in Fingers mode, 20 in Open hand.
        float fingers = SessionRewards.HandMusicPace(HandMusicMode.Fingers, hold, circle);
        Require(SessionRewards.Targets(120, fingers) == (8, 16), "Fingers defaults");
        Require(SessionRewards.Targets(120, SessionRewards.HandMusicPace(HandMusicMode.OpenHand, hold, .5f)) == (10, 20),
            "Open hand defaults; circle size does not matter there");
        int Three(float holdTime, float size) =>
            SessionRewards.Targets(120, SessionRewards.HandMusicPace(HandMusicMode.Fingers, holdTime, size)).three;
        Require(Three(hold, .5f) < 16 && Three(hold, 1) > 16, "circle size scales the target: " + Three(hold, .5f) + "/" + Three(hold, 1));
        Require(Three(1.2f, circle) < 16 && Three(.3f, circle) > 16, "hold time scales the target");
        var summary = SessionRewards.Summarize(Music("now", 12, 120.01f, "Fingers"), new List<SessionResult>(), fingers, 120);
        Require(summary.threeStarCatches == 16 && summary.twoStarCatches == 8 && summary.stars == 2, "Hand Music stars");
        var history = new List<SessionResult>
        {
            Session("b", 50, 120.02f), // butterflies never count as notes
            Music("r", 18, 120.02f, "Fingers"),
            Music("f", 40, 120.02f, "Open hand") // another mode is not the same challenge
        };
        var music = SessionRewards.Summarize(Music("now", 20, 120.01f, "Fingers"), history, fingers, 120);
        Require(music.previousBest == 18 && music.newPersonalBest && music.lastSession.sessionId == "f",
            "Hand Music bests per mode; last session is the latest Hand Music one");
        Require(SessionResultsScreen.ComparisonText(music).StartsWith("Last session: 40 played in Open hand mode"),
            "other mode gives context: " + SessionResultsScreen.ComparisonText(music));
        Require(SessionResultsScreen.GoalText(summary) == "Play 16 notes next time for 3 stars", "note goal: " + SessionResultsScreen.GoalText(summary));
    }

    private static void CheckStreaks()
    {
        var today = new DateTime(2026, 9, 25);
        SessionResult Day(string id, int daysAgo, int score = 5, bool completed = true) => new SessionResult
        {
            sessionId = id, dateLocal = today.AddDays(-daysAgo).ToString("yyyy-MM-dd") + " 10:00",
            score = score, completed = completed, durationSeconds = 30
        };
        // Five, four and three days ago in a row; nothing finished two days ago; twice yesterday; today only a zero.
        var history = new List<SessionResult>
        {
            Day("a", 5), Day("b", 4), Day("c", 3), Day("d", 2, completed: false), Day("e", 1), Day("f", 1), Day("g", 0, score: 0)
        };
        Require(ProgressStore.BestStreak(history) == 3, "best streak");
        Require(ProgressStore.CurrentStreak(history, today) == 1, "a streak that reached yesterday is still alive today");
        Require(ProgressStore.CurrentStreak(history, today.AddDays(1)) == 0, "a missed day ends the streak");
        var now = Day("now", 0);
        var grown = SessionRewards.Summarize(now, history, Pace, 30);
        Require(grown.streak == 2 && grown.streakGrew, "today's first finished session grows the streak");
        var again = SessionRewards.Summarize(Day("later", 0), history.Append(now).ToList(), Pace, 30);
        Require(again.streak == 2 && !again.streakGrew, "a second session on the same day does not");
        var zero = SessionRewards.Summarize(Day("zero", 0, score: 0), history, Pace, 30);
        Require(zero.stars == 0 && !zero.streakGrew, "nothing caught: no stars and no streak");
        Require(!SessionRewards.Summarize(Day("early", 0, completed: false), history, Pace, 30).streakGrew, "ending early does not count");
    }

    private static void CheckMessages()
    {
        // Expected text uses the same culture formatting as the screen (e.g. 0,3s on a Romanian phone).
        var history = new List<SessionResult> { Session("a", 7, 30.01f, average: 2.4f) };
        string better = SessionResultsScreen.ComparisonText(Summary(9, 30.02f, 2.1f, history));
        Require(better == $"2 more than last time (7)  ·  {0.3f:0.0}s quicker per catch", "improvement: " + better);
        string lower = SessionResultsScreen.ComparisonText(Summary(5, 30.02f, 2.9f, history));
        Require(lower == $"Last session: 7 caught  ·  {2.9f:0.0}s per catch", "lower score stays neutral: " + lower);
        string otherLength = SessionResultsScreen.ComparisonText(Summary(9, 60.02f, 3f, history));
        Require(otherLength.StartsWith("Last session: 7 caught in 30s"), "other length gives context: " + otherLength);
        string first = SessionResultsScreen.ComparisonText(Summary(3, 30.02f, 3f, new List<SessionResult>()));
        Require(first.StartsWith("Your first session"), "first session: " + first);
        string goal = SessionResultsScreen.GoalText(Summary(3, 30.02f, 3f, history));
        Require(goal == "Catch 5 butterflies next time for 3 stars", "next goal: " + goal);
        string firstStar = SessionResultsScreen.GoalText(Summary(1, 30.02f, 3f, history));
        Require(firstStar == "Catch 3 butterflies next time for 2 stars", "next goal from one star: " + firstStar);
        string fromZero = SessionResultsScreen.GoalText(Summary(0, 30.02f, 0, history));
        Require(fromZero == "Catch 1 butterfly next time for 1 star", "next goal from nothing: " + fromZero);
        string top = SessionResultsScreen.GoalText(Summary(8, 30.02f, 3f, history));
        Require(top.StartsWith("All three stars"), "three stars: " + top);
        string early = SessionResultsScreen.GoalText(SessionRewards.Summarize(Session("now", 3, 17.4f, completed: false), history, Pace, 30));
        Require(early == "Catch 5 butterflies next time for 3 stars", "early goal uses the full session: " + early);
        var titleOf = typeof(SessionResultsScreen).GetMethod("Title", BindingFlags.NonPublic | BindingFlags.Static);
        Require((string)titleOf.Invoke(null, new object[] { Summary(0, 30.02f, 0, history) }) == "No butterflies this time" &&
            (string)titleOf.Invoke(null, new object[] { SessionRewards.Summarize(Music("now", 0, 120.01f, "Fingers"), history, 7.5f, 120) }) == "No notes this time",
            "no congratulations without a catch or note");
    }

    private static void CheckSessionFlow()
    {
        ProgressStore.ResetStatistics();
        ProgressStore.Record(Session("earlier", 4, 30.01f));
        GameSettings.sessionDuration = 30;
        GameSettings.holdTime = 1;
        GameSettings.movementSpeed = 1;
        GameSettings.movementRange = .5f;
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene 1.unity");
        var session = UnityEngine.Object.FindFirstObjectByType<SessionManager>();
        var tracker = UnityEngine.Object.FindFirstObjectByType<ButterflyAngularTracker>();
        Require(session != null && tracker != null, "exercise scene components");

        // The timer runs out: Update must show the results instead of loading the menu.
        session.RestartSession();
        Set(session, "elapsedTime", 30.02f);
        Set(session, "remainingTime", 0f);
        Set(tracker, "score", 6);
        Set(tracker, "totalCatchTime", 15f);
        Call(session, "Update");
        Require(tracker.IsPaused, "butterflies stop at the end");
        var screen = OnlyOne<SessionResultsScreen>("results shown when time runs out");
        int highest = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(canvas => canvas.gameObject != screen.gameObject).Select(canvas => canvas.sortingOrder)
            .DefaultIfEmpty(0).Max();
        Require(screen.GetComponent<Canvas>().sortingOrder > highest, "results drawn above the exercise");
        var badges = screen.transform.Find("Safe Area/Session details/Badge row");
        Require(badges != null && badges.Find("New personal best!") != null && badges.Find("Streak started!") != null,
            "personal best and new streak badges");
        var history = ProgressStore.LoadHistory();
        Require(history.Count == 2 && history[1].sessionId != "earlier" && history[1].completed && history[1].score == 6,
            "completed session recorded");
        Require(history[1].stars == 3 && !history[1].IsHandMusic, "stars saved with the butterfly session");

        // Pressing End session or pausing the app afterwards must not add or downgrade the record.
        session.FinishSession(false);
        Require(session.SaveProgress() == null, "no save after finishing");
        OnlyOne<SessionResultsScreen>("only one results screen");
        history = ProgressStore.LoadHistory();
        Require(history.Count == 2 && history[1].completed, "record stays completed");

        UnityEngine.Object.DestroyImmediate(screen.gameObject);
        session.PlayAgain();
        Require(!tracker.IsPaused && tracker.Score == 0 && Mathf.Approximately(session.GetRemainingTime(), 30),
            "play again starts a fresh session");

        // Ending early records an unfinished session and still shows results.
        Set(session, "elapsedTime", 12.5f);
        Set(tracker, "score", 2);
        session.FinishSession(false);
        history = ProgressStore.LoadHistory();
        Require(history.Count == 3 && !history[2].completed && Mathf.Approximately(history[2].durationSeconds, 12.5f),
            "early end recorded as unfinished");
        UnityEngine.Object.DestroyImmediate(OnlyOne<SessionResultsScreen>("results shown after ending early").gameObject);
    }

    private static void CheckLayout(string folder)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        UnityEngine.Random.InitState(7);
        var history = new List<SessionResult>
        {
            Session("a", 6, 30.01f, average: 2.6f),
            Session("b", 12, 60.02f, average: 3f),
            Session("c", 7, 30.03f, average: 2.4f),
            Music("m", 21, 120.02f, "Fingers", average: 7.4f)
        };
        var full = new Rect(0, 0, 1, 1);
        // Both badges at once: a new personal best on a day that grew the streak.
        var best = Summary(9, 30.02f, 2.1f, history);
        best.streak = 4;
        best.streakGrew = true;
        Layout("best-16x9", best, new Vector2(1920, 1080), full, folder);
        // Canvas of a 19.5:9 phone, with landscape notch and home-indicator insets.
        Layout("first-phone", Summary(3, 30.02f, 4.2f, new List<SessionResult>()), new Vector2(2118, 979),
            new Rect(0.056f, 0.054f, 0.888f, 0.946f), folder);
        Layout("early-4x3", SessionRewards.Summarize(Session("now", 3, 18f, completed: false, average: 3.4f), history, Pace, 30),
            new Vector2(1663, 1247), full, folder);
        Layout("lower-20x9", Summary(5, 30.02f, 2.9f, history), new Vector2(2147, 966), full, folder);
        Layout("matched-16x9", Summary(7, 30.02f, 2.4f, history), new Vector2(1920, 1080), full, folder);
        Layout("none-20x9", Summary(0, 30.02f, 0, history), new Vector2(2147, 966), full, folder);
        Layout("music-16x9", SessionRewards.Summarize(Music("now", 22, 120.01f, "Fingers", average: 5.3f), history,
            SessionRewards.HandMusicPace(HandMusicMode.Fingers, .6f, .8f), 120), new Vector2(1920, 1080), full, folder);
        if (folder != null) Timeline(Summary(9, 30.02f, 2.1f, history), Path.Combine(folder, "timeline.png"));
        Debug.Log("SESSION_RESULTS_LAYOUT_PASS");
    }

    private static void Layout(string name, SessionSummary summary, Vector2 size, Rect safe, string folder)
    {
        var screen = Preview(summary, size);
        var safeArea = (RectTransform)screen.transform.Find("Safe Area");
        safeArea.anchorMin = safe.min;
        safeArea.anchorMax = safe.max;
        Canvas.ForceUpdateCanvases();
        for (int frame = 0; frame < 100; frame++) screen.Tick(0.02f); // 2 s: all revealed, confetti in the air
        var overflow = CheckTools.OverflowingText(screen).ToList();
        Require(overflow.Count == 0, name + " text overflows: " + string.Join(" | ", overflow));
        var details = (RectTransform)safeArea.Find("Session details");
        Require(LayoutUtility.GetPreferredHeight(details) <= details.rect.height, name + " details fit between band and buttons");
        if (folder != null)
        {
            var image = new Texture2D((int)size.x, (int)size.y, TextureFormat.RGB24, false);
            CheckTools.Render(size, image, 0, 0, image.width, image.height);
            CheckTools.SavePng(image, Path.Combine(folder, name + ".png"));
        }
        UnityEngine.Object.DestroyImmediate(screen.gameObject);
    }

    private static void Timeline(SessionSummary summary, string path)
    {
        // Six moments of the 3-star personal-best sequence, in a 3x2 sheet.
        float[] moments = { 0.3f, 0.7f, 1.2f, 1.6f, 2.2f, 3.5f };
        var size = new Vector2(1920, 1080);
        var screen = Preview(summary, size);
        var sheet = new Texture2D(1920, 720, TextureFormat.RGB24, false);
        float time = 0;
        for (int i = 0; i < moments.Length; i++)
        {
            for (; time < moments[i]; time += 0.02f) screen.Tick(0.02f);
            CheckTools.Render(size, sheet, i % 3 * 640, (1 - i / 3) * 360, 640, 360);
        }
        CheckTools.SavePng(sheet, path);
        UnityEngine.Object.DestroyImmediate(screen.gameObject);
    }

    private static SessionResultsScreen Preview(SessionSummary summary, Vector2 size)
    {
        var screen = SessionResultsScreen.Show(summary, "MenuScene", () => { }, null);
        CheckTools.UseWorldSpace(screen.GetComponent<Canvas>(), size);
        return screen;
    }

    private static SessionSummary Summary(int score, float seconds, float average, List<SessionResult> history) =>
        SessionRewards.Summarize(Session("now", score, seconds, average: average), history, Pace, Mathf.Round(seconds));

    private static SessionResult Session(string id, int score, float seconds, bool completed = true, float average = 3f) =>
        new SessionResult { sessionId = id, score = score, durationSeconds = seconds, completed = completed, avgReactionTime = average };

    private static SessionResult Music(string id, int score, float seconds, string mode, float average = 3f)
    {
        var result = Session(id, score, seconds, average: average);
        result.exercise = SessionResult.HandMusicExercise;
        result.mode = mode;
        return result;
    }

    private static T OnlyOne<T>(string name) where T : UnityEngine.Object
    {
        var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        Require(found.Length == 1, name);
        return found[0];
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    private static void Require(bool ok, string name) { if (!ok) throw new Exception("Session results check failed: " + name); }
}
