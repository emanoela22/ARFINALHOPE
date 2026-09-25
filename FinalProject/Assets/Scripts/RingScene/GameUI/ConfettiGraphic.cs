using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Paper confetti drawn as one mesh. Overlay canvases cannot show a ParticleSystem,
// and this needs no camera or artwork.
[RequireComponent(typeof(CanvasRenderer))]
public class ConfettiGraphic : Graphic
{
    private struct Piece
    {
        public Vector2 position, velocity;
        public float angle, spin, phase, age, life, size;
        public Color tint;
    }

    private const float Gravity = 900f, Drag = 2.5f;
    private readonly List<Piece> pieces = new();

    public int Count => pieces.Count;

    // Fans upwards from a point, e.g. behind the stars.
    public void Burst(Vector2 origin, int count, Color[] palette)
    {
        for (int i = 0; i < count; i++)
        {
            float direction = Random.Range(15f, 165f) * Mathf.Deg2Rad;
            Add(origin, new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)) * Random.Range(600f, 1700f),
                Random.Range(2.6f, 3.6f), palette);
        }
    }

    // Falls across the full width from just above the top edge.
    public void Rain(int count, Color[] palette)
    {
        var area = rectTransform.rect;
        for (int i = 0; i < count; i++)
            Add(new Vector2(Random.Range(area.xMin, area.xMax), area.yMax + Random.Range(20f, 400f)),
                new Vector2(Random.Range(-80f, 80f), -Random.Range(100f, 300f)), Random.Range(3.5f, 4.5f), palette);
    }

    private void Add(Vector2 position, Vector2 velocity, float life, Color[] palette)
    {
        pieces.Add(new Piece
        {
            position = position,
            velocity = velocity,
            angle = Random.Range(0f, 360f),
            spin = Random.Range(-400f, 400f),
            phase = Random.Range(0f, Mathf.PI * 2),
            life = life,
            size = Random.Range(16f, 28f),
            tint = palette[Random.Range(0, palette.Length)]
        });
        SetVerticesDirty();
    }

    public void Tick(float deltaTime)
    {
        if (pieces.Count == 0) return;
        // Drag caps the fall at Gravity / Drag, so pieces float down instead of dropping.
        float drag = Mathf.Exp(-Drag * deltaTime);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            piece.age += deltaTime;
            if (piece.age >= piece.life)
            {
                pieces.RemoveAt(i);
                continue;
            }
            piece.velocity = (piece.velocity + Vector2.down * Gravity * deltaTime) * drag;
            piece.position += (piece.velocity + Vector2.right * Mathf.Sin(piece.age * 5f + piece.phase) * 90f) * deltaTime;
            piece.angle += piece.spin * deltaTime;
            pieces[i] = piece;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var piece in pieces)
        {
            var tint = piece.tint * color;
            tint.a *= Mathf.Clamp01((piece.life - piece.age) / 0.6f);
            // Squashing one side reads as the paper tumbling over.
            var half = new Vector2(piece.size, piece.size * 0.6f * Mathf.Cos(piece.age * 7f + piece.phase)) * 0.5f;
            var rotation = Quaternion.Euler(0, 0, piece.angle);
            int start = vh.currentVertCount;
            vh.AddVert(piece.position + (Vector2)(rotation * new Vector2(-half.x, -half.y)), tint, Vector2.zero);
            vh.AddVert(piece.position + (Vector2)(rotation * new Vector2(-half.x, half.y)), tint, Vector2.zero);
            vh.AddVert(piece.position + (Vector2)(rotation * new Vector2(half.x, half.y)), tint, Vector2.zero);
            vh.AddVert(piece.position + (Vector2)(rotation * new Vector2(half.x, -half.y)), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
