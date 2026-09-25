using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// What one finished session earned, compared with the sessions saved before it.
public class SessionSummary
{
    public SessionResult result;
    public int stars; // 0-3; nothing caught or played earns none
    public int twoStarCatches, threeStarCatches;
    public int previousBest = -1; // best earlier completed session of the same kind and length, -1 if none
    public bool newPersonalBest, matchedPersonalBest;
    public SessionResult lastSession; // most recent earlier session of the same exercise, null on the first one
    public int streak; // days in a row with a finished session, including this one
    public bool streakGrew; // this session was the first finished one today
    public int NextStarCatches => stars == 0 ? 1 : stars == 1 ? twoStarCatches : threeStarCatches;
}

public static class SessionRewards
{
    // Calibrated so that a person with neglect can earn 3 stars at the default settings (to be refined
    // with the care team): one butterfly every 6 s (5 in the 30-second session), and one Hand Music note
    // every 7.5 s in Fingers mode (16 in 2 minutes) or 6 s in Open hand mode. Harder settings make each
    // catch or note take longer, so they need fewer; easier settings need more.
    public const float ButterflySeconds = 6f;
    private static readonly float[] HandMusicSeconds = { 7.5f, 6f }; // by HandMusicMode
    private const float ReachSeconds = 3f; // the part of each note spent reaching the gold circle
    public const float TwoStarShare = 0.5f;

    // The hold, the butterfly's travel, and aiming, which grows with the movement range: the butterfly
    // shows farther out toward the neglected side and the ring needs finer aim.
    public static float ButterflyPace(float holdTime, float movementSpeed, float movementRange)
    {
        float aim = ButterflySeconds - GameSettings.DefaultHoldTime -
            ButterflyAngularTracker.TravelSeconds(GameSettings.DefaultMovementSpeed);
        return Mathf.Max(0.1f, holdTime) + ButterflyAngularTracker.TravelSeconds(movementSpeed) +
            aim * movementRange / GameSettings.DefaultMovementRange;
    }

    // The mode's default pace, adjusted for the hold time. In Fingers mode a smaller gold circle takes
    // longer to reach (and a larger one less), though less than in proportion to its size.
    public static float HandMusicPace(HandMusicMode mode, float hold, float circle)
    {
        float pace = HandMusicSeconds[(int)mode] + Mathf.Max(0.1f, hold) - GameSettings.DefaultHandMusicHold;
        if (mode == HandMusicMode.Fingers)
            pace += ReachSeconds * (Mathf.Sqrt(GameSettings.DefaultHandMusicCircle / Mathf.Max(0.1f, circle)) - 1);
        return pace;
    }

    // Catches or notes needed for 2 and 3 stars in a session of this length, rounded to the nearest
    // whole one so easier and harder settings move the target alike.
    public static (int two, int three) Targets(float seconds, float secondsPerTarget)
    {
        int three = Mathf.Max(2, Mathf.FloorToInt(seconds / Mathf.Max(0.1f, secondsPerTarget) + 0.5f));
        return (Mathf.Max(1, Mathf.CeilToInt(three * TwoStarShare)), three);
    }

    // Targets come from the planned session length, so ending early can never make stars easier.
    public static SessionSummary Summarize(SessionResult result, List<SessionResult> history,
        float secondsPerTarget, float plannedSeconds)
    {
        var summary = new SessionSummary { result = result };
        (summary.twoStarCatches, summary.threeStarCatches) = Targets(plannedSeconds, secondsPerTarget);
        summary.stars = result.score >= summary.threeStarCatches ? 3 : result.score >= summary.twoStarCatches ? 2
            : result.score > 0 ? 1 : 0;
        // An app pause may already have saved part of this session under the same id.
        var earlier = history.Where(r => r.sessionId != result.sessionId).ToList();
        foreach (var session in earlier)
        {
            if (session.IsHandMusic != result.IsHandMusic) continue;
            summary.lastSession = session;
            if (Comparable(session, result)) summary.previousBest = Mathf.Max(summary.previousBest, session.score);
        }
        summary.newPersonalBest = summary.previousBest >= 0 && result.score > summary.previousBest;
        summary.matchedPersonalBest = summary.previousBest > 0 && result.score == summary.previousBest;
        // Streaks count finished sessions of either exercise.
        if (ProgressStore.TryGetDay(result, out var today))
        {
            int before = ProgressStore.CurrentStreak(earlier, today);
            summary.streak = ProgressStore.CurrentStreak(earlier.Append(result).ToList(), today);
            summary.streakGrew = summary.streak > before;
        }
        return summary;
    }

    // Completed sessions of the same exercise, mode and length. Sessions ended early never set or earn a best.
    public static bool Comparable(SessionResult a, SessionResult b) =>
        a.completed && b.completed && a.IsHandMusic == b.IsHandMusic && (a.mode ?? "") == (b.mode ?? "") &&
        Mathf.RoundToInt(a.durationSeconds) == Mathf.RoundToInt(b.durationSeconds);
}
