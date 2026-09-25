using UnityEngine;
using System;

public static class ProgressStore
{
    [Serializable]
    private class History { public System.Collections.Generic.List<SessionResult> sessions = new(); }
    private const string HistoryKey = "NEGLECT_SESSION_HISTORY_V1";

    public static System.Collections.Generic.List<SessionResult> LoadHistory()
    {
        try
        {
            var data = JsonUtility.FromJson<History>(PlayerPrefs.GetString(HistoryKey, "{}"));
            return data?.sessions ?? new System.Collections.Generic.List<SessionResult>();
        }
        catch (Exception) { return new System.Collections.Generic.List<SessionResult>(); }
    }

    public static void Record(SessionResult result)
    {
        var records = LoadHistory();
        int index = records.FindIndex(r => r.sessionId == result.sessionId);
        if (index >= 0) records[index] = result;
        else records.Add(result);
        PlayerPrefs.SetString(HistoryKey, JsonUtility.ToJson(new History { sessions = records }));
        SaveLast(result);
        SaveToday(result);
    }
    private const string KEY_LAST = "NEGLECT_LAST_SESSION_JSON";
    private const string KEY_TODAY = "NEGLECT_TODAY_SESSION_JSON";
    private const string KEY_TODAY_DATE = "NEGLECT_TODAY_DATE";

    public static void ResetStatistics()
    {
        // Clear only the score store, preserving unrelated preferences.
        PlayerPrefs.DeleteKey(HistoryKey);
        PlayerPrefs.DeleteKey(KEY_LAST);
        PlayerPrefs.DeleteKey(KEY_TODAY);
        PlayerPrefs.DeleteKey(KEY_TODAY_DATE);
        PlayerPrefs.Save();
    }

    public static string TodayDateLocal()
        => DateTime.Now.ToString("yyyy-MM-dd");

    public static void SaveLast(SessionResult r)
    {
        PlayerPrefs.SetString(KEY_LAST, JsonUtility.ToJson(r));
        PlayerPrefs.Save();
    }

    public static SessionResult LoadLast()
    {
        if (!PlayerPrefs.HasKey(KEY_LAST)) return null;
        return JsonUtility.FromJson<SessionResult>(PlayerPrefs.GetString(KEY_LAST));
    }

    public static void SaveToday(SessionResult r)
    {
        PlayerPrefs.SetString(KEY_TODAY, JsonUtility.ToJson(r));
        PlayerPrefs.SetString(KEY_TODAY_DATE, TodayDateLocal());
        PlayerPrefs.Save();
    }

    public static SessionResult LoadToday()
    {
        if (!PlayerPrefs.HasKey(KEY_TODAY)) return null;
        if (!PlayerPrefs.HasKey(KEY_TODAY_DATE)) return null;

        string savedDate = PlayerPrefs.GetString(KEY_TODAY_DATE);
        if (savedDate != TodayDateLocal()) return null; // not today's data

        return JsonUtility.FromJson<SessionResult>(PlayerPrefs.GetString(KEY_TODAY));
    }

    public static bool HasTodaySession() => LoadToday() != null;

    public static int GetStreak() => CurrentStreak(LoadHistory(), DateTime.Now);

    // Days in a row with at least one finished session that caught or played something (either
    // exercise), up to today. A streak that reached yesterday stays alive until today is over, so it
    // is not lost first thing in the morning.
    public static int CurrentStreak(System.Collections.Generic.List<SessionResult> history, DateTime today)
    {
        var days = PracticeDays(history);
        var day = today.Date;
        if (!days.Contains(day)) day = day.AddDays(-1);
        int streak = 0;
        for (; days.Contains(day); day = day.AddDays(-1)) streak++;
        return streak;
    }

    public static int BestStreak(System.Collections.Generic.List<SessionResult> history)
    {
        var days = new System.Collections.Generic.List<DateTime>(PracticeDays(history));
        days.Sort();
        int best = 0, run = 0;
        for (int i = 0; i < days.Count; i++)
        {
            run = i > 0 && days[i] == days[i - 1].AddDays(1) ? run + 1 : 1;
            best = Math.Max(best, run);
        }
        return best;
    }

    // The calendar day a session was saved on, from its "yyyy-MM-dd HH:mm" stamp.
    public static bool TryGetDay(SessionResult result, out DateTime day)
    {
        day = default;
        return result.dateLocal != null && result.dateLocal.Length >= 10 && DateTime.TryParseExact(result.dateLocal.Substring(0, 10),
            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out day);
    }

    private static System.Collections.Generic.HashSet<DateTime> PracticeDays(System.Collections.Generic.List<SessionResult> history)
    {
        var days = new System.Collections.Generic.HashSet<DateTime>();
        foreach (var result in history)
            if (result.completed && result.score > 0 && TryGetDay(result, out var day)) days.Add(day);
        return days;
    }
}
