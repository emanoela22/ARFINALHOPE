using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SessionLogger : MonoBehaviour
{
    [Serializable]
    public class LogEvent
    {
        public string type;
        public float t;
        public string data;
    }

    [Serializable]
    public class SessionData
    {
        public string sessionId;
        public string neglectedSide;
        public float startTimeUnix;
        public float endTimeUnix;
        public int finalScore;
        public List<LogEvent> events = new();
    }

    private SessionData session;

    public void BeginSession(string neglectedSide)
    {
        session = new SessionData
        {
            sessionId = Guid.NewGuid().ToString(),
            neglectedSide = neglectedSide,
            startTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        Log("session_start", Time.time, $"neglectedSide={neglectedSide}");
    }

    public void EndSession(int finalScore)
    {
        if (session == null) return;

        session.finalScore = finalScore;
        session.endTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Log("session_end", Time.time, $"score={finalScore}");

        SaveToJson();
        session = null;
    }

    public void LogSpawn(float t, Vector3 start, Vector3 end, float speed, float reactionWindow)
    {
        Log("spawn", t, $"start={start} end={end} speed={speed:F2} window={reactionWindow:F2}");
    }

    public void LogHit(float t, float reactionTime, bool success)
    {
        Log("hit", t, $"success={success} reaction={reactionTime:F2}");
    }

    public void LogDifficulty(float t, float speed, float endOffset, float reactionWindow)
    {
        Log("difficulty", t, $"speed={speed:F2} endOffset={endOffset:F2} window={reactionWindow:F2}");
    }

    private void Log(string type, float t, string data)
    {
        if (session == null) return;
        session.events.Add(new LogEvent { type = type, t = t, data = data });
    }

    private void SaveToJson()
    {
        try
        {
            string json = JsonUtility.ToJson(session, prettyPrint: true);
            string fileName = $"session_{session.sessionId}.json";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, json);
            Debug.Log($"[SessionLogger] Saved: {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SessionLogger] Save failed: " + e.Message);
        }
    }
}
