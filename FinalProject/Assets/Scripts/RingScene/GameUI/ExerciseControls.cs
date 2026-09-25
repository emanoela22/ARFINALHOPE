using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Built at runtime so the exercise does not require manual Inspector wiring.
public class ExerciseControls : MonoBehaviour
{
    private ButterflyAngularTracker tracker;
    private SessionManager session;
    private RectTransform safeArea;
    private GameObject panel;
    private TMP_Text sideText, speedText, rangeText, durationText, feedback;
    private TMP_Text sessionHud;
    private TMP_Text adaptiveSizeText;
    private bool adaptiveSize;
    private int colourChoice;
    private TMP_Text colourText;
    private RectTransform arrow;
    private bool left;
    private float speed, range, duration;
    private float feedbackUntil;
    private GameObject canvasObject;

    private void Start()
    {
        tracker = GetComponent<ButterflyAngularTracker>();
        session = FindFirstObjectByType<SessionManager>();
        tracker.TargetCaught += OnCaught;
        canvasObject = new GameObject("Exercise Controls", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        safeArea = Rect("Safe Area", canvasObject.transform, Vector2.zero, Vector2.zero);
        tracker.HideLegacyScore();
        if (session != null) session.HideLegacyTimer();
        var hud = Rect("Session summary", safeArea, new Vector2(0, -72), new Vector2(540, 90));
        hud.anchorMin = hud.anchorMax = new Vector2(0.5f, 1);
        ModernUI.Surface(hud.gameObject.AddComponent<Image>(), ClinicalMenu.Paper, true);
        sessionHud = Label(hud, "", Vector2.zero, new Vector2(520, 80), 32);
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == "OnBackToMenuPressed")
                    button.gameObject.SetActive(false);
        Button(safeArea, "End session", new Vector2(-190, 70), new Vector2(310, 90), () =>
        {
            if (session != null) session.FinishSession(false);
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
        }, new Vector2(1, 0));
        Button(safeArea, "Exercise settings", new Vector2(-210, -72), new Vector2(350, 90), Open,
            new Vector2(1, 1));
        Button(safeArea, "Reset", new Vector2(145, -72), new Vector2(220, 90), Reset,
            new Vector2(0, 1));
        feedback = Label(safeArea, "", new Vector2(0, 160), new Vector2(700, 100), 48);
        feedback.color = new Color(0.4f, 1f, 0.75f);

        // A simple geometric arrow avoids font-dependent arrow glyphs.
        arrow = Rect("Direction hint", safeArea, Vector2.zero, new Vector2(100, 100));
        Bar(arrow, Vector2.zero, new Vector2(76, 12), 0);
        Bar(arrow, new Vector2(25, 15), new Vector2(48, 12), -45);
        Bar(arrow, new Vector2(25, -15), new Vector2(48, 12), 45);
        arrow.gameObject.SetActive(false);

        var blocker = Rect("Settings overlay", safeArea, Vector2.zero, Vector2.zero);
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.sizeDelta = Vector2.zero;
        blocker.gameObject.AddComponent<Image>().color = ClinicalMenu.Paper;
        panel = blocker.gameObject;
        Label(blocker, "Exercise settings", new Vector2(0, 440), new Vector2(1100, 90), 52);
        Label(blocker, "Paused · Apply restarts the exercise", new Vector2(0, 365), new Vector2(1100, 65), 28);
        sideText = Row(blocker, 255, () => { left = !left; Refresh(); }, () => { left = !left; Refresh(); });
        speedText = Row(blocker, 145, () => { speed = Mathf.Max(0.25f, speed - 0.25f); Refresh(); },
            () => { speed = Mathf.Min(2f, speed + 0.25f); Refresh(); });
        rangeText = Row(blocker, 35, () => { range = Mathf.Max(0.3f, range - 0.05f); Refresh(); },
            () => { range = Mathf.Min(0.95f, range + 0.05f); Refresh(); });
        durationText = Row(blocker, -75, () => { duration = Mathf.Max(15, duration - 15); Refresh(); },
            () => { duration = Mathf.Min(300, duration + 15); Refresh(); });
        adaptiveSizeText = Row(blocker, -185, () => { adaptiveSize = !adaptiveSize; Refresh(); },
            () => { adaptiveSize = !adaptiveSize; Refresh(); });
        colourText = Row(blocker, -295,
            () => { colourChoice = (colourChoice + GameSettings.ButterflyColourNames.Length - 1) % GameSettings.ButterflyColourNames.Length; Refresh(); },
            () => { colourChoice = (colourChoice + 1) % GameSettings.ButterflyColourNames.Length; Refresh(); });
        Button(blocker, "Cancel", new Vector2(-235, -410), new Vector2(350, 95), Close);
        Button(blocker, "Apply & restart", new Vector2(235, -410), new Vector2(400, 95), Apply);
        panel.SetActive(false);
    }

    private void Update()
    {
        sessionHud.text = $"Caught  {tracker.Score}     |     Time  {(session != null ? Mathf.CeilToInt(session.GetRemainingTime()) : 0)}s";
        Rect safe = Screen.safeArea;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        bool show = !panel.activeSelf && tracker.NeedsDirectionHint;
        arrow.gameObject.SetActive(show);
        if (show)
        {
            Vector2 direction = tracker.TargetDirection.normalized;
            Vector2 bounds = safeArea.rect.size * 0.5f - new Vector2(130, 180);
            float distance = Mathf.Min(bounds.x / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
                bounds.y / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            arrow.anchoredPosition = direction * distance;
            arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
        float remaining = feedbackUntil - Time.unscaledTime;
        feedback.text = remaining > 0 ? "Caught! +1" : "";
        feedback.alpha = Mathf.Clamp01(remaining * 2);
        feedback.rectTransform.localScale = Vector3.one * (1f + 0.1f * Mathf.Clamp01(remaining));
    }

    private void Open()
    {
        left = GameSettings.trainLeftSide;
        speed = GameSettings.movementSpeed;
        range = Mathf.Clamp(GameSettings.movementRange, 0.3f, 0.95f);
        duration = GameSettings.sessionDuration;
        adaptiveSize = GameSettings.adaptiveButterflySize;
        colourChoice = GameSettings.butterflyColour;
        Refresh();
        tracker.IsPaused = true;
        if (session != null) session.IsPaused = true;
        panel.SetActive(true);
    }

    private void Close()
    {
        panel.SetActive(false);
        tracker.IsPaused = false;
        if (session != null) session.IsPaused = false;
    }

    private void Apply()
    {
        GameSettings.trainLeftSide = left;
        GameSettings.movementSpeed = speed;
        GameSettings.movementRange = range;
        GameSettings.sessionDuration = duration;
        GameSettings.adaptiveButterflySize = adaptiveSize;
        GameSettings.butterflyColour = colourChoice;
        GameSettings.Save();
        Reset();
        Close();
    }

    private void Reset()
    {
        feedbackUntil = 0;
        tracker.ResetExercise();
    }

    private void Refresh()
    {
        adaptiveSizeText.text = "Adaptive size: " + (adaptiveSize ? "On" : "Off");
        colourText.text = "Butterfly colour: " + GameSettings.ButterflyColourNames[colourChoice];
        sideText.text = "Direction: " + (left ? "Left (LT / LB)" : "Right (RT / RB)");
        speedText.text = $"Movement speed: {speed:0.##}x";
        rangeText.text = $"Movement range: {range:P0}";
        durationText.text = $"Session duration: {duration:0}s";
    }

    private void OnCaught() => feedbackUntil = Time.unscaledTime + 1.2f;

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var label = Rect(text.Length > 0 ? text : "Label", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = ModernUI.ReadableFont;
        label.text = text;
        label.fontSize = Mathf.Max(36, fontSize);
        if (fontSize >= 40) label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = ClinicalMenu.Ink;
        label.raycastTarget = false;
        return label;
    }

    private static void Button(Transform parent, string text, Vector2 position, Vector2 size,
        UnityEngine.Events.UnityAction action, Vector2? anchor = null)
    {
        var rect = Rect(text, parent, position, size);
        if (anchor.HasValue) rect.anchorMin = rect.anchorMax = anchor.Value;
        var background = rect.gameObject.AddComponent<Image>();
        ModernUI.Surface(background, ClinicalMenu.Teal, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);
        Label(rect, text, Vector2.zero, size - new Vector2(12, 8), 30).color = Color.white;
    }

    private static TMP_Text Row(Transform parent, float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        Button(parent, "-", new Vector2(-510, y), new Vector2(105, 85), minus);
        Button(parent, "+", new Vector2(510, y), new Vector2(105, 85), plus);
        return Label(parent, "", new Vector2(0, y), new Vector2(850, 85), 34);
    }

    private static void Bar(Transform parent, Vector2 position, Vector2 size, float angle)
    {
        var rect = Rect("Arrow", parent, position, size);
        rect.localRotation = Quaternion.Euler(0, 0, angle);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 0.85f, 0.2f);
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (tracker != null) tracker.TargetCaught -= OnCaught;
        if (canvasObject != null) Destroy(canvasObject);
    }
}
