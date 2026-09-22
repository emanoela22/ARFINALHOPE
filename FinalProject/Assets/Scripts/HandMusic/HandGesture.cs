using UnityEngine;

public enum MusicGesture { None, Open, Fist, Pinch }

public static class HandGesture
{
    public static int NoteForHand(Vector3[] points)
        => Classify(points)==MusicGesture.Pinch ? 6 : FingerCount(FingerMask(points));
    public static int FingerMask(Vector3[] p)
    {
        if (p == null || p.Length < 21) return 0;
        float palm = Vector3.Distance(p[5], p[17]);
        if (palm < .0001f) return 0;
        int mask = 0;
        if (Vector3.Angle(p[2]-p[3],p[4]-p[3]) > 130f && Vector3.Distance(p[4],p[5]) > palm*.75f)
            mask |= 1;
        for (int finger=0;finger<4;finger++)
        {
            int joint=5+finger*4;
            if (IsExtended(p, joint))
                mask |= 1 << (finger+1);
        }
        return mask;
    }

    private static bool IsExtended(Vector3[] p, int joint)
    {
        // Work in the finger's own frame, not its distance from the wrist.
        // A relaxed finger can bend considerably while still clearly being raised.
        float length = Vector3.Distance(p[joint],p[joint+1]) +
            Vector3.Distance(p[joint+1],p[joint+2]) + Vector3.Distance(p[joint+2],p[joint+3]);
        return length > .0001f &&
            Vector3.Angle(p[joint]-p[joint+1],p[joint+3]-p[joint+1]) > 125f &&
            Vector3.Distance(p[joint],p[joint+3]) / length > .72f;
    }

    public static int FingerCount(int mask)
    {
        int count=0;
        for(int i=0;i<5;i++) if((mask & (1<<i))!=0)count++;
        return count;
    }
    // Uses palm-relative distances and joint angles, so either hand can be used.
    public static MusicGesture Classify(Vector3[] p)
    {
        if (p == null || p.Length < 21) return MusicGesture.None;
        float palm = Vector3.Distance(p[5], p[17]);
        if (palm < 0.0001f) return MusicGesture.None;
        int extended = 0;
        int folded = 0;
        int otherExtended = 0;
        for (int finger = 0; finger < 4; finger++)
        {
            int joint = 5 + finger * 4;
            float angle = Vector3.Angle(p[joint] - p[joint + 1], p[joint + 3] - p[joint + 1]);
            bool straight = IsExtended(p, joint);
            if (straight) { extended++; if (finger > 0) otherExtended++; }
            if (angle < 115f || Vector3.Distance(p[0], p[joint + 3]) < Vector3.Distance(p[0], p[joint + 1]) * 0.95f) folded++;
        }
        if (Vector3.Distance(p[4], p[8]) / palm < 0.35f && otherExtended >= 1) return MusicGesture.Pinch;
        if (extended >= 3) return MusicGesture.Open;
        if (folded == 4) return MusicGesture.Fist;
        return MusicGesture.None;
    }
}

public sealed class FingerMaskFilter
{
    private int previous, older;
    private bool initialized;
    public void Reset() { initialized=false;previous=older=0; }
    public int Sample(int mask)
    {
        if (!initialized) { initialized=true;previous=older=mask;return mask; }
        int stable=(mask & previous) | (mask & older) | (previous & older);
        older=previous;previous=mask;
        return stable;
    }
}

public sealed class FingerNoteGate
{
    private int candidate, lastNote;
    private float held, neutral;
    public float Progress(float hold) => Mathf.Clamp01(held/Mathf.Max(.1f,hold));
    public void Reset() { candidate=lastNote=0;held=neutral=0; }
    public bool Sample(int count, bool visible, bool playing, float delta, float hold)
    {
        if (!visible) { candidate=0;held=neutral=0;return false; }
        if (count==0)
        {
            candidate=0;held=0;neutral+=delta;
            if(neutral>=.25f)lastNote=0;
            return false;
        }
        neutral=0;
        if(!playing || count==lastNote) { candidate=0;held=0;return false; }
        if(candidate!=count) { candidate=count;held=0; }
        held+=delta;
        if(held<hold)return false;
        lastNote=count;held=0;return true;
    }
}

public sealed class GuidedHandSong
{
    public static readonly string[] Titles={"Hot Cross Buns","Twinkle, Twinkle, Little Star"};
    private static readonly int[][] Melodies={
        new[]{3,2,1,3,2,1,1,1,1,1,2,2,2,2,3,2,1},
        new[]{1,1,5,5,6,6,5,4,4,3,3,2,2,1,
            5,5,4,4,3,3,2,5,5,4,4,3,3,2,
            1,1,5,5,6,6,5,4,4,3,3,2,2,1}
    };
    private int[] Melody => Melodies[Selected];
    public int Selected { get; private set; }
    public string Title => Titles[Selected];
    public void Select(int index) { Selected=Mathf.Clamp(index,0,Melodies.Length-1);Reset(); }
    public int Position { get; private set; }
    public int Length => Melody.Length;
    public bool Complete => Position >= Melody.Length;
    public int Next => Complete ? 0 : Melody[Position];
    public bool Play(int note)
    {
        if(Complete || note!=Next)return false;
        Position++;return true;
    }
    public void Reset() => Position=0;
}

public sealed class GestureNoteGate
{
    private MusicGesture candidate;
    private float held, neutral;
    private bool latched;
    public float Progress(float hold) => Mathf.Clamp01(held / Mathf.Max(0.1f, hold));
    public bool NeedsRelease => latched;

    public void Reset() { candidate = MusicGesture.None; held = neutral = 0; latched = false; }

    public bool Sample(MusicGesture gesture, bool handVisible, float delta, float hold)
    {
        // Losing the hand resets a pending note, but cannot re-arm a held gesture.
        if (!handVisible) { candidate = MusicGesture.None; held = neutral = 0; return false; }
        if (gesture == MusicGesture.None)
        {
            held = 0;
            candidate = MusicGesture.None;
            neutral += delta;
            if (neutral >= 0.3f) latched = false;
            return false;
        }
        neutral = 0;
        if (latched) return false;
        if (candidate != gesture) { candidate = gesture; held = 0; }
        held += delta;
        if (held < hold) return false;
        latched = true;
        held = 0;
        return true;
    }
}
