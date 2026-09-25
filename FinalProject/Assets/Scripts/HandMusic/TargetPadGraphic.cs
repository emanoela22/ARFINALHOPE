using UnityEngine;
using UnityEngine.UI;

// Reach target: a soft gold disc whose inner ring fills while the hand holds inside it.
[RequireComponent(typeof(CanvasRenderer))]
public class TargetPadGraphic : Graphic
{
    private float progress;
    public float Progress
    {
        get => progress;
        set
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, value)) return;
            progress = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float radius = Mathf.Min(r.width, r.height) * .5f;
        var fill = color;
        fill.a *= .3f;
        Arc(vh, r.center, 0, radius, 1, fill);
        Arc(vh, r.center, radius * .86f, radius, 1, color);
        if (progress > 0) Arc(vh, r.center, radius * .6f, radius * .78f, progress, Color.white);
    }

    // Ring from 12 o'clock, clockwise, over share of the circle; an inner radius of 0 draws a disc.
    private static void Arc(VertexHelper vh, Vector2 centre, float inner, float outer, float share, Color tint)
    {
        int steps = Mathf.Max(2, Mathf.CeilToInt(48 * share));
        int start = vh.currentVertCount;
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.PI / 2 - share * 2 * Mathf.PI * i / steps;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(centre + direction * inner, tint, Vector2.zero);
            vh.AddVert(centre + direction * outer, tint, Vector2.zero);
            if (i == 0) continue;
            int v = start + i * 2;
            vh.AddTriangle(v - 2, v - 1, v + 1);
            vh.AddTriangle(v - 2, v + 1, v);
        }
    }
}
