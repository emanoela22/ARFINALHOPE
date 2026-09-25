using UnityEngine;
using System;

[Serializable]
public class SessionResult
{
    public const string HandMusicExercise = "HandMusic";
    public string dateLocal; //date
    public int score;
    public int attempts;
    public float avgReactionTime;
    public float hitRate;
    public string neglectedSide; //left/right
    public string sessionId;
    public float durationSeconds;
    public bool completed;
    public string exercise; // HandMusicExercise, or empty for the butterfly exercise (and older records)
    public string mode; // Hand Music play mode
    public float reach; // Hand Music Fingers mode: farthest gold circle, 0 = beside the midline, 1 = edge of the view
    public int stars; // 0 when not rated (older records, or saved when the app closed mid-session)
    public bool IsHandMusic => exercise == HandMusicExercise;
}
