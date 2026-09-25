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
    private TMP_Text status, holdLabel, countLabel, timeLabel;
    private Image progress;
    private RectTransform safe;
    private AudioSource audioSource;
    private AudioClip[] notes = new AudioClip[6];
    private Vector3[] landmarks = new Vector3[21];
    private readonly GuidedHandSong song = new GuidedHandSong();
    private float nextFrame, lastSample;
    private bool ready, paused, front;
    private long lastTimestamp;
    private int previousFrameRate = -1;
    // The trained (neglected) side comes from Settings; it decides the playing hand, cues and layout.
    private static bool TrainLeft => GameSettings.trainLeftSide;
    private static string Side => GameSettings.TrainedSide;

    private IEnumerator Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        // The preview, cues and tracked hand move at 60 fps; the camera itself delivers 30.
        previousFrameRate = Application.targetFrameRate;
        Application.targetFrameRate = 60;
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
        status.text = $"Find the gold line on your {Side}, then show your {Side} hand.";
        lastSample = Time.realtimeSinceStartup;
        ready = true;
    }

    private bool StartCamera(string device)
    {
        try { webcam = new WebCamTexture(device,640,480,30); webcam.Play(); return true; }
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
        LayoutPreview();
        bool tracking = ready && !paused && Time.realtimeSinceStartup-lastSample <= .6f;
        TickSession(Time.unscaledDeltaTime, tracking);
        TickCues(Time.unscaledDeltaTime);
        if (sessionOver) return;
        if (ready && !paused && !tracking)
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

    // The preview shows the camera's own texture, so it updates at the camera's rate.
    private void LayoutPreview()
    {
        if (webcam == null || webcam.width <= 16) return;
        if (preview.texture != webcam) { preview.texture = webcam; preview.color = Color.white; }
        int rotation = webcam.videoRotationAngle;
        previewAspect.aspectRatio = rotation%180==0 ? (float)webcam.width/webcam.height : (float)webcam.height/webcam.width;
        FitPreview(preview, cameraImage.rect.size, rotation, webcam.videoVerticallyMirrored, front);
    }

    // Shows the raw camera image exactly like the upright image the hands are tracked on (see
    // UpdateUprightTexture), so the tracked fingers line up with it. Flips go in the UVs, in the camera
    // image's own axes; the turn rotates the preview, which keeps the camera image's shape so it fills
    // the box once turned. Mirroring the image reverses the direction of the turn.
    private static void FitPreview(RawImage image, Vector2 box, int rotation, bool verticallyMirrored, bool mirrored)
    {
        image.uvRect = new Rect(mirrored?1:0, verticallyMirrored?1:0, mirrored?-1:1, verticallyMirrored?-1:1);
        var rect = image.rectTransform;
        var size = rotation%180==0 ? box : new Vector2(box.y,box.x);
        if (rect.sizeDelta != size) { rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = size; }
        var turn = Quaternion.Euler(0,0,mirrored?rotation:-rotation);
        if (rect.localRotation != turn) rect.localRotation = turn;
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

    // Notes are panned toward where they are played, so sound also draws attention to that side.
    private void PlayNote(int note, float pan, float volume = .65f)
    {
        if (note < 1 || note > notes.Length) return;
        audioSource.Stop();
        audioSource.panStereo = Mathf.Clamp(pan, -1, 1);
        audioSource.PlayOneShot(notes[note-1], volume);
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
        // The camera (where the hands play) sits on the trained side; guidance stays on the other side.
        cameraBox=Panel("Camera panel",safe,new Vector2(.02f,.03f),new Vector2(.645f,.865f));
        ModernUI.Surface(cameraBox.gameObject.AddComponent<Image>(),new Color(.08f,.17f,.2f));
        cameraImage=Rect("Camera",cameraBox,Vector2.zero,Vector2.one);
        previewAspect=cameraImage.gameObject.AddComponent<AspectRatioFitter>(); previewAspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        previewAspect.aspectRatio=4f/3f;
        // The live image is its own child, so turning it (FitPreview) leaves the cues and tracked hands upright.
        preview=Rect("Camera feed",cameraImage,Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>(); preview.raycastTarget=false; preview.color=Color.clear;
        BuildCues();
        overlay=Rect("Tracked fingers",cameraImage,Vector2.zero,Vector2.one).gameObject.AddComponent<HandTrackingOverlay>();overlay.raycastTarget=false;
        BuildCountBadge();
        BuildGuideUI();
        BuildTopBar();
        BuildSongSelector();
        RefreshCounters();
    }
    private void ChangeHold(float delta)
    {
        GameSettings.handMusicHold=Mathf.Clamp(Mathf.Round((GameSettings.handMusicHold+delta)*10)/10,.3f,1.5f);
        GameSettings.Save();
        holdLabel.text=$"Hold: {GameSettings.handMusicHold:0.0}s"; dwell.Reset();
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    // Layout anchors are written for left-side training and mirrored for the right.
    private static RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var r=Rect(name,parent,min,max); Place(r,min,max); return r;
    }
    private static void Place(RectTransform r,Vector2 min,Vector2 max)
    {
        if (!TrainLeft) (min.x,max.x)=(1-max.x,1-min.x);
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
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
        if (value) SaveProgress(false);
        if (webcam==null || !ready) return;
        if (value) { webcam.Pause();StopPlaying();ClearOverlay(); }
        else if (!sessionOver) { webcam.Play();lastSample=Time.realtimeSinceStartup; }
    }
    private void OnDestroy()
    {
        Application.targetFrameRate=previousFrameRate;
        ready=false;webcam?.Stop();if(webcam!=null)Destroy(webcam);
        (detector as IDisposable)?.Dispose();frame?.Dispose();if(upright!=null)Destroy(upright);
        foreach(var clip in notes)if(clip!=null)Destroy(clip);
    }
}
