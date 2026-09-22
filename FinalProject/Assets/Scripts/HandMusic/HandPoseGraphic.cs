using UnityEngine;
using UnityEngine.UI;

// Simple, scalable hand diagrams. No font-dependent emoji or external artwork.
[RequireComponent(typeof(CanvasRenderer))]
public class HandPoseGraphic : Graphic
{
    public MusicGesture gesture;
    public int fingerCount = -1;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 wrist = new Vector2(0.5f, 0.08f);
        Vector2[] bases = { new Vector2(.23f,.38f), new Vector2(.36f,.55f), new Vector2(.5f,.58f), new Vector2(.64f,.55f), new Vector2(.76f,.46f) };
        Vector2[] tips = { new Vector2(.08f,.63f), new Vector2(.32f,.91f), new Vector2(.5f,.98f), new Vector2(.67f,.90f), new Vector2(.84f,.74f) };
        for (int i = 0; i < 5; i++)
        {
            Vector2 tip = gesture == MusicGesture.Fist ? bases[i] + new Vector2(0,-.05f) : tips[i];
            if(fingerCount>=0 && !(i==0 ? fingerCount==5 : i<=fingerCount)) tip=bases[i]+new Vector2(0,-.05f);
            if (gesture == MusicGesture.Pinch && i < 2) tip = new Vector2(.2f,.7f);
            Vector2 knuckle = Vector2.Lerp(bases[i], tips[i], gesture == MusicGesture.Fist ? .38f : .5f);
            Line(vh, wrist, bases[i]);
            Line(vh, bases[i], knuckle);
            Line(vh, knuckle, tip);
            if (i > 0) Line(vh, bases[i-1], bases[i]);
        }
    }
    private void Line(VertexHelper vh, Vector2 a, Vector2 b)
    {
        var r = rectTransform.rect;
        a = r.min + Vector2.Scale(a,r.size); b = r.min + Vector2.Scale(b,r.size);
        Vector2 d = (b-a).normalized;
        Vector2 n = new Vector2(-d.y,d.x) * 3.5f;
        int start = vh.currentVertCount;
        vh.AddVert(a-n,color,Vector2.zero); vh.AddVert(a+n,color,Vector2.zero);
        vh.AddVert(b+n,color,Vector2.zero); vh.AddVert(b-n,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
}
