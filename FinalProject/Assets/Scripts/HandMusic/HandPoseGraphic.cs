using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The guide hand, drawn as a mesh (no font emoji or external artwork): a rounded palm with one darker
// outline around the whole hand and a soft lighter shade inside. Raised fingers stand tall with a white
// tip, as in the camera overlay; folded ones are short, darker bumps over the palm, so the count to hold
// up reads at a glance. When the pose changes, the hand gives a small hop and the fingers that change move
// one by one, rising in counting order.
[RequireComponent(typeof(CanvasRenderer))]
public class HandPoseGraphic : Graphic
{
    public MusicGesture gesture;
    public int fingerCount = -1;
    public bool mirrored; // thumb on the right: a left hand as seen in the mirrored camera view

    // A finger in a 100-unit square (y up, thumb on the left): its joints, whether it stands out from the
    // palm (1) or lies folded over it (0), and whether its tip is marked white as raised.
    private struct Finger { public Vector2 root, joint, tip; public float stand, mark; }

    private const float Outline = 2.6f, Rise = .28f, Stagger = .09f, Hop = .35f;
    private static readonly Vector2 HopCentre = new Vector2(50, 40);
    // Thumb, then index, middle, ring and little finger: their size at the root, joint and tip.
    private static readonly float[][] Radii = { new[] { 7.6f, 6.5f, 5.7f }, new[] { 5.6f, 5.3f, 4.9f },
        new[] { 5.8f, 5.5f, 5.1f }, new[] { 5.4f, 5.1f, 4.7f }, new[] { 4.8f, 4.5f, 4.1f } };
    private static readonly Vector2[] Roots = { new Vector2(42, 26), new Vector2(41.5f, 51), new Vector2(53.5f, 53), new Vector2(65, 51), new Vector2(75.5f, 47) };
    // The fingers fan out a little and differ in length, like a relaxed open hand.
    private static readonly float[] Lean = { 0, -7, -1, 5, 13 }, Length = { 0, 33, 37, 34, 25 };
    // The palm is widest across the knuckles.
    private static readonly Vector2[] Palm = { new Vector2(46, 17), new Vector2(65, 17), new Vector2(72, 49), new Vector2(40, 49) };
    private static readonly float[] PalmRadii = { 10, 10, 9, 9 };
    // Counting order: index, middle, ring and little finger, then the thumb for five.
    private static readonly int[] Counting = { 1, 2, 3, 4, 0 };

    private readonly Finger[] from = new Finger[5], to = new Finger[5], drawn = new Finger[5];
    private readonly float[] delay = new float[5];
    private readonly List<Vector2> ring = new List<Vector2>(), core = new List<Vector2>();
    private readonly Vector2[] pair = new Vector2[2];
    private readonly float[] pairRadii = new float[2];
    private MusicGesture shownGesture;
    private int shownCount = int.MinValue;
    private float changedAt = float.NegativeInfinity, settledAfter;
    private bool animating;
    private Rect area;

    // Finger 0 is the thumb, raised only for 5; fingers 1-4 are raised up to the count. The pinch that
    // plays A raises the middle finger while the thumb and index tips meet.
    public static bool Raised(MusicGesture gesture, int fingerCount, int finger) => gesture == MusicGesture.Pinch ? finger == 2
        : gesture == MusicGesture.Open && (fingerCount < 0 || (finger == 0 ? fingerCount == 5 : finger <= fingerCount));

    private void Update()
    {
        if (animating) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (gesture != shownGesture || fingerCount != shownCount) Retarget();
        float seconds = Time.unscaledTime - changedAt;
        for (int i = 0; i < 5; i++) drawn[i] = Blend(from[i], to[i], (seconds - delay[i]) / Rise);
        animating = seconds < settledAfter;
        area = rectTransform.rect;
        Draw(vh, seconds < Hop ? 1 + .05f * Mathf.Sin(Mathf.PI * seconds / Hop) : 1);
    }

    // A new pose starts from wherever the hand is drawn now (from a fist the first time), so a change
    // part-way through another carries on smoothly.
    private void Retarget()
    {
        bool first = shownCount == int.MinValue;
        shownGesture = gesture;
        shownCount = fingerCount;
        float next = .06f;
        settledAfter = Hop;
        foreach (int i in Counting)
        {
            from[i] = first ? Pose(MusicGesture.Fist, 0, i) : drawn[i];
            to[i] = Pose(gesture, fingerCount, i);
            bool rises = to[i].stand > from[i].stand + .5f || to[i].mark > from[i].mark + .5f;
            delay[i] = rises ? next : 0;
            if (rises) next += Stagger;
            settledAfter = Mathf.Max(settledAfter, delay[i] + Rise);
        }
        // Only play mode animates; edit-mode previews show the pose settled.
        changedAt = Application.isPlaying ? Time.unscaledTime : float.NegativeInfinity;
    }

    private static Finger Pose(MusicGesture gesture, int fingerCount, int finger)
    {
        var root = Roots[finger];
        // The pinch that plays A: the index curls down to meet the thumb.
        if (gesture == MusicGesture.Pinch && finger < 2)
            return finger == 0 ? Bent(root, new Vector2(27, 40), new Vector2(19.5f, 57), 1, 0) : Bent(root, new Vector2(37, 68), new Vector2(22, 62), 1, 0);
        bool raised = Raised(gesture, fingerCount, finger);
        // The thumb angles out from the side of the palm, or folds from that side across it.
        if (finger == 0)
            return raised ? Bent(root, new Vector2(28, 38), new Vector2(18, 53), 1, 1) : Bent(new Vector2(36, 26), new Vector2(46, 31), new Vector2(55, 35), 0, 0);
        float lean = Lean[finger] * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Sin(lean), Mathf.Cos(lean));
        // A folded finger curls down over the palm, leaving a short bump above it.
        return raised ? Bent(root, root + direction * Length[finger] * .52f, root + direction * Length[finger], 1, 1)
            : Bent(root, root + direction * 5.5f, root + direction * 8.5f, 0, 0);
    }

    private static Finger Bent(Vector2 root, Vector2 joint, Vector2 tip, float stand, float mark) =>
        new Finger { root = root, joint = joint, tip = tip, stand = stand, mark = mark };

    // Part-way from one pose to the next. The joints ease out with a slight overshoot, so fingers spring
    // into place; colour and white tip follow the same curve without the overshoot.
    private static Finger Blend(Finger a, Finger b, float progress)
    {
        float v = Mathf.Clamp01(progress) - 1, spring = 1 + 2.2f * v * v * v + 1.2f * v * v;
        return new Finger
        {
            root = Vector2.LerpUnclamped(a.root, b.root, spring),
            joint = Vector2.LerpUnclamped(a.joint, b.joint, spring),
            tip = Vector2.LerpUnclamped(a.tip, b.tip, spring),
            stand = Mathf.Lerp(a.stand, b.stand, spring),
            mark = Mathf.Lerp(a.mark, b.mark, spring)
        };
    }

    private void Draw(VertexHelper vh, float hop)
    {
        Color edge = Tone(Color.black, .45f), fill = Tone(Color.white, .15f), shade = Tone(Color.white, .42f);
        Color folded = Tone(Color.black, .08f), foldedShade = Tone(Color.white, .15f);
        // One outline around the palm and the fingers standing out from it...
        Hull(ring, Palm, PalmRadii, 4, Outline);
        Solid(vh, ring, edge, hop);
        for (int i = 0; i < 5; i++) if (drawn[i].stand >= .5f) Segments(vh, i, true, edge, edge, hop);
        // ...then their fills, the palm last so its top edge reads as the knuckles.
        for (int i = 0; i < 5; i++) if (drawn[i].stand >= .5f) Segments(vh, i, false, Color.Lerp(folded, fill, drawn[i].stand), shade, hop);
        Hull(ring, Palm, PalmRadii, 4, 0);
        Shrink(.55f);
        Shaded(vh, fill, shade, hop);
        // Folded fingers lie over the palm, each outlined on its own.
        for (int i = 0; i < 5; i++)
        {
            if (drawn[i].stand >= .5f) continue;
            Segments(vh, i, true, edge, edge, hop);
            Segments(vh, i, false, Color.Lerp(folded, fill, drawn[i].stand), foldedShade, hop);
        }
        // White tips mark the raised fingers, as in the camera overlay.
        for (int i = 0; i < 5; i++)
            if (drawn[i].mark > 0) Disc(vh, drawn[i].tip, Radii[i][2] * .55f * drawn[i].mark, Tone(Color.white, 1), hop);
    }

    // A finger's two segments, either grown into its outline or filled with a lighter centre.
    private void Segments(VertexHelper vh, int finger, bool outline, Color rim, Color centre, float hop)
    {
        var f = drawn[finger];
        var radii = Radii[finger];
        Segment(vh, f.root, radii[0], f.joint, radii[1], outline, rim, centre, hop);
        Segment(vh, f.joint, radii[1], f.tip, radii[2], outline, rim, centre, hop);
    }

    private void Segment(VertexHelper vh, Vector2 a, float ra, Vector2 b, float rb, bool outline, Color rim, Color centre, float hop)
    {
        pair[0] = a;
        pair[1] = (b - a).sqrMagnitude < 1e-6f ? a + new Vector2(0, .001f) : b;
        pairRadii[0] = ra;
        pairRadii[1] = rb;
        Hull(ring, pair, pairRadii, 2, outline ? Outline : 0);
        if (outline) { Solid(vh, ring, rim, hop); return; }
        Hull(core, pair, pairRadii, 2, -.6f * Mathf.Min(ra, rb));
        Shaded(vh, rim, centre, hop);
    }

    // The outline of the convex hull of circles listed counter-clockwise, each radius grown by grow (shrunk
    // when negative). The arcs are sampled at the same angles for any grow, so two rings of one shape pair
    // up point by point.
    private static void Hull(List<Vector2> points, Vector2[] centres, float[] radii, int count, float grow)
    {
        points.Clear();
        for (int i = 0; i < count; i++)
        {
            int previous = (i + count - 1) % count, next = (i + 1) % count;
            float start = Angle(Tangent(centres[previous], radii[previous], centres[i], radii[i]));
            float end = Angle(Tangent(centres[i], radii[i], centres[next], radii[next]));
            while (end < start) end += 2 * Mathf.PI;
            int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) / (Mathf.PI / 12)));
            float radius = Mathf.Max(0, radii[i] + grow);
            for (int k = 0; k <= steps; k++)
            {
                float angle = Mathf.Lerp(start, end, (float)k / steps);
                points.Add(centres[i] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }

    // Outward normal of the line touching circle a and then circle b, on the hull's right-hand side.
    private static Vector2 Tangent(Vector2 a, float ra, Vector2 b, float rb)
    {
        var along = b - a;
        float length = along.magnitude;
        along /= length;
        float sine = Mathf.Clamp((ra - rb) / length, -1, 1);
        return new Vector2(along.y, -along.x) * Mathf.Sqrt(1 - sine * sine) + along * sine;
    }

    private static float Angle(Vector2 direction) => Mathf.Atan2(direction.y, direction.x);

    // The core ring as a smaller copy of the ring about its middle: a palm-shaped soft highlight.
    private void Shrink(float scale)
    {
        var middle = Vector2.zero;
        foreach (var p in ring) middle += p / ring.Count;
        core.Clear();
        foreach (var p in ring) core.Add(middle + (p - middle) * scale);
    }

    private void Solid(VertexHelper vh, List<Vector2> points, Color tint, float hop)
    {
        int start = vh.currentVertCount;
        var middle = Vector2.zero;
        foreach (var p in points) middle += p / points.Count;
        vh.AddVert(Map(middle, hop), tint, Vector2.zero);
        foreach (var p in points) vh.AddVert(Map(p, hop), tint, Vector2.zero);
        for (int i = 0; i < points.Count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Count);
    }

    // The ring's rim colour eases into the lighter centre colour at the core ring: a soft inner shade.
    private void Shaded(VertexHelper vh, Color rim, Color centre, float hop)
    {
        int start = vh.currentVertCount, count = ring.Count;
        for (int i = 0; i < count; i++)
        {
            vh.AddVert(Map(ring[i], hop), rim, Vector2.zero);
            vh.AddVert(Map(core[i], hop), centre, Vector2.zero);
        }
        for (int i = 0; i < count; i++)
        {
            int a = start + i * 2, b = start + (i + 1) % count * 2;
            vh.AddTriangle(a, b, b + 1);
            vh.AddTriangle(a, b + 1, a + 1);
        }
        Solid(vh, core, centre, hop);
    }

    private void Disc(VertexHelper vh, Vector2 centre, float radius, Color tint, float hop)
    {
        int start = vh.currentVertCount;
        vh.AddVert(Map(centre, hop), tint, Vector2.zero);
        for (int k = 0; k <= 16; k++)
        {
            float angle = k * Mathf.PI / 8;
            vh.AddVert(Map(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, hop), tint, Vector2.zero);
            if (k > 0) vh.AddTriangle(start, start + k, start + k + 1);
        }
    }

    // Units to the rect: a centred square, scaled about the palm for the hop and mirrored for the other hand.
    private Vector2 Map(Vector2 p, float hop)
    {
        var point = HopCentre + (p - HopCentre) * hop;
        if (mirrored) point.x = 100 - point.x;
        return area.center + (point - new Vector2(50, 50)) * (Mathf.Min(area.width, area.height) / 100);
    }

    private Color Tone(Color towards, float amount)
    {
        var tone = Color.Lerp(color, towards, amount);
        tone.a = color.a;
        return tone;
    }
}
