using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;

// Unity -batchmode -quit -projectPath <project> -executeMethod HandMusicCheck.Run [-handMusicPreview <folder>]
public static class HandMusicCheck
{
    private static readonly HandMusicMode[] Modes = { HandMusicMode.Fingers, HandMusicMode.OpenHand };

    public static void Run()
    {
        CheckGate();
        CheckFingersAndSongs();
        CheckRelaxedFingers();
        CheckTrainingLogic();
        CheckPreviewOrientation();
        CheckHandPoses();
        var points = new Vector3[21];
        for (int finger=0;finger<4;finger++)
            for(int joint=0;joint<4;joint++)
                points[5+finger*4+joint]=new Vector3((finger-1.5f)*.02f,.04f+joint*.025f,0);
        points[4]=new Vector3(-.10f,.04f,0);
        Require(HandGesture.Classify(points)==MusicGesture.Open,"open hand");
        Require(HandGesture.Openness(points)>.95f,"open hand measures open");
        points[4]=points[8];
        Require(HandGesture.Classify(points)==MusicGesture.Pinch,"pinch");
        for(int finger=0;finger<4;finger++) points[8+finger*4]=new Vector3((finger-1.5f)*.02f,.025f,0);
        Require(HandGesture.Classify(points)==MusicGesture.Fist,"fist");
        Require(HandGesture.Openness(points)<.5f,"fist measures closed");
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
        CheckTools.KeepSavedData(CheckPlayModes);
        CheckTools.KeepSavedData(() => CheckLayout(CheckTools.Argument("-handMusicPreview")));
        RenderHandPoses(CheckTools.Argument("-handMusicPreview"));
        Debug.Log("HAND_MUSIC_LAYOUT_PASS: both training sides, both modes.");
    }

    private static void CheckTrainingLogic()
    {
        // On the phone's mirrored front camera MediaPipe calls the left hand "Right" (seen on a device).
        Require(HandGesture.IsLeftHand("Right", true) && !HandGesture.IsLeftHand("Left", true) &&
            HandGesture.IsLeftHand("Left", false) && !HandGesture.IsLeftHand("Right", false), "hand labels match the patient's hands");
        const float aspect = 4f / 3f;
        foreach (float size in new[] { .5f, GameSettings.DefaultHandMusicCircle, 1 })
            foreach (bool left in new[] { true, false })
            {
                float radius = ReachTargets.MaxRadius * size;
                var targets = new ReachTargets();
                for (int i = 0; i < 12; i++)
                {
                    var point = targets.Next(left, radius, aspect);
                    float near = left ? point.x + radius / aspect : 1 - (point.x - radius / aspect);
                    float far = left ? point.x - radius / aspect : 1 - (point.x + radius / aspect);
                    Require(near <= .5f + 1e-4f && far >= 0, $"whole circle on the trained half (left={left}, size={size}, x={point.x})");
                    targets.Register(2);
                }
                Require(Mathf.Approximately(targets.Distance, 1) && Mathf.Approximately(targets.Farthest, 1), "quick reaches move circles to the edge");
            }
        var slow = new ReachTargets();
        slow.Register(12);
        Require(Mathf.Approximately(slow.Distance, .2f), "a long reach brings circles closer");
        slow.Register(3); slow.Register(5); slow.Register(3);
        Require(Mathf.Approximately(slow.Distance, .2f), "a slow reach breaks the quick streak");
        Require(ReachTargets.Contains(new Vector2(.3f, .5f), new Vector2(.38f, .5f), aspect, .12f) &&
            !ReachTargets.Contains(new Vector2(.3f, .5f), new Vector2(.38f, .5f), aspect, .096f) &&
            !ReachTargets.Contains(new Vector2(.3f, .5f), new Vector2(.4f, .5f), aspect, .12f), "the circle's size decides its round hit area");

        var dwell = new DwellGate();
        int fired = 0;
        for (int i = 0; i < 20; i++) if (dwell.Sample(true, .1f, .55f)) fired++;
        Require(fired == 3, "holding inside plays once per hold: " + fired);
        dwell.Reset();
        for (int i = 0; i < 4; i++) dwell.Sample(true, .1f, 1);
        dwell.Sample(false, .1f, 1); dwell.Sample(false, .1f, 1);
        Require(Mathf.Approximately(dwell.Progress(1), .4f), "a brief tracking drop keeps the hold");
        for (int i = 0; i < 3; i++) dwell.Sample(false, .1f, 1);
        Require(dwell.Progress(1) == 0, "leaving the circle resets the hold");

        var open = new OpenHandGate();
        open.Calibrate(.5f, .9f);
        Require(!open.Sample(.95f, true, 1, .5f), "the hand has to close before the first note");
        open.Sample(.55f, true, .1f, .5f);
        Require(open.Armed, "closing arms the next note");
        Require(!open.Sample(.8f, true, .3f, .5f) && open.Sample(.8f, true, .3f, .5f), "opening past the threshold plays after the hold");
        Require(!open.Sample(.8f, true, 1, .5f), "staying open does not repeat");
        open.Sample(.55f, false, .1f, .5f);
        Require(!open.Armed, "a lost hand does not re-arm");
        open.Calibrate(.6f, .61f);
        Require(Mathf.Approximately(open.OpenAt, .6f + .08f * .6f), "a tiny range is widened against tracking noise");

        var calibration = new OpennessCalibration();
        float[] closed = { .45f, .45f, .9f, .45f, .45f, .44f, .46f, .45f };
        foreach (float value in closed) { calibration.Sample(value, true, .25f); calibration.Sample(.1f, false, .25f); }
        Require(calibration.Step == 1, "closed step needs two seconds of visible hand");
        for (int i = 0; i < 8; i++) calibration.Sample(.6f + i * .05f, true, .25f);
        Require(calibration.Done && Mathf.Approximately(calibration.ClosedLevel, .45f) && Mathf.Approximately(calibration.OpenLevel, .9f),
            $"calibrated levels {calibration.ClosedLevel} / {calibration.OpenLevel}");
    }

    // The live preview turns and mirrors the raw camera image in the UI. For every camera rotation and
    // flip, each camera pixel must show where the upright image the hands are tracked on has it.
    private static void CheckPreviewOrientation()
    {
        const int w = 8, h = 6;
        var fit = typeof(HandMusicController).GetMethod("FitPreview", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (int rotation in new[] { 0, 90, 180, 270 })
            foreach (bool flipped in new[] { false, true })
                foreach (bool mirrored in new[] { false, true })
                {
                    bool turned = rotation % 180 != 0;
                    int ow = turned ? h : w, oh = turned ? w : h;
                    var box = new GameObject("Camera", typeof(RectTransform)).GetComponent<RectTransform>();
                    box.sizeDelta = new Vector2(ow, oh);
                    var image = new GameObject("Camera feed", typeof(RectTransform)).AddComponent<RawImage>();
                    image.rectTransform.SetParent(box, false);
                    fit.Invoke(null, new object[] { image, box.rect.size, rotation, flipped, mirrored });
                    var feed = image.rectTransform;
                    var uvs = image.uvRect;
                    for (int row = 0; row < h; row++)
                        for (int x = 0; x < w; x++)
                        {
                            var uv = new Vector2((x + .5f) / w, (row + .5f) / h);
                            var drawn = Rect.NormalizedToPoint(feed.rect, new Vector2((uv.x - uvs.x) / uvs.width, (uv.y - uvs.y) / uvs.height));
                            Vector2 shown = box.InverseTransformPoint(feed.TransformPoint(drawn));
                            var upright = Upright(x, row, w, h, rotation, flipped, mirrored);
                            Require(Vector2.Distance(shown, new Vector2(upright.x + .5f - ow / 2f, upright.y + .5f - oh / 2f)) < .01f,
                                $"preview matches the tracked image (rotation {rotation}, flipped {flipped}, mirrored {mirrored})");
                        }
                    UnityEngine.Object.DestroyImmediate(box.gameObject);
                }
    }

    private static readonly (MusicGesture gesture, int count)[] Poses = {
        (MusicGesture.Open, 0), (MusicGesture.Open, 1), (MusicGesture.Open, 2), (MusicGesture.Open, 3),
        (MusicGesture.Open, 4), (MusicGesture.Open, 5), (MusicGesture.Open, -1), (MusicGesture.Pinch, -1) };

    // The guide hand raises the same fingers as before its redesign, and mirroring flips its drawing exactly.
    private static void CheckHandPoses()
    {
        foreach (MusicGesture gesture in Enum.GetValues(typeof(MusicGesture)))
            for (int count = -1; count <= 6; count++)
                for (int finger = 0; finger < 5; finger++)
                {
                    bool before = gesture == MusicGesture.Pinch ? finger == 2
                        : gesture == MusicGesture.Open && (count < 0 || (finger == 0 ? count == 5 : finger <= count));
                    Require(HandPoseGraphic.Raised(gesture, count, finger) == before, $"hand picture raises finger {finger} for {gesture} {count}");
                }
        var hand = new GameObject("Hand", typeof(RectTransform)).AddComponent<HandPoseGraphic>();
        hand.rectTransform.sizeDelta = new Vector2(200, 220);
        var populate = typeof(HandPoseGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
        foreach (var (gesture, count) in Poses)
        {
            (hand.gesture, hand.fingerCount) = (gesture, count);
            var shapes = new Vector3[2][];
            for (int side = 0; side < 2; side++)
            {
                hand.mirrored = side == 1;
                using var mesh = new VertexHelper();
                populate.Invoke(hand, new object[] { mesh });
                shapes[side] = new Vector3[mesh.currentVertCount];
                var vertex = new UIVertex();
                for (int i = 0; i < mesh.currentVertCount; i++) { mesh.PopulateUIVertex(ref vertex, i); shapes[side][i] = vertex.position; }
            }
            Require(shapes[0].Length > 0 && shapes[0].Length == shapes[1].Length &&
                shapes[0].Zip(shapes[1], (a, b) => Mathf.Abs(a.x + b.x) + Mathf.Abs(a.y - b.y)).Max() < 1e-3f, $"mirrored hand picture for {gesture} {count}");
        }
        UnityEngine.Object.DestroyImmediate(hand.gameObject);
    }

    // For review: the guide hand in every pose, both ways round, and a change from one finger to three part-way through.
    private static void RenderHandPoses(string folder)
    {
        if (folder == null) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var canvas = new GameObject("Hand poses", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        CheckTools.UseWorldSpace(canvas, new Vector2(1920, 1080));
        var paper = new GameObject("Paper", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        paper.rectTransform.SetParent(canvas.transform, false);
        paper.rectTransform.anchorMin = Vector2.zero;
        paper.rectTransform.anchorMax = Vector2.one;
        paper.rectTransform.sizeDelta = Vector2.zero;
        for (int i = 0; i < 16; i++) PoseHand(canvas.transform, new Vector2(480 * (i % 4) - 720, 405 - 270 * (i / 4)), Poses[i % 8], i >= 8);
        Render(folder, "hand-poses");
        foreach (var shown in canvas.GetComponentsInChildren<HandPoseGraphic>()) UnityEngine.Object.DestroyImmediate(shown.gameObject);
        var changedAt = typeof(HandPoseGraphic).GetField("changedAt", BindingFlags.NonPublic | BindingFlags.Instance);
        float[] moments = { 0, .06f, .12f, .18f, .24f, .3f, .4f, .6f };
        for (int i = 0; i < moments.Length; i++)
        {
            var hand = PoseHand(canvas.transform, new Vector2(240 * i - 840, 0), (MusicGesture.Open, 1), false);
            hand.fingerCount = 3;
            hand.SetVerticesDirty();
            Canvas.ForceUpdateCanvases();
            changedAt.SetValue(hand, Time.unscaledTime - moments[i]);
            hand.SetVerticesDirty();
        }
        Render(folder, "hand-rise");
    }

    private static HandPoseGraphic PoseHand(Transform parent, Vector2 position, (MusicGesture gesture, int count) pose, bool mirrored)
    {
        var hand = new GameObject("Hand", typeof(RectTransform)).AddComponent<HandPoseGraphic>();
        hand.rectTransform.SetParent(parent, false);
        hand.rectTransform.sizeDelta = new Vector2(230, 250);
        hand.rectTransform.anchoredPosition = position;
        hand.color = ClinicalMenu.Teal;
        (hand.gesture, hand.fingerCount, hand.mirrored) = (pose.gesture, pose.count, mirrored);
        Canvas.ForceUpdateCanvases();
        return hand;
    }

    // Where camera pixel (x, row) lands in the upright image the hands are tracked on: flipped if the camera
    // says so, turned by its rotation, then mirrored like a selfie (HandMusicController.UpdateUprightTexture).
    private static Vector2Int Upright(int x, int row, int w, int h, int rotation, bool flipped, bool mirrored)
    {
        int y = flipped ? h - 1 - row : row, ow = rotation % 180 == 0 ? w : h;
        int dx = x, dy = y;
        if (rotation == 90) { dx = y; dy = w - 1 - x; }
        else if (rotation == 180) { dx = w - 1 - x; dy = h - 1 - y; }
        else if (rotation == 270) { dx = h - 1 - y; dy = x; }
        return new Vector2Int(mirrored ? ow - 1 - dx : dx, dy);
    }

    // Drives the real controller with synthetic hands, for both training sides and every mode.
    private static void CheckPlayModes()
    {
        foreach (bool left in new[] { true, false })
        {
            GameSettings.trainLeftSide = left;
            GameSettings.handMusicHold = .5f;
            GameSettings.handMusicDuration = 60;
            ProgressStore.ResetStatistics();
            var trained = left ? new Vector2(.3f, .45f) : new Vector2(.7f, .45f);
            var attended = left ? new Vector2(.75f, .4f) : new Vector2(.25f, .4f);
            string side = left ? "left" : "right";
            foreach (var mode in Modes)
            {
                GameSettings.handMusicMode = mode;
                GameSettings.handMusicCircle = GameSettings.DefaultHandMusicCircle;
                var c = Build();
                string name = $"{mode} ({side})";
                if (mode == HandMusicMode.OpenHand)
                {
                    for (int i = 0; i < 8; i++) Step(c, Hand(trained, .45f), default, .25f);
                    for (int i = 0; i < 8; i++) Step(c, Hand(trained, .9f), default, .25f);
                    Require(Get<OpennessCalibration>(c, "calibration").Done && !Get<bool>(c, "sessionRunning"), name + ": calibration before the timer");
                    Step(c, Hand(trained, .45f), default);
                    for (int i = 0; i < 7; i++) Step(c, Hand(trained, .9f), default);
                    Require(Get<int>(c, "correctNotes") == 1, name + ": close then open plays a note");
                    Step(c, Hand(trained, .45f), default);
                    for (int i = 0; i < 7; i++) Step(c, Hand(attended, .9f), default);
                    Require(Get<int>(c, "correctNotes") == 1 && Status(c).Contains("gold side"), name + ": only on the trained side");
                }
                else
                {
                    Step(c, Hand(trained), default);
                    Require(Get<bool>(c, "sessionRunning"), name + ": the timer starts when the trained hand appears");
                    Call(c, "TickCues", 1f);
                    var target = Get<Vector2>(c, "targetPoint");
                    Require(left ? target.x < .5f : target.x > .5f, name + ": circle on the trained side");
                    int next = Get<GuidedHandSong>(c, "song").Next;
                    Require(Get<TMPro.TMP_Text>(c, "targetLabel").text == next.ToString(), name + ": the circle shows the finger count");
                    for (int i = 0; i < 7; i++) Step(c, Hand(target), default);
                    for (int i = 0; i < 7; i++) Step(c, Hand(target, fingers: next == 1 ? 2 : 1), default);
                    string wanted = next == 1 ? "Show 1 finger." : $"Show {next} fingers.";
                    Require(Get<int>(c, "correctNotes") == 0 && Status(c).EndsWith(wanted), name + ": no or wrong fingers do not play: " + Status(c));
                    for (int i = 0; i < 7; i++) Step(c, Hand(trained + new Vector2(0, .3f)), Hand(target, fingers: next));
                    Require(Get<int>(c, "correctNotes") == 0 && Status(c).Contains($"Use your {side} hand"), name + ": the other hand does not play");
                    for (int i = 0; i < 7; i++) Step(c, Hand(target, fingers: next), default);
                    Require(Get<int>(c, "correctNotes") == 1 && Get<GuidedHandSong>(c, "song").Position == 1, name + ": holding in the circle plays the next note");
                    Require(Get<float>(c, "sweepAge") >= 0, name + ": the next circle is on its way");
                    // The circle can be resized during play: down to half, up to the original size.
                    var circle = Get<RectTransform>(c, "target");
                    float before = circle.anchorMax.y - circle.anchorMin.y;
                    Call(c, "ChangeCircle", -.1f);
                    Require(Mathf.Approximately(GameSettings.handMusicCircle, .7f) && circle.anchorMax.y - circle.anchorMin.y < before - 1e-4f,
                        name + ": Smaller shrinks the circle");
                    for (int i = 0; i < 6; i++) Call(c, "ChangeCircle", .1f);
                    Require(Mathf.Approximately(GameSettings.handMusicCircle, 1) &&
                        Mathf.Approximately(circle.anchorMax.y - circle.anchorMin.y, 2 * ReachTargets.MaxRadius), name + ": the largest circle is the original size");
                }
                // The session clock runs to the end and opens the results with stars.
                Call(c, "TickSession", 30f, true);
                Require(Get<TMPro.TMP_Text>(c, "timeLabel").text == "Time 0:30", name + ": countdown");
                Call(c, "TickSession", 31f, true);
                var screen = UnityEngine.Object.FindObjectsByType<SessionResultsScreen>(FindObjectsSortMode.None);
                Require(screen.Length == 1, name + ": results screen at the end");
                var saved = ProgressStore.LoadHistory().Last();
                Require(saved.IsHandMusic && saved.completed && saved.score == 1 && saved.stars >= 1 &&
                    saved.mode == GameSettings.HandMusicModeNames[(int)mode] && saved.neglectedSide == (left ? "Left" : "Right") &&
                    (mode == HandMusicMode.OpenHand || saved.reach > 0), name + ": session saved");
                UnityEngine.Object.DestroyImmediate(screen[0].gameObject);
            }
        }
    }

    // Renders a playing moment for every side and mode, and checks the layout mirrors with the side.
    private static void CheckLayout(string folder)
    {
        foreach (bool left in new[] { true, false })
            foreach (var mode in Modes)
            {
                GameSettings.trainLeftSide = left;
                GameSettings.handMusicMode = mode;
                GameSettings.handMusicHold = .5f;
                GameSettings.handMusicCircle = GameSettings.DefaultHandMusicCircle;
                var c = Build();
                var canvas = c.GetComponentInChildren<Canvas>();
                CheckTools.UseWorldSpace(canvas, new Vector2(1920, 1080));
                string name = $"handmusic-{(left ? "left" : "right")}-{mode.ToString().ToLower()}";
                Pose(c, left, mode);
                // Each posed moment is 40% through its hold; a Filled image without a sprite would show it full.
                var progress = Get<Image>(c, "progress");
                Require(progress.sprite != null && Mathf.Abs(progress.fillAmount - .4f) < .05f, name + ": hold progress shows " + progress.fillAmount);
                var safe = canvas.transform.Find("Safe area");
                var camera = (RectTransform)safe.Find("Camera panel");
                var guide = (RectTransform)safe.Find("Song guidance");
                var line = (RectTransform)safe.Find("Camera panel/Camera/Gold line");
                Require(left ? camera.anchorMax.x < .66f && guide.anchorMin.x > .5f && line.anchorMax.x < .02f
                    : camera.anchorMin.x > .34f && guide.anchorMax.x < .5f && line.anchorMin.x > .98f,
                    name + ": play area and gold line on the trained side, guidance on the other");
                // What the patient reads or taps during play stays on the side they notice.
                foreach (string item in new[] { "Toolbar/End session", "Toolbar/Notes: 0", "Toolbar/Time" })
                {
                    var rect = (RectTransform)safe.Find(item);
                    Require(left ? rect.anchorMin.x > .5f : rect.anchorMax.x < .5f, name + ": " + item + " on the side that is not neglected");
                }
                Require(Get<HandPoseGraphic>(c, "nextPose").mirrored == left, name + ": the hand picture shows the trained hand");
                Require(safe.Find("Song guidance/Circle size").gameObject.activeSelf == (mode != HandMusicMode.OpenHand),
                    name + ": circle size controls only where there is a circle");
                var badge = safe.Find("Camera panel/Camera/Finger count");
                var hands = safe.Find("Camera panel/Camera/Tracked fingers");
                Require(badge.gameObject.activeSelf == (mode == HandMusicMode.Fingers) && badge.GetSiblingIndex() > hands.GetSiblingIndex(),
                    name + ": the finger count shows in Fingers mode, above the tracked hand");
                var overflow = CheckTools.OverflowingText(canvas).ToList();
                Require(overflow.Count == 0, name + " text overflows: " + string.Join(" | ", overflow));
                Render(folder, name);
                if (mode != HandMusicMode.Fingers) continue;
                Call(c, "ToggleFullCamera");
                overflow = CheckTools.OverflowingText(canvas).ToList();
                Require(overflow.Count == 0, name + " full camera text overflows: " + string.Join(" | ", overflow));
                Render(folder, name + "-full");
            }
    }

    // A mid-session moment: a stand-in camera frame, both hands drawn, and the mode's cue part-way done.
    private static void Pose(HandMusicController c, bool left, HandMusicMode mode)
    {
        var frame = new Texture2D(64, 48, TextureFormat.RGB24, false);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 64; x++)
            frame.SetPixel(x, y, Color.Lerp(new Color(.18f, .22f, .24f), new Color(.42f, .45f, .44f), y / 47f));
        frame.Apply();
        var preview = Get<RawImage>(c, "preview");
        preview.texture = frame;
        preview.color = Color.white;
        var trained = left ? new Vector2(.3f, .45f) : new Vector2(.7f, .45f);
        var attended = left ? new Vector2(.78f, .35f) : new Vector2(.22f, .35f);
        int fingers = 5;
        if (mode == HandMusicMode.OpenHand)
        {
            for (int i = 0; i < 8; i++) Step(c, Hand(trained, .45f), Hand(attended), .25f);
            for (int i = 0; i < 8; i++) Step(c, Hand(trained, .9f), Hand(attended), .25f);
            Step(c, Hand(trained, .45f), Hand(attended));
            for (int i = 0; i < 2; i++) Step(c, Hand(trained, .9f), Hand(attended));
        }
        else
        {
            Step(c, Hand(trained), Hand(attended));
            Call(c, "TickCues", 1f);
            trained = Get<Vector2>(c, "targetPoint") + new Vector2(0, .02f);
            if (mode == HandMusicMode.Fingers) fingers = Get<GuidedHandSong>(c, "song").Next;
            for (int i = 0; i < 2; i++) Step(c, Hand(trained, fingers: fingers), Hand(attended));
        }
        var overlay = Get<HandTrackingOverlay>(c, "overlay");
        Draw(overlay, 1, trained, fingers >= 5 ? 31 : ((1 << fingers) - 1) << 1);
        Draw(overlay, 0, attended, 0);
    }

    // A simple hand skeleton in camera-image space; mask bits raise the thumb (bit 0) and fingers.
    private static void Draw(HandTrackingOverlay overlay, int role, Vector2 palm, int mask)
    {
        var wrist = palm - new Vector2(0, .07f);
        float thumb = (mask & 1) != 0 ? 1 : .5f;
        overlay.points[role][0] = wrist;
        for (int j = 1; j <= 4; j++) overlay.points[role][j] = wrist + new Vector2((palm.x < .5f ? .03f : -.03f) * j * thumb, .025f * j * thumb);
        for (int finger = 0; finger < 4; finger++)
        {
            float step = (mask & (1 << (finger + 1))) != 0 ? .04f : .01f;
            for (int joint = 0; joint < 4; joint++)
                overlay.points[role][5 + finger * 4 + joint] = wrist + new Vector2((finger - 1.5f) * .03f, .08f + joint * step);
        }
        overlay.visible[role] = true;
        overlay.masks[role] = mask;
        overlay.SetVerticesDirty();
    }

    private static void Render(string folder, string name)
    {
        if (folder == null) return;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        CheckTools.Render(new Vector2(1920, 1080), image, 0, 0, 1920, 1080);
        CheckTools.SavePng(image, Path.Combine(folder, name + ".png"));
    }

    private static HandMusicController Build()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/HandMusic.unity");
        var controller = UnityEngine.Object.FindFirstObjectByType<HandMusicController>();
        Call(controller, "BuildUI");
        Call(controller, "BuildAudio");
        return controller;
    }

    private static TrackedHand Hand(Vector2 palm, float openness = .7f, int fingers = 0, MusicGesture pose = MusicGesture.None) =>
        new TrackedHand { visible = true, palm = palm, openness = openness, fingers = fingers, pose = pose };

    private static void Step(HandMusicController c, TrackedHand playHand, TrackedHand otherHand, float elapsed = .1f) =>
        Call(c, "Step", playHand, otherHand, elapsed);

    private static string Status(HandMusicController c) => Get<TMPro.TMP_Text>(c, "status").text;

    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

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
    private static void CheckFingersAndSongs()
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
        Debug.Log("FINGER_AND_SONG_CHECK_PASS");
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
