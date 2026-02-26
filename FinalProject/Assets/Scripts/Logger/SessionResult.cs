using UnityEngine;
using System;

[Serializable]
public class SessionResult
{
    public string dateLocal;
    public int score;
    public int attempts;
    public float avgReactionTime;
    public float hitRate;
    public string neglectedSide;
}
