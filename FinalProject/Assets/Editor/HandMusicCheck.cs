using System;
using UnityEngine;
using UnityEditor.SceneManagement;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;

public static class HandMusicCheck
{
    public static void Run()
    {
        CheckGate();
        CheckTwoHandMusic();
        CheckRelaxedFingers();
        var points = new Vector3[21];
        for (int finger=0;finger<4;finger++)
            for(int joint=0;joint<4;joint++)
                points[5+finger*4+joint]=new Vector3((finger-1.5f)*.02f,.04f+joint*.025f,0);
        points[4]=new Vector3(-.10f,.04f,0);
        Require(HandGesture.Classify(points)==MusicGesture.Open,"open hand");
        points[4]=points[8];
        Require(HandGesture.Classify(points)==MusicGesture.Pinch,"pinch");
        for(int finger=0;finger<4;finger++) points[8+finger*4]=new Vector3((finger-1.5f)*.02f,.025f,0);
        Require(HandGesture.Classify(points)==MusicGesture.Fist,"fist");
        for(int i=0;i<21;i++) points[i]=Quaternion.Euler(30,60,90)*points[i]*2;
        Require(HandGesture.Classify(points)==MusicGesture.Fist,"rotated/scaled fist");
        EditorSceneManager.OpenScene("Assets/Scenes/HandMusic.unity");
        Require(UnityEngine.Object.FindFirstObjectByType<HandMusicController>()!=null,"scene component");
        var model=Resources.Load<TextAsset>("HandMusic/hand_landmarker");
        Require(model!=null && model.bytes.Length>1000000,"bundled model");
        using(var detector=HandLandmarker.CreateFromOptions(new HandLandmarkerOptions(
            new BaseOptions(BaseOptions.Delegate.CPU,modelAssetBuffer:model.bytes),runningMode:RunningMode.VIDEO,numHands:2)))
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            texture.SetPixels32(new Color32[64*64]);texture.Apply();
            using(var image=new Mediapipe.Image(texture))
            {
                var result=detector.DetectForVideo(image,1);
                Require(result.handLandmarks==null || result.handLandmarks.Count==0,"blank frame has no hand");
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }
        Debug.Log("HAND_MUSIC_CHECK_PASS: gesture classification, note gating, scene, native model and blank-frame inference.");
        string[] args=Environment.GetCommandLineArgs();
        int preview=Array.IndexOf(args,"-handMusicPreview");
        if(preview>=0 && preview+1<args.Length) RenderPreview(args[preview+1]);
    }
    private static void RenderPreview(string path)
    {
        var controller=UnityEngine.Object.FindFirstObjectByType<HandMusicController>();
        typeof(HandMusicController).GetMethod("BuildUI",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(controller,null);
        typeof(HandMusicController).GetMethod("SelectSong",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(controller,new object[]{1});
        var selectedSong=(GuidedHandSong)typeof(HandMusicController).GetField("song",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(controller);
        foreach(int note in new[]{1,1,5,5}) selectedSong.Play(note);
        typeof(HandMusicController).GetMethod("RefreshSongGuide",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(controller,null);
        var canvas=GameObject.Find("Hand Music UI").GetComponent<Canvas>();
        var camera=new GameObject("Preview camera").AddComponent<Camera>();
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ClinicalMenu.Paper;
        var target=new RenderTexture(1920,1080,24);camera.targetTexture=target;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;
        Canvas.ForceUpdateCanvases();
        foreach(var label in canvas.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            label.ForceMeshUpdate();
            Require(!label.isTextOverflowing,"UI overflow: "+label.text);
        }
        camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
        typeof(HandMusicController).GetMethod("ToggleFullCamera",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(controller,null);
        Canvas.ForceUpdateCanvases();
        foreach(var label in canvas.GetComponentsInChildren<TMPro.TMP_Text>())
        { label.ForceMeshUpdate();Require(!label.isTextOverflowing,"full camera overflow: "+label.text); }
        var cameraPanel=canvas.transform.Find("Safe area/Camera panel").GetComponent<RectTransform>();
        Require(cameraPanel.anchorMin==Vector2.zero && cameraPanel.anchorMax==Vector2.one,"full camera anchors");
        camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.ChangeExtension(path,null)+"-full.png",image.EncodeToPNG());
        typeof(HandMusicController).GetMethod("ToggleFullCamera",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(controller,null);
        Require(cameraPanel.anchorMin!=Vector2.zero,"return to split camera");
        RenderTexture.active=null;camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("HAND_MUSIC_LAYOUT_PASS");
    }
    private static void CheckGate()
    {
        var gate=new GestureNoteGate();int count=0;
        for(int i=0;i<20;i++) if(gate.Sample(MusicGesture.Open,true,.1f,.5f)) count++;
        Require(count==1,"single note per hold");
        gate.Sample(MusicGesture.None,false,1,.5f);
        Require(!gate.Sample(MusicGesture.Open,true,1,.5f),"hand loss cannot repeat note");
        gate.Sample(MusicGesture.None,true,.31f,.5f);
        Require(gate.Sample(MusicGesture.Fist,true,.6f,.5f),"neutral rearms note");
        gate.Reset();gate.Sample(MusicGesture.Open,true,.3f,.5f);
        Require(!gate.Sample(MusicGesture.Fist,true,.3f,.5f),"changing gesture resets hold");
    }
    private static void CheckTwoHandMusic()
    {
        for(int count=1;count<=5;count++)
        {
            var p=new Vector3[21];
            for(int finger=0;finger<4;finger++)for(int j=0;j<4;j++)
                p[5+finger*4+j]=new Vector3((finger-1.5f)*.02f,.04f+j*.025f,0);
            for(int finger=count;finger<4;finger++)p[8+finger*4]=new Vector3((finger-1.5f)*.02f,.025f,0);
            p[2]=new Vector3(-.05f,.025f,0);p[3]=new Vector3(-.075f,.025f,0);
            p[4]=count==5?new Vector3(-.10f,.025f,0):new Vector3(0,.05f,0);
            Require(HandGesture.FingerCount(HandGesture.FingerMask(p))==count,"finger count "+count);
            for(int i=0;i<21;i++)p[i]=Quaternion.Euler(20,45,70)*p[i]*1.7f;
            Require(HandGesture.FingerCount(HandGesture.FingerMask(p))==count,"rotated count "+count);
        }
        var gate=new FingerNoteGate();
        Require(!gate.Sample(3,true,false,1,.5f),"closed control blocks note");
        Require(gate.Sample(3,true,true,.6f,.5f),"open control plays note");
        Require(!gate.Sample(3,true,true,1,.5f),"held count does not repeat");
        gate.Sample(0,false,true,1,.5f);
        Require(!gate.Sample(3,true,true,1,.5f),"missing note hand does not rearm");
        Require(gate.Sample(2,true,true,.6f,.5f),"different count plays next note");
        gate.Sample(0,true,true,.3f,.5f);
        Require(gate.Sample(2,true,true,.6f,.5f),"lower fingers rearm repeated note");
        var song=new GuidedHandSong();
        Require(!song.Play(1) && song.Position==0,"wrong note waits");
        int[] tune={3,2,1,3,2,1,1,1,1,1,2,2,2,2,3,2,1};
        foreach(int note in tune)Require(song.Play(note),"song note");
        Require(song.Complete && !song.Play(1),"song completes once");
        song.Reset();Require(song.Next==3 && song.Position==0,"song restart");
        song.Select(1);
        Require(song.Position==0 && song.Next==1 && song.Length==42,"select Twinkle");
        int[] twinkle={1,1,5,5,6,6,5,4,4,3,3,2,2,1,5,5,4,4,3,3,2,5,5,4,4,3,3,2,1,1,5,5,6,6,5,4,4,3,3,2,2,1};
        foreach(int expected in twinkle) { Require(song.Next==expected,"Twinkle next note");Require(song.Play(expected),"Twinkle progress"); }
        Require(song.Complete,"Twinkle completes");
        song.Select(0);Require(song.Next==3 && song.Position==0,"switch back resets progress");
        gate.Reset();Require(gate.Sample(6,true,true,.6f,.5f),"A note accepted");
        Require(!gate.Sample(6,true,true,1,.5f),"held A does not skip repeated notes");
        Debug.Log("TWO_HAND_MUSIC_CHECK_PASS");
    }
    private static void CheckRelaxedFingers()
    {
        for(int count=3;count<=5;count++)
        {
            var p=new Vector3[21];
            for(int f=0;f<4;f++)
            {
                int j=5+f*4;
                p[j]=new Vector3((f-1.5f)*.02f,.04f,0);
                p[j+1]=p[j]+new Vector3(0,.025f,0);
                p[j+2]=p[j+1]+new Vector3(0,.015f,.015f);
                p[j+3]=p[j+1]+new Vector3(0,.03f,.03f);
                if(f>=count)p[j+3]=p[j]-new Vector3(0,.015f,0);
            }
            p[2]=new Vector3(-.05f,.025f,0);p[3]=new Vector3(-.075f,.025f,0);
            p[4]=count==5?new Vector3(-.10f,.025f,0):new Vector3(0,.05f,0);
            Require(HandGesture.FingerCount(HandGesture.FingerMask(p))==count,"relaxed count "+count);
            Require(HandGesture.Classify(p)==MusicGesture.Open,"relaxed control opens "+count);
            for(int i=0;i<21;i++)p[i]=Quaternion.Euler(55,70,110)*p[i]*1.8f+Vector3.one;
            Require(HandGesture.FingerCount(HandGesture.FingerMask(p))==count,"rotated relaxed count "+count);
        }
        var filter=new FingerMaskFilter();
        Require(filter.Sample(31)==31,"filter starts with visible fingers");
        Require(filter.Sample(3)==31,"single bad frame ignored");
        Require(filter.Sample(31)==31,"count recovers without flicker");
        filter.Sample(0);
        Require(filter.Sample(0)==0,"sustained closed fingers recognized");
        filter.Reset();Require(filter.Sample(3)==3,"filter resets after loss or swap");
        Debug.Log("RELAXED_FINGER_CHECK_PASS");
    }
    private static void Require(bool ok,string name) { if(!ok)throw new Exception("Hand Music check failed: "+name); }
}
