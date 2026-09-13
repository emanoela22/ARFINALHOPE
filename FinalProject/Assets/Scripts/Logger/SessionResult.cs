using UnityEngine;
using System;

[Serializable]
public class SessionResult
{
    public string dateLocal; //date
    public int score;
    public int attempts;
    public float avgReactionTime;
    public float hitRate;
    public string neglectedSide; //left/right
    public string sessionId;
    public float durationSeconds;
    public bool completed;
}
