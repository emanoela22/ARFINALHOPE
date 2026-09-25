using System.Collections.Generic;
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

    // How straight the four fingers are: low in a fist, near 1 when fully open. Measured against
    // each finger's own length, so hand size and distance from the camera do not matter.
    public static float Openness(Vector3[] p)
    {
        if (p == null || p.Length < 21) return 0;
        float sum = 0;
        for (int finger = 0; finger < 4; finger++)
        {
            int joint = 5 + finger * 4;
            float length = Vector3.Distance(p[joint], p[joint + 1]) +
                Vector3.Distance(p[joint + 1], p[joint + 2]) + Vector3.Distance(p[joint + 2], p[joint + 3]);
            if (length > .0001f) sum += Vector3.Distance(p[joint], p[joint + 3]) / length;
        }
        return sum / 4;
    }

    // Wrist and knuckles only, so curled or weak fingers do not move it.
    public static Vector2 PalmCentre(Vector2[] p) => (p[0] + p[5] + p[9] + p[13] + p[17]) / 5;

    // Which of the patient's hands MediaPipe's label means. With the phone's front camera (whose image
    // the app mirrors like a selfie) the left hand comes back as "Right", as seen on a phone; an
    // unmirrored camera gives the opposite.
    public static bool IsLeftHand(string label, bool frontCamera) => label == (frontCamera ? "Right" : "Left");
}

// One tracked hand, reduced to what the play modes need.
public struct TrackedHand
{
    public bool visible;
    public Vector2 palm; // camera-image space: x from the screen's left (mirrored like a selfie), y from the bottom
    public float openness;
    public int fingers; // raised fingers, or 6 for the pinch that plays A
    public MusicGesture pose;
}

// Fingers mode: the hand has to stay inside the gold circle for the hold time. Brief tracking
// drop-outs are forgiven, so a weak or trembling hand does not lose its progress.
public sealed class DwellGate
{
    private float held, away;
    public float Progress(float hold) => Mathf.Clamp01(held / Mathf.Max(.1f, hold));
    public void Reset() { held = away = 0; }
    public bool Sample(bool inside, float delta, float hold)
    {
        if (!inside)
        {
            away += delta;
            if (away > .3f) held = 0;
            return false;
        }
        away = 0;
        held += delta;
        if (held < hold) return false;
        held = 0;
        return true;
    }
}

// Places gold circles on the trained half of the camera view: close to the midline at first,
// further toward the edge after quick catches, and back in again when a reach takes long.
public sealed class ReachTargets
{
    public const float MaxRadius = .12f; // the largest circle, as a share of the image height
    public float Distance { get; private set; } = .3f; // 0 = beside the midline, 1 = at the edge of the view
    public float Farthest { get; private set; }
    private int quickStreak;
    private bool high;

    public void Reset() { Distance = .3f; Farthest = 0; quickStreak = 0; }

    // Camera-image space; the preview is mirrored, so the patient's left is the screen's left.
    // The whole circle stays on the trained half, whatever its size.
    public Vector2 Next(bool trainLeft, float radius, float aspect)
    {
        high = !high;
        float half = radius / aspect + .01f;
        float offset = half + Distance * (.5f - 2 * half);
        return new Vector2(trainLeft ? .5f - offset : .5f + offset, high ? .58f : .38f);
    }

    public void Register(float seconds)
    {
        Farthest = Mathf.Max(Farthest, Distance);
        if (seconds > 4f)
        {
            quickStreak = 0;
            if (seconds > 10f) Distance = Mathf.Max(0, Distance - .1f);
            return;
        }
        if (++quickStreak < 2) return;
        quickStreak = 0;
        Distance = Mathf.Min(1, Distance + .15f);
    }

    // Round on screen: horizontal distance is scaled by the image's width/height.
    public static bool Contains(Vector2 target, Vector2 point, float aspect, float radius)
    {
        var offset = new Vector2((point.x - target.x) * aspect, point.y - target.y);
        return offset.sqrMagnitude <= radius * radius;
    }
}

// Open hand mode: a note needs the hand opened past a personal threshold after it has closed
// again, so each note is one grasp-and-release.
public sealed class OpenHandGate
{
    private float closedLevel = .55f, openLevel = .9f, held;
    public bool Armed { get; private set; }
    public float OpenAt => closedLevel + (openLevel - closedLevel) * .6f;
    public float CloseAt => closedLevel + (openLevel - closedLevel) * .3f;
    public float Progress(float hold) => Mathf.Clamp01(held / Mathf.Max(.1f, hold));
    public void Reset() { Armed = false; held = 0; }
    public void Calibrate(float closed, float open)
    {
        closedLevel = closed;
        openLevel = Mathf.Max(open, closed + .08f); // a very small range would trigger on tracking noise
        Reset();
    }
    public bool Sample(float openness, bool visible, float delta, float hold)
    {
        // Losing the hand resets a pending note but does not re-arm it.
        if (!visible) { held = 0; return false; }
        if (openness <= CloseAt) { Armed = true; held = 0; return false; }
        if (!Armed || openness < OpenAt) { held = 0; return false; }
        held += delta;
        if (held < hold) return false;
        Armed = false;
        held = 0;
        return true;
    }
}

// Measures one hand's own closed and open levels, so a weak hand only has to open as far as it can.
public sealed class OpennessCalibration
{
    public const float StepSeconds = 2f; // of visible hand, for each step
    private readonly List<float> closed = new(), open = new();
    private float visibleTime;
    public int Step { get; private set; } // 0 = resting closed, 1 = opening, 2 = done
    public bool Done => Step >= 2;
    public float Progress => Mathf.Clamp01(visibleTime / StepSeconds);
    // The median while resting; near the top while opening, ignoring a stray spike.
    public float ClosedLevel => Percentile(closed, .5f);
    public float OpenLevel => Percentile(open, .8f);

    public void Reset() { closed.Clear(); open.Clear(); visibleTime = 0; Step = 0; }

    public void Sample(float openness, bool visible, float delta)
    {
        if (Done || !visible) return;
        (Step == 0 ? closed : open).Add(openness);
        visibleTime += delta;
        if (visibleTime < StepSeconds) return;
        visibleTime = 0;
        Step++;
    }

    private static float Percentile(List<float> values, float share)
    {
        if (values.Count == 0) return 0;
        var sorted = new List<float>(values);
        sorted.Sort();
        return sorted[Mathf.RoundToInt(share * (sorted.Count - 1))];
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
