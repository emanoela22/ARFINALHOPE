using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Accessible, scrollable portrait menu using the existing exercise scene.</summary>
public class ClinicalMenu : MonoBehaviour
{
    public static readonly Color Ink = new Color(0.08f, 0.19f, 0.25f);
    public static readonly Color Teal = new Color(0.0f, 0.36f, 0.38f);
    public static readonly Color Paper = new Color(0.94f, 0.98f, 0.97f);
    private Transform content;
    private RectTransform safe;
    private ScrollRect scroll;
    private GameObject root;
    private MenuUIController controller;
    private int page;
    private Transform navigation;

    private void Start()
    {
        controller = GetComponent<MenuUIController>();
        // Keep the authored menu intact; this replacement owns its presentation.
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.isRootCanvas) canvas.gameObject.SetActive(false);
        root = new GameObject("Clinical menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        root.AddComponent<Image>().color = Paper;
        var colourField = Box("Teal background", root.transform);
        colourField.anchorMin = new Vector2(0, 0.65f);
        colourField.gameObject.AddComponent<Image>().color = Teal;
        safe = Box("Safe area", root.transform);
        var viewport = Box("Scroll viewport", safe);
        viewport.offsetMin = new Vector2(48, 185);
        viewport.offsetMax = new Vector2(-48, -36);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var body = Box("Page content", viewport);
        body.anchorMin = new Vector2(0, 1);
        body.anchorMax = Vector2.one;
        body.pivot = new Vector2(0.5f, 1);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content = body;
        scroll.content = body;
        var nav = Box("Bottom navigation", safe);
        nav.anchorMax = new Vector2(1, 0);
        nav.offsetMin = new Vector2(40, 24);
        nav.offsetMax = new Vector2(-40, 150);
        ModernUI.Surface(nav.gameObject.AddComponent<Image>(), new Color(1, 1, 1, 0.96f), true);
        var navLayout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
        navLayout.padding = new RectOffset(12, 12, 12, 12);
        navLayout.spacing = 8;
        navLayout.childControlWidth = true;
        navLayout.childControlHeight = true;
        navLayout.childForceExpandWidth = true;
        navigation = nav;
        Home();
    }

    private void Update()
    {
        var area = Screen.safeArea;
        safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
    }

    private void Clear(string title, string subtitle)
    {
        foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var header = Item("Welcome header", 430);
        ModernUI.Surface(header.AddComponent<Image>(), Teal);
        var headerLayout = header.AddComponent<VerticalLayoutGroup>();
        headerLayout.padding = new RectOffset(36, 36, 28, 28);
        headerLayout.spacing = 12;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;
        MakeText(header.transform, "Butterfly Care", 38, 60, new Color(0.76f, 0.97f, 0.9f));
        MakeText(header.transform, title, 72, 106, Color.white);
        MakeText(header.transform, subtitle, 38, 125, Color.white);
        scroll.verticalNormalizedPosition = 1;
    }

    private void Navigation(string selected)
    {
        foreach (Transform child in navigation) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        MakeButton(navigation, "Home", Home, selected == "Home");
        MakeButton(navigation, "Progress", () => { page = 0; History(); }, selected == "Progress");
        MakeButton(navigation, "Settings", Settings, selected == "Settings");
    }

    private void Home()
    {
        Clear("Your daily focus", "A little focus. A fresh start.");
        Navigation("Home");
        var history = ProgressStore.LoadHistory();
        var today = history.Where(r => r.dateLocal != null && r.dateLocal.StartsWith(ProgressStore.TodayDateLocal())).ToList();
        ExerciseHero();
        Action("Start exercise   →", Instructions);
        Action("Hand Music   →", HandMusicInstructions, false);
        var row = Item("Today's statistics", 330);
        var columns = row.AddComponent<HorizontalLayoutGroup>();
        columns.spacing = 24;
        columns.childControlWidth = true;
        columns.childControlHeight = true;
        columns.childForceExpandWidth = true;
        Metric(row.transform, "Caught today", today.Sum(r => r.score).ToString(), "butterflies", new Color(0.79f, 0.94f, 0.91f));
        Metric(row.transform, "Sessions today", today.Count(r => r.completed).ToString(), "completed", new Color(0.86f, 0.89f, 0.98f));
        ScoreChart(history);
        Text($"{history.Sum(r => r.score)} lifetime catches  ·  {history.Count} saved sessions", 28, 70, Ink);
        Text("Saved on this device. Session lengths and settings can affect scores.", 25, 90, Ink);
    }

    private void HandMusicInstructions()
    {
        Clear("Hand Music", "Prop up the phone in landscape and show both hands to the front camera.");
        Navigation("");
        Card("YOUR CONTROL HAND", "Open to play · Fist to stop", "Your left hand controls playback. Your right hand chooses notes. Use Swap hands to reverse them.");
        Card("YOUR NOTE HAND", "1–5 fingers = C–G", "Hold a finger count to play a note. Lower all fingers briefly before repeating the same note.");
        Card("CHOOSE A SONG", "Hot Cross Buns or Twinkle", "Use Choose song to switch tunes. Follow each note at your own pace. For A, pinch thumb and index with another finger raised.");
        Action("Open front camera", () => SceneManager.LoadScene("HandMusic"));
        Action("Back", Home, false);
    }

    private void Instructions()
    {
        Clear("Before you begin", "Use the position and exercise settings agreed with your care team.");
        Navigation("");
        Card("01  GET READY", "Hold the phone in landscape", "Face forward comfortably before starting.");
        Card("02  FIND THE BUTTERFLY", "Move it into the centre ring", "Gently turn the phone to follow the butterfly. An arrow helps when the target is farther away.");
        Card("03  HOLD & CATCH", $"Keep it in the ring for {GameSettings.holdTime:0.0}s", "Each catch adds one point. You can pause using Exercise settings.");
        Action("Begin session", () => SceneManager.LoadScene(controller.arSceneName));
        Action("Back", Home, false);
    }

    private void ExerciseHero()
    {
        var card = Item("Butterfly exercise", 340);
        ModernUI.Surface(card.AddComponent<Image>(), Color.white, true);
        var art = Box("Butterfly illustration", card.transform);
        art.anchorMin = art.anchorMax = new Vector2(0.8f, 0.5f);
        art.sizeDelta = new Vector2(210, 220);
        art.anchoredPosition = Vector2.zero;
        // Lightweight vector-like wings: decorative only, never behind text.
        Wing(art, new Vector2(-42, 32), new Vector2(95, 115), 25, new Color(0.12f, 0.7f, 0.65f));
        Wing(art, new Vector2(42, 32), new Vector2(95, 115), -25, new Color(0.12f, 0.7f, 0.65f));
        Wing(art, new Vector2(-34, -46), new Vector2(75, 85), -25, new Color(0.62f, 0.85f, 0.91f));
        Wing(art, new Vector2(34, -46), new Vector2(75, 85), 25, new Color(0.62f, 0.85f, 0.91f));
        Wing(art, Vector2.zero, new Vector2(16, 135), 0, Teal);
        var words = Box("Exercise description", card.transform);
        words.anchorMax = new Vector2(0.62f, 1);
        words.offsetMin = new Vector2(32, 24);
        words.offsetMax = new Vector2(0, -24);
        var layout = words.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        MakeText(words, "Your next session", 36, 80, Teal);
        MakeText(words, "Follow the\nbutterfly", 54, 135, Ink);
        MakeText(words, $"{GameSettings.sessionDuration:0}s · {(GameSettings.trainLeftSide ? "Left" : "Right")} side", 38, 80, Ink);
    }

    private static void Wing(Transform parent, Vector2 position, Vector2 size, float angle, Color color)
    {
        var wing = Box("Wing", parent);
        wing.anchorMin = wing.anchorMax = Vector2.one * 0.5f;
        wing.sizeDelta = size;
        wing.anchoredPosition = position;
        wing.localRotation = Quaternion.Euler(0, 0, angle);
        var image = wing.gameObject.AddComponent<Image>();
        ModernUI.Surface(image, color);
        image.raycastTarget = false;
    }

    private void Settings()
    {
        Clear("Exercise settings", "Choose a comfortable starting level. You can also adjust these during the exercise.");
        Navigation("Settings");
        Action("Butterfly colour: " + GameSettings.ButterflyColourNames[GameSettings.butterflyColour],
            () => RefreshSetting(() => GameSettings.butterflyColour = (GameSettings.butterflyColour + 1) % GameSettings.ButterflyColourNames.Length), false);
        var swatch = Item("Selected butterfly colour", 30);
        ModernUI.Surface(swatch.AddComponent<Image>(), GameSettings.ButterflyColours[GameSettings.butterflyColour]);
        Action("Adaptive size: " + (GameSettings.adaptiveButterflySize ? "On" : "Off"),
            () => RefreshSetting(() => GameSettings.adaptiveButterflySize = !GameSettings.adaptiveButterflySize), false);
        Text("Gently grows and shrinks as the butterfly moves. Size stays steady while you hold it in the ring.", 38, 155, Ink);
        Action("Training side: " + (GameSettings.trainLeftSide ? "Left" : "Right") + " · Tap to switch",
            () => { GameSettings.trainLeftSide = !GameSettings.trainLeftSide; Settings(); }, false);
        Adjust("Movement speed", $"{GameSettings.movementSpeed:0.##}x", () => GameSettings.movementSpeed = Mathf.Max(0.25f, GameSettings.movementSpeed - 0.25f),
            () => GameSettings.movementSpeed = Mathf.Min(2, GameSettings.movementSpeed + 0.25f));
        Adjust("Movement range", $"{GameSettings.movementRange:P0}", () => GameSettings.movementRange = Mathf.Max(0.3f, GameSettings.movementRange - 0.05f),
            () => GameSettings.movementRange = Mathf.Min(0.95f, GameSettings.movementRange + 0.05f));
        Adjust("Session duration", $"{GameSettings.sessionDuration:0}s", () => GameSettings.sessionDuration = Mathf.Max(15, GameSettings.sessionDuration - 15),
            () => GameSettings.sessionDuration = Mathf.Min(300, GameSettings.sessionDuration + 15));
        Adjust("Hold to catch", $"{GameSettings.holdTime:0.0}s", () => GameSettings.holdTime = Mathf.Max(0.5f, GameSettings.holdTime - 0.5f),
            () => GameSettings.holdTime = Mathf.Min(4, GameSettings.holdTime + 0.5f));
        Action("Done", Home);
        Action("Debug: reset saved statistics", ConfirmStatisticsReset, false);
    }

    private void ConfirmStatisticsReset()
    {
        Clear("Reset statistics?", "This clears saved scores and session history on this device.");
        Navigation("Settings");
        Card("DEBUG TOOL", "Start with a clean record", "Today's totals, best score and the progress chart will reset. Exercise settings will stay the same.");
        Action("Cancel — keep my statistics", Settings);
        Action("Yes, reset saved statistics", () =>
        {
            ProgressStore.ResetStatistics();
            controller.RefreshAll();
            Home();
        }, false);
    }

    private void History()
    {
        Clear("Your progress", "Catch counts and times describe your exercise sessions; they are not a clinical assessment.");
        Navigation("Progress");
        var history = ProgressStore.LoadHistory();
        if (history.Count > 0) ScoreChart(history);
        if (history.Count == 0)
            Card("READY WHEN YOU ARE", "No saved sessions yet", "Complete an exercise to begin your history. Sessions ended early are also recorded.");
        else
        {
            var ordered = history.AsEnumerable().Reverse().ToList();
            page = Mathf.Clamp(page, 0, (ordered.Count - 1) / 6);
            foreach (var result in ordered.Skip(page * 6).Take(6))
                Card(result.dateLocal + (result.completed ? " · Completed" : " · Ended early"),
                    $"{result.score} butterflies caught",
                    $"{result.neglectedSide} side · {result.durationSeconds:0}s active\nAverage catch time: " +
                    (result.score > 0 ? $"{result.avgReactionTime:0.0}s" : "—"));
            Text($"Page {page + 1} of {(ordered.Count + 5) / 6}", 28, 55, Ink);
            if (page > 0) Action("Newer sessions", () => { page--; History(); }, false);
            if ((page + 1) * 6 < ordered.Count) Action("Older sessions", () => { page++; History(); }, false);
        }
        Action("Back to home", Home);
    }

    private void Adjust(string title, string value, System.Action minus, System.Action plus)
    {
        Text(title + ": " + value, 34, 65, Ink);
        var row = Item("Adjustment", 130);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        MakeButton(row.transform, "−  Decrease", () => RefreshSetting(minus), false);
        MakeButton(row.transform, "+  Increase", () => RefreshSetting(plus), false);
    }

    private void RefreshSetting(System.Action change)
    {
        float position = scroll.verticalNormalizedPosition;
        change();
        Settings();
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = position;
    }

    private static void Metric(Transform parent, string title, string value, string suffix, Color tint)
    {
        var box = new GameObject(title, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
        box.transform.SetParent(parent, false);
        box.GetComponent<LayoutElement>().preferredWidth = 440;
        ModernUI.Surface(box.GetComponent<Image>(), tint, true);
        var layout = box.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 20, 20);
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        MakeText(box.transform, title, 36, 66, Teal);
        MakeText(box.transform, value, 88, 115, Ink);
        MakeText(box.transform, suffix, 36, 60, Ink);
    }

    private void ScoreChart(System.Collections.Generic.List<SessionResult> history)
    {
        var card = Item("Recent scores", 340);
        ModernUI.Surface(card.AddComponent<Image>(), Color.white, true);
        var title = Box("Chart title", card.transform);
        title.anchorMin = new Vector2(0, 1);
        title.offsetMin = new Vector2(28, -65);
        title.offsetMax = new Vector2(-28, -15);
        var heading = title.gameObject.AddComponent<TextMeshProUGUI>();
        heading.font = ModernUI.ReadableFont;
        heading.text = "Recent session scores";
        heading.fontSize = 38;
        heading.fontStyle = FontStyles.Bold;
        heading.color = Ink;
        heading.raycastTarget = false;
        var samples = history.Skip(Mathf.Max(0, history.Count - 7)).ToList();
        if (samples.Count == 0)
        {
            var empty = Box("No scores yet", card.transform);
            empty.offsetMin = new Vector2(28, 35);
            empty.offsetMax = new Vector2(-28, -80);
            var text = empty.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = heading.font;
            text.text = "Your progress starts here.\nFinish a session to see your scores.";
            text.fontSize = 38;
            text.color = Ink;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return;
        }
        int maximum = Mathf.Max(1, samples.Max(r => r.score));
        for (int i = 0; i < samples.Count; i++)
        {
            var column = Box("Session score", card.transform);
            column.anchorMin = new Vector2((float)i / samples.Count, 0);
            column.anchorMax = new Vector2((float)(i + 1) / samples.Count, 1);
            column.offsetMin = new Vector2(24, 22);
            column.offsetMax = new Vector2(-24, -82);
            float height = samples[i].score == 0 ? 3 : 115f * samples[i].score / maximum;
            var bar = Box("Score bar", column);
            bar.anchorMax = new Vector2(1, 0);
            bar.offsetMin = new Vector2(10, 45);
            bar.offsetMax = new Vector2(-10, 45 + height);
            ModernUI.Surface(bar.gameObject.AddComponent<Image>(), i == samples.Count - 1 ? Teal : new Color(0.64f, 0.86f, 0.92f));
            var value = Box("Score value", column);
            value.anchorMin = new Vector2(0, 0);
            value.anchorMax = new Vector2(1, 0);
            value.offsetMin = new Vector2(0, 47 + height);
            value.offsetMax = new Vector2(0, 90 + height);
            var score = value.gameObject.AddComponent<TextMeshProUGUI>();
            score.font = heading.font;
            score.text = samples[i].score.ToString();
            score.fontSize = 36;
            score.color = Ink;
            score.alignment = TextAlignmentOptions.Center;
            score.raycastTarget = false;
            var label = Box("Session number", column);
            label.anchorMax = new Vector2(1, 0);
            label.offsetMin = Vector2.zero;
            label.offsetMax = new Vector2(0, 38);
            var caption = label.gameObject.AddComponent<TextMeshProUGUI>();
            caption.font = heading.font;
            caption.text = "#" + (history.Count - samples.Count + i + 1);
            caption.fontSize = 32;
            caption.color = Teal;
            caption.alignment = TextAlignmentOptions.Center;
            caption.raycastTarget = false;
        }
    }

    private void Card(string eyebrow, string value, string detail, Color? tint = null)
    {
        var card = Item("Progress card", 395);
        ModernUI.Surface(card.AddComponent<Image>(), tint ?? new Color(1, 1, 1, 0.95f), true);
        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 22, 22);
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.spacing = 8;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        MakeText(card.transform, eyebrow, 36, 85, Teal);
        MakeText(card.transform, value, 50, 100, Ink);
        MakeText(card.transform, detail, 38, 145, Ink);
    }

    private void Action(string title, UnityEngine.Events.UnityAction action, bool primary = true)
        => MakeButton(content, title, action, primary);
    private void Text(string text, int size, float height, Color color) => MakeText(content, text, size, height, color);

    private GameObject Item(string name, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        obj.transform.SetParent(content, false);
        obj.GetComponent<LayoutElement>().preferredHeight = height;
        return obj;
    }

    private static void MakeText(Transform parent, string text, int size, float height, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(height, 80);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = ModernUI.ReadableFont;
        label.text = text;
        label.fontSize = Mathf.Max(size, 36);
        if (size >= 42) label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void MakeButton(Transform parent, string title, UnityEngine.Events.UnityAction action, bool primary)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 140;
        ModernUI.Surface(go.GetComponent<Image>(), primary ? Teal : new Color(1, 1, 1, 0.9f), primary);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(action);
        var rect = Box("Label", go.transform);
        rect.offsetMin = new Vector2(24, 8);
        rect.offsetMax = new Vector2(-24, -8);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = ModernUI.ReadableFont;
        label.text = title;
        label.fontSize = 42;
        label.fontStyle = FontStyles.Bold;
        label.color = primary ? Color.white : Teal;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static RectTransform Box(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void OnDestroy() { if (root != null) Destroy(root); }
}
