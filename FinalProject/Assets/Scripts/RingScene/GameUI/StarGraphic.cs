using UnityEngine;
using UnityEngine.UI;

// Five-point star drawn as a mesh, so it does not depend on font glyphs. Maskable, so scrolling
// lists (the Progress page) clip it like their text.
[RequireComponent(typeof(CanvasRenderer))]
public class StarGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        // The points span 1.902 radii across and 1.809 top to bottom; fit and centre that shape.
        float outer = Mathf.Min(r.width / 1.902f, r.height / 1.809f);
        Vector2 centre = r.center + new Vector2(0, -0.0955f * outer);
        vh.AddVert(centre, color, Vector2.zero);
        for (int i = 0; i <= 10; i++)
        {
            float angle = Mathf.PI / 2 + i * Mathf.PI / 5;
            float radius = i % 2 == 0 ? outer : outer * 0.48f;
            vh.AddVert(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            if (i > 0) vh.AddTriangle(0, i, i + 1);
        }
    }
}
