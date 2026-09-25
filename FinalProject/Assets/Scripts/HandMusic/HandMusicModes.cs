using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Play modes built around the trained (neglected) side: that side's hand plays, and the cues
// lead the eyes toward that side. Everything follows GameSettings.trainLeftSide.
public partial class HandMusicController
{
    private static readonly Color Gold = new Color(1f, .85f, .2f);
    private const float SweepSeconds = .7f;
    private RectTransform cameraImage, target, sweep, playZone, countBadge;
    private TargetPadGraphic targetPad;
    private TMP_Text targetLabel;
    private readonly FingerMaskFilter[] fingerFilters = { new FingerMaskFilter(), new FingerMaskFilter() };
    private readonly ReachTargets targets = new ReachTargets();
    private readonly DwellGate dwell = new DwellGate();
    private readonly OpenHandGate openGate = new OpenHandGate();
    private readonly OpennessCalibration calibration = new OpennessCalibration();
    private Vector2 targetPoint;
    private float sweepAge = -1, targetShownAt; // sweepAge < 0 when no sweep is running
    private bool targetShown;
    private string detectionSummary = "";
    private static HandMusicMode Mode => GameSettings.handMusicMode;
    private static bool CircleMode => Mode == HandMusicMode.Fingers;
    private static float Radius => ReachTargets.MaxRadius * GameSettings.handMusicCircle;

    private void ProcessHands(bool detected, float elapsed)
    {
        int trained = -1, other = -1;
        float trainedScore = 0, otherScore = 0;
        ClearOverlay();
        if (detected && result.handedness != null && result.handWorldLandmarks != null && result.handLandmarks != null)
        {
            int count = Mathf.Min(result.handedness.Count, Mathf.Min(result.handWorldLandmarks.Count, result.handLandmarks.Count));
            for (int i = 0; i < count; i++)
            {
                var categories = result.handedness[i].categories;
                if (categories == null || categories.Count == 0 || result.handWorldLandmarks[i].landmarks.Count < 21 || result.handLandmarks[i].landmarks.Count < 21) continue;
                var category = categories[0];
                if (category.score < .7f) continue;
                if (category.categoryName != "Left" && category.categoryName != "Right") continue;
                if (HandGesture.IsLeftHand(category.categoryName, front) == TrainLeft) { if (category.score > trainedScore) { trained = i; trainedScore = category.score; } }
                else if (category.score > otherScore) { other = i; otherScore = category.score; }
            }
        }
        // Overlay role 1 is drawn gold (the trained-side hand that plays), role 0 mint (the other hand).
        var playHand = Track(trained, 1);
        var otherHand = Track(other, 0);
        overlay.SetVerticesDirty();
        Step(playHand, otherHand, elapsed);
    }

    private TrackedHand Track(int index, int role)
    {
        if (index < 0) { fingerFilters[role].Reset(); return default; }
        var world = result.handWorldLandmarks[index].landmarks;
        var normalized = result.handLandmarks[index].landmarks;
        var points = overlay.points[role];
        for (int j = 0; j < 21; j++)
        {
            landmarks[j] = new Vector3(world[j].x, world[j].y, world[j].z);
            points[j] = new Vector2(normalized[j].x, 1 - normalized[j].y);
        }
        overlay.visible[role] = true;
        overlay.masks[role] = fingerFilters[role].Sample(HandGesture.FingerMask(landmarks));
        var pose = HandGesture.Classify(landmarks);
        return new TrackedHand
        {
            visible = true,
            palm = HandGesture.PalmCentre(points),
            openness = HandGesture.Openness(landmarks),
            fingers = pose == MusicGesture.Pinch ? 6 : HandGesture.FingerCount(overlay.masks[role]),
            pose = pose
        };
    }

    // One tracking sample: the active mode turns the trained hand into notes.
    private void Step(TrackedHand playHand, TrackedHand otherHand, float elapsed)
    {
        detectionSummary = $"{Capital(Side)} hand: {(playHand.visible ? "playing" : "not visible")}\n" +
            $"{Capital(GameSettings.OtherSide)} hand: {(otherHand.visible ? "in view" : "resting")}";
        if (selectingSong || sessionOver) { StopPlaying(); RefreshSongGuide(); return; }
        if (Mode == HandMusicMode.OpenHand && !calibration.Done) Calibrate(playHand, elapsed);
        else if (!sessionRunning && !playHand.visible)
            status.text = $"Find the gold line on your {Side}, then show your {Side} hand.";
        else
        {
            // The timer starts once the playing hand is in view, not while the phone is being set up.
            if (!sessionRunning) BeginSession();
            if (song.Complete) { status.text = "Song complete! It starts again in a moment."; progress.fillAmount = 0; }
            else if (CircleMode) StepCircle(playHand, otherHand, elapsed);
            else StepOpen(playHand, elapsed);
        }
        RefreshSongGuide();
    }

    // Fingers mode: a note plays only while the trained hand is inside the gold circle holding up
    // that note's finger count (1-5 for C-G, a pinch for A) for the hold time.
    private void StepCircle(TrackedHand hand, TrackedHand other, float elapsed)
    {
        if (!targetShown) { dwell.Reset(); status.text = $"Look {Side} for the gold circle."; return; }
        float aspect = previewAspect.aspectRatio;
        bool inside = hand.visible && ReachTargets.Contains(targetPoint, hand.palm, aspect, Radius);
        bool shaped = hand.fingers == song.Next;
        bool fired = dwell.Sample(inside && shaped, elapsed, GameSettings.handMusicHold);
        targetPad.Progress = progress.fillAmount = dwell.Progress(GameSettings.handMusicHold);
        if (fired)
        {
            targets.Register(sessionElapsed - targetShownAt);
            PlaySongNote(Pan(targetPoint.x));
            if (!song.Complete) NextTarget();
            else ShowTarget(false);
            return;
        }
        if (!hand.visible) status.text = $"Show your {Side} hand.";
        else if (inside && shaped) status.text = "Hold it there…";
        else if (inside) status.text = (hand.fingers == 0 ? "Now show " : $"That's {Shown(hand.fingers)}. Show ") + Shown(song.Next) + ".";
        else if (other.visible && ReachTargets.Contains(targetPoint, other.palm, aspect, Radius))
            status.text = $"Use your {Side} hand for the gold circle.";
        else status.text = $"Reach the gold circle with your {Side} hand.";
    }

    private void StepOpen(TrackedHand hand, float elapsed)
    {
        bool inZone = hand.visible && InTrainedHalf(hand.palm);
        bool fired = openGate.Sample(hand.openness, inZone, elapsed, GameSettings.handMusicHold);
        progress.fillAmount = openGate.Progress(GameSettings.handMusicHold);
        if (fired) { PlaySongNote(TrainLeft ? -.6f : .6f); return; }
        if (!hand.visible) status.text = $"Show your {Side} hand.";
        else if (!inZone) status.text = $"Keep your {Side} hand on the gold side.";
        else if (!openGate.Armed) status.text = "Close your hand, then open it to play.";
        else status.text = hand.openness >= openGate.OpenAt ? "Keep it open…" : $"Open your {Side} hand to play.";
    }

    // Two short steps measure this hand's own closed and open levels before the timer starts.
    private void Calibrate(TrackedHand hand, float elapsed)
    {
        calibration.Sample(hand.openness, hand.visible, elapsed);
        progress.fillAmount = calibration.Progress;
        if (calibration.Done)
        {
            openGate.Calibrate(calibration.ClosedLevel, calibration.OpenLevel);
            status.text = "All set! Close your hand, then open it to play.";
        }
        else if (!hand.visible) status.text = $"Show your {Side} hand to measure how it moves.";
        else status.text = calibration.Step == 0 ? $"Rest your {Side} hand in a loose fist…"
            : $"Now open your {Side} hand as wide as is comfortable…";
    }

    // Plays the song's next note and counts it toward the session.
    private void PlaySongNote(float pan)
    {
        int note = song.Next;
        if (!song.Play(note)) return;
        PlayNote(note, pan);
        CountNote();
        if (song.Complete) songDoneAt = sessionElapsed;
    }

    private static string Shown(int fingers) => fingers == 6 ? "a pinch" : $"{fingers} finger{(fingers == 1 ? "" : "s")}";
    private static bool InTrainedHalf(Vector2 palm) => TrainLeft ? palm.x <= .5f : palm.x >= .5f;
    private static float Pan(float x) => Mathf.Clamp((x - .5f) * 1.6f, -.8f, .8f);
    private static string Capital(string text) => char.ToUpper(text[0]) + text.Substring(1);

    private void BuildCues()
    {
        // A steady gold edge on the trained side to look for: a scanning anchor.
        var edge = Rect("Gold line", cameraImage, new Vector2(TrainLeft ? 0 : .985f, 0), new Vector2(TrainLeft ? .015f : 1, 1));
        var line = edge.gameObject.AddComponent<Image>(); line.color = Gold; line.raycastTarget = false;
        // Open hand mode plays on the trained half of the view.
        playZone = Rect("Play zone", cameraImage, new Vector2(TrainLeft ? 0 : .5f, 0), new Vector2(TrainLeft ? .5f : 1, 1));
        var zone = playZone.gameObject.AddComponent<Image>(); zone.color = new Color(1f, .85f, .2f, .1f); zone.raycastTarget = false;
        playZone.gameObject.SetActive(!CircleMode);
        sweep = Rect("Guiding sweep", cameraImage, Vector2.zero, Vector2.one);
        var beam = sweep.gameObject.AddComponent<Image>(); beam.color = new Color(1f, .85f, .2f, .6f); beam.raycastTarget = false;
        sweep.gameObject.SetActive(false);
        target = Rect("Gold circle", cameraImage, Vector2.zero, Vector2.one);
        targetPad = target.gameObject.AddComponent<TargetPadGraphic>(); targetPad.color = Gold; targetPad.raycastTarget = false;
        target.gameObject.SetActive(false);
    }

    // Fingers mode shows the count to hold up in a white badge on the circle's rim, where the patient
    // is looking. Built after the hand overlay, so the tracked hand never covers it.
    private void BuildCountBadge()
    {
        countBadge = Rect("Finger count", cameraImage, Vector2.zero, Vector2.one);
        var badge = countBadge.gameObject.AddComponent<Image>();
        ModernUI.Surface(badge, Color.white, true);
        badge.raycastTarget = false;
        targetLabel = Rect("Count", countBadge, new Vector2(.12f, .12f), new Vector2(.88f, .88f)).gameObject.AddComponent<TextMeshProUGUI>();
        targetLabel.font = ModernUI.ReadableFont; targetLabel.fontStyle = FontStyles.Bold; targetLabel.color = ClinicalMenu.Ink;
        targetLabel.alignment = TextAlignmentOptions.Center; targetLabel.raycastTarget = false;
        targetLabel.enableAutoSizing = true; targetLabel.fontSizeMin = 12; targetLabel.fontSizeMax = 80;
        countBadge.gameObject.SetActive(false);
    }

    private void ShowTarget(bool shown)
    {
        target.gameObject.SetActive(shown);
        countBadge.gameObject.SetActive(shown && Mode == HandMusicMode.Fingers);
    }

    private void NextTarget()
    {
        targetPoint = targets.Next(TrainLeft, Radius, Mathf.Max(.1f, previewAspect.aspectRatio));
        targetShown = false;
        targetPad.Progress = 0;
        dwell.Reset();
        ShowTarget(false);
        sweepAge = 0;
    }

    // Anchors the circle around its point; the horizontal radius is scaled so it stays round on screen.
    private void PositionTarget()
    {
        float aspect = Mathf.Max(.1f, previewAspect.aspectRatio);
        var half = new Vector2(Radius / aspect, Radius);
        target.anchorMin = targetPoint - half;
        target.anchorMax = targetPoint + half;
        // The badge sits on the upper rim, toward the other side and clear of the raised fingers.
        var badge = targetPoint + new Vector2(TrainLeft ? half.x : -half.x, half.y) * .75f;
        countBadge.anchorMin = badge - half * .42f;
        countBadge.anchorMax = badge + half * .42f;
    }

    private void ChangeCircle(float delta)
    {
        GameSettings.handMusicCircle = Mathf.Clamp(Mathf.Round((GameSettings.handMusicCircle + delta) * 10) / 10, .5f, 1);
        GameSettings.Save();
        circleLabel.text = $"Circle: {GameSettings.handMusicCircle:P0}";
        PositionTarget();
    }

    // A gold beam sweeps from the attended side to the new circle (the "lighthouse"), then the
    // circle lights up with a soft preview of its note from that side.
    private void TickCues(float delta)
    {
        if (targetShown && !sessionOver) target.localScale = Vector3.one * (1 + .04f * Mathf.Sin(Time.unscaledTime * 4));
        if (sweepAge < 0) return;
        sweepAge += delta;
        float t = Mathf.Clamp01(sweepAge / SweepSeconds);
        float x = Mathf.Lerp(TrainLeft ? .97f : .03f, targetPoint.x, 1 - (1 - t) * (1 - t));
        sweep.anchorMin = new Vector2(x - .012f, 0);
        sweep.anchorMax = new Vector2(x + .012f, 1);
        sweep.gameObject.SetActive(t < 1);
        if (t < 1) return;
        sweepAge = -1;
        PositionTarget();
        targetLabel.text = song.Next == 6 ? "Pinch" : song.Next.ToString();
        ShowTarget(true);
        targetShown = true;
        targetShownAt = sessionElapsed;
        if (!song.Complete) PlayNote(song.Next, Pan(targetPoint.x), .2f);
    }
}
