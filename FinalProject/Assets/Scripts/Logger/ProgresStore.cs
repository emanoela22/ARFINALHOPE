using UnityEngine;
using System;

public static class ProgressStore
{
    private const string KEY_LAST = "NEGLECT_LAST_SESSION_JSON";
    private const string KEY_TODAY = "NEGLECT_TODAY_SESSION_JSON";
    private const string KEY_TODAY_DATE = "NEGLECT_TODAY_DATE";

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

    public static int GetStreak()
    {
        // Simple “streak” placeholder:
        // 1 if today completed, else 0. (We can expand later to real streak history)
        return HasTodaySession() ? 1 : 0;
    }
}