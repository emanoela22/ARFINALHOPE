using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class HandTrackingOverlay : Graphic
{
    public readonly Vector2[][] points={new Vector2[21],new Vector2[21]};
    public readonly bool[] visible=new bool[2];
    public readonly int[] masks=new int[2];
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r=rectTransform.rect;
        for(int hand=0;hand<2;hand++)
        {
            if(!visible[hand])continue;
            Color tint=hand==0 ? new Color(.1f,1f,.8f) : new Color(1f,.75f,.15f);
            for(int finger=0;finger<5;finger++)
            {
                int start=1+finger*4;
                Line(vh,At(r,hand,0),At(r,hand,start),5,tint);
                for(int j=0;j<3;j++)Line(vh,At(r,hand,start+j),At(r,hand,start+j+1),5,tint);
                bool extended=(masks[hand]&(1<<finger))!=0;
                Dot(vh,At(r,hand,start+3),extended?12:7,extended?Color.white:tint);
            }
            for(int j=0;j<21;j++)Dot(vh,At(r,hand,j),4,tint);
            Line(vh,At(r,hand,5),At(r,hand,9),5,tint);
            Line(vh,At(r,hand,9),At(r,hand,13),5,tint);
            Line(vh,At(r,hand,13),At(r,hand,17),5,tint);
        }
    }
    private Vector2 At(Rect r,int hand,int point)=>r.min+Vector2.Scale(points[hand][point],r.size);
    private static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
    {
        var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;int i=vh.currentVertCount;
        vh.AddVert(a-n,tint,Vector2.zero);vh.AddVert(a+n,tint,Vector2.zero);vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
    private static void Dot(VertexHelper vh,Vector2 p,float radius,Color tint)
    {
        int start=vh.currentVertCount;vh.AddVert(p,tint,Vector2.zero);
        for(int k=0;k<=12;k++)
        { float a=k*Mathf.PI/6;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);if(k>0)vh.AddTriangle(start,start+k,start+k+1); }
    }
}
