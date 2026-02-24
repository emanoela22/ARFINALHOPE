using UnityEngine;

public class SessionStats : MonoBehaviour
{
    public int Attempts { get; private set; }
    public int Hits { get; private set; }
    public float TotalReactionTime { get; private set; }

    public float AvgReactionTime =>
        Hits > 0 ? TotalReactionTime / Hits : 0f;

    public float HitRate =>
        Attempts > 0 ? (float)Hits / Attempts : 0f;

    public void ResetStats()
    {
        Attempts = 0;
        Hits = 0;
        TotalReactionTime = 0f;
    }

    public void RegisterHit(float reactionTime)
    {
        Attempts++;
        Hits++;
        TotalReactionTime += reactionTime;
    }

    public void RegisterMiss()
    {
        Attempts++;
    }
}