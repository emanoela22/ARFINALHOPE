using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;

public partial class HandMusicController : MonoBehaviour
{
    private WebCamTexture webcam;
    private Texture2D upright;
    private Color32[] sourcePixels, uprightPixels;
    private Mediapipe.Unity.Experimental.TextureFrame frame;
    private HandLandmarker detector;
    private HandLandmarkerResult result;
    private RawImage preview;
    private AspectRatioFitter previewAspect;
    private TMP_Text status, holdLabel, countLabel;
    private Image progress;
    private RectTransform safe;
    private AudioSource audioSource;
    private AudioClip[] notes = new AudioClip[6];
    private Vector3[] landmarks = new Vector3[21];
    private readonly FingerNoteGate gate = new FingerNoteGate();
    private readonly GuidedHandSong song = new GuidedHandSong();
    private bool controlLeft = true, playing;
    private float openHeld;
    private float holdSeconds = 0.6f, nextFrame, lastSample;
    private int notesPlayed;
    private bool ready, paused, front;
    private long lastTimestamp;

    private IEnumerator Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        BuildUI();
        BuildAudio();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            bool answered = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => answered = true;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
            float deadline = Time.realtimeSinceStartup + 60;
            while (!answered && Time.realtimeSinceStartup < deadline) yield return null;
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        { status.text = "Camera access is needed. Allow it in phone Settings, then reopen Hand Music."; yield break; }
#else
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        { status.text = "Camera access was not granted. You can return to the menu."; yield break; }
#endif
        string device = null;
        foreach (var camera in WebCamTexture.devices)
            if (camera.isFrontFacing) { device = camera.name; front = true; break; }
#if UNITY_EDITOR || UNITY_STANDALONE
        if (device == null && WebCamTexture.devices.Length > 0) device = WebCamTexture.devices[0].name;
#endif
        if (device == null) { status.text = "No front camera found on this device."; yield break; }
        if (!StartCamera(device)) yield break;
        float timeout = Time.realtimeSinceStartup + 15;
        while (webcam.width <= 16 && Time.realtimeSinceStartup < timeout) yield return null;
        if (webcam.width <= 16) { status.text = "Camera did not start. Close other camera apps and try again."; yield break; }
        if (!CreateDetector()) yield break;
        status.text = "Show both hands. Open your control hand to play.";
        lastSample = Time.realtimeSinceStartup;
        ready = true;
    }

    private bool StartCamera(string device)
    {
        try { webcam = new WebCamTexture(device,640,480,15); webcam.Play(); return true; }
        catch (Exception e) { Fail("Could not open the camera",e); return false; }
    }
    private bool CreateDetector()
    {
        try
        {
            var model = Resources.Load<TextAsset>("HandMusic/hand_landmarker");
            if (model == null) throw new InvalidOperationException("Hand model missing");
            detector = HandLandmarker.CreateFromOptions(new HandLandmarkerOptions(
                new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer:model.bytes),
                runningMode:RunningMode.VIDEO, numHands:2, minHandDetectionConfidence:0.6f,
                minHandPresenceConfidence:0.6f, minTrackingConfidence:0.6f));
            result = HandLandmarkerResult.Alloc(2);
            return true;
        }
        catch (Exception e) { Fail("Hand tracking could not start",e); return false; }
    }

    private void Update()
    {
        var area = Screen.safeArea;
        safe.anchorMin = new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
        safe.anchorMax = new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
        if (ready && !paused && Time.realtimeSinceStartup-lastSample > .6f)
        { StopPlaying();ClearOverlay();status.text="Waiting for camera tracking…"; }
        if (!ready || paused || !webcam.isPlaying || !webcam.didUpdateThisFrame || Time.realtimeSinceStartup < nextFrame) return;
        nextFrame = Time.realtimeSinceStartup + 0.1f;
        try
        {
            UpdateUprightTexture();
            frame.ReadTextureOnCPU(upright, flipHorizontally:false, flipVertically:true);
            using var image = frame.BuildCPUImage();
            long timestamp = Math.Max(lastTimestamp+1,(long)(Time.realtimeSinceStartupAsDouble*1000));
            lastTimestamp = timestamp;
            bool detected = detector.TryDetectForVideo(image,timestamp,null,ref result);
            float elapsed = Mathf.Min(0.2f,Time.realtimeSinceStartup-lastSample);
            lastSample = Time.realtimeSinceStartup;
            ProcessHands(detected,elapsed);
        }
        catch (Exception e) { Fail("Hand tracking stopped. Return to the menu and try again",e); }
    }

    private void UpdateUprightTexture()
    {
        int w=webcam.width, h=webcam.height, rotation=webcam.videoRotationAngle;
        int ow=rotation%180==0?w:h, oh=rotation%180==0?h:w;
        if (upright == null || upright.width != ow || upright.height != oh)
        {
            frame?.Dispose();
            if (upright != null) Destroy(upright);
            upright = new Texture2D(ow,oh,TextureFormat.RGBA32,false);
            uprightPixels = new Color32[ow*oh];
            sourcePixels = new Color32[w*h];
            frame = new Mediapipe.Unity.Experimental.TextureFrame(ow,oh,TextureFormat.RGBA32);
            preview.texture = upright;
            preview.color = Color.white;
            previewAspect.aspectRatio = (float)ow/oh;
        }
        webcam.GetPixels32(sourcePixels);
        for (int y=0;y<h;y++) for (int x=0;x<w;x++)
        {
            int sy=webcam.videoVerticallyMirrored?h-1-y:y;
            int dx=x,dy=y;
            if (rotation==90) { dx=y;dy=w-1-x; }
            else if (rotation==180) { dx=w-1-x;dy=h-1-y; }
            else if (rotation==270) { dx=h-1-y;dy=x; }
            if (front) dx=ow-1-dx;
            uprightPixels[dy*ow+dx]=sourcePixels[sy*w+x];
        }
        upright.SetPixels32(uprightPixels);
        upright.Apply(false);
    }

    private void BuildAudio()
    {
        if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake=false;
        audioSource.spatialBlend=0;
        float[] frequencies={261.63f,293.66f,329.63f,349.23f,392f,440f};
        for(int n=0;n<6;n++)
        {
            const int rate=44100;
            var samples=new float[(int)(rate*.7f)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;
                float envelope=Mathf.Min(1,t/.025f)*Mathf.Exp(-5*t)*Mathf.Clamp01((.7f-t)/.06f);
                float phase=2*Mathf.PI*frequencies[n]*t;
                samples[i]=.45f*envelope*(Mathf.Sin(phase)+.2f*Mathf.Sin(phase*2));
            }
            notes[n]=AudioClip.Create("Hand Music " + n,samples.Length,1,rate,false);
            notes[n].SetData(samples,0);
        }
    }

    private void BuildUI()
    {
        if (FindFirstObjectByType<EventSystem>() == null) new GameObject("Event system",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var canvas=new GameObject("Hand Music UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform,false);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        canvas.AddComponent<Image>().color=ClinicalMenu.Paper;
        safe=Rect("Safe area",canvas.transform,Vector2.zero,Vector2.one);
        cameraBox=Rect("Camera panel",safe,new Vector2(.02f,.22f),new Vector2(.65f,.85f));
        ModernUI.Surface(cameraBox.gameObject.AddComponent<Image>(),new Color(.08f,.17f,.2f));
        var cameraImage=Rect("Camera",cameraBox,Vector2.zero,Vector2.one);
        preview=cameraImage.gameObject.AddComponent<RawImage>(); preview.raycastTarget=false; preview.color=Color.clear;
        previewAspect=cameraImage.gameObject.AddComponent<AspectRatioFitter>(); previewAspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        overlay=Rect("Tracked fingers",cameraImage,Vector2.zero,Vector2.one).gameObject.AddComponent<HandTrackingOverlay>();overlay.raycastTarget=false;
        BuildGuideUI();
        statusBox=Rect("Status panel",safe,new Vector2(.02f,.03f),new Vector2(.65f,.19f));
        ModernUI.Surface(statusBox.gameObject.AddComponent<Image>(),ClinicalMenu.Paper);
        status=Text(statusBox,"Starting front camera…",new Vector2(.02f,.22f),new Vector2(.98f,.97f),36);
        var track=Rect("Hold progress",statusBox,new Vector2(.02f,.04f),new Vector2(.98f,.14f));
        track.gameObject.AddComponent<Image>().color=new Color(.77f,.87f,.88f);
        progress=Rect("Progress",track,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
        ModernUI.Surface(progress,ClinicalMenu.Teal); progress.type=Image.Type.Filled; progress.fillMethod=Image.FillMethod.Horizontal; progress.fillAmount=0;
        BuildTopBar();
        BuildSongSelector();
    }
    private void ChangeHold(float delta) { holdSeconds=Mathf.Clamp(holdSeconds+delta,.3f,1.5f); holdLabel.text=$"Hold time: {holdSeconds:0.0}s"; gate.Reset(); }
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    private static TMP_Text Text(Transform parent,string text,Vector2 min,Vector2 max,int size)
    {
        var label=Rect(text,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=ModernUI.ReadableFont;label.text=text;label.fontSize=size;label.color=ClinicalMenu.Ink;
        label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;return label;
    }
    private static TMP_Text Button(Transform parent,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
    {
        var r=Rect(text,parent,min,max);ModernUI.Surface(r.gameObject.AddComponent<Image>(),ClinicalMenu.Teal);
        r.gameObject.AddComponent<Button>().onClick.AddListener(action);
        var label=Text(r,text,new Vector2(.04f,.04f),new Vector2(.96f,.96f),32);label.color=Color.white;label.alignment=TextAlignmentOptions.Center;
        return label;
    }
    private void Fail(string message,Exception e) { ready=false;StopPlaying();ClearOverlay();status.text=message+".";Debug.LogException(e);webcam?.Stop(); }
    private void OnApplicationPause(bool value)
    {
        paused=value;
        if (webcam==null || !ready) return;
        if (value) { webcam.Pause();StopPlaying();ClearOverlay(); }
        else { webcam.Play();lastSample=Time.realtimeSinceStartup; }
    }
    private void OnDestroy()
    {
        ready=false;webcam?.Stop();if(webcam!=null)Destroy(webcam);
        (detector as IDisposable)?.Dispose();frame?.Dispose();if(upright!=null)Destroy(upright);
        foreach(var clip in notes)if(clip!=null)Destroy(clip);
    }
}
