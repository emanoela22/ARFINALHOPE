using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Accessible, scrollable portrait menu using the existing exercise scene.</summary>
public class ClinicalMenu : MonoBehaviour
{
    public static readonly Color Ink = new Color(0.08f, 0.19f, 0.25f);
    public static readonly Color Teal = new Color(0.0f, 0.36f, 0.38f);
    public static readonly Color Paper = new Color(0.94f, 0.98f, 0.97f);
    public static readonly Color Mint = new Color(0.79f, 0.94f, 0.91f);
    private static readonly Color Lavender = new Color(0.86f, 0.89f, 0.98f);
    // Hand Music's accent, paired with the Lavender tile; white text on it is as legible as on Teal.
    private static readonly Color Purple = new Color(0.34f, 0.25f, 0.64f);
    // Room for the app name, a one-line title and up to two description lines.
    private const float HeaderHeight = 300;
    private Transform content;
    private RectTransform safe;
    private ScrollRect scroll;
    private GameObject root;
    private MenuUIController controller;
    private int page;
    private Transform navigation;
    private TMP_Text headerTitle, headerSubtitle;
    private int previousFrameRate = -1;
    // Labels that show a setting: a tap updates them in place instead of rebuilding the page.
    private readonly System.Collections.Generic.List<System.Action> refreshers = new System.Collections.Generic.List<System.Action>();

    private void Start()
    {
        controller = GetComponent<MenuUIController>();
        // Phones default to 30 fps; scrolling and taps feel smooth at 60. Restored when the menu closes.
        previousFrameRate = Application.targetFrameRate;
        Application.targetFrameRate = 60;
        // On a dense screen the default 10 px is under a millimetre, so a slightly wobbly tap (a tremor)
        // scrolled the page instead of pressing the button under the finger.
        var events = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (events != null) events.pixelDragThreshold = Mathf.Max(events.pixelDragThreshold, Mathf.RoundToInt((Screen.dpi > 0 ? Screen.dpi : 160) * .1f));
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
        safe = Box("Safe area", root.transform);
        // Fixed page header; it reaches past the safe area so the teal also fills the notch and status bar.
        var header = Box("Page header", safe);
        header.anchorMin = new Vector2(0, 1);
        header.offsetMin = new Vector2(-200, -HeaderHeight);
        header.offsetMax = new Vector2(200, 400);
        header.gameObject.AddComponent<Image>().color = Teal;
        var headerText = Box("Header text", safe);
        headerText.anchorMin = new Vector2(0, 1);
        headerText.offsetMin = new Vector2(48, -HeaderHeight);
        headerText.offsetMax = new Vector2(-48, 0);
        var headerLayout = headerText.gameObject.AddComponent<VerticalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.spacing = 4;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;
        HeaderText(headerText, 38, new Color(0.76f, 0.97f, 0.9f)).text = "Butterfly Care";
        headerTitle = HeaderText(headerText, 72, Color.white);
        headerSubtitle = HeaderText(headerText, 38, Color.white);
        // Starts below the header, so pages scroll under nothing and never over it.
        var viewport = Box("Scroll viewport", safe);
        viewport.offsetMin = new Vector2(48, 185);
        viewport.offsetMax = new Vector2(-48, -HeaderHeight - 24);
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
        StartCoroutine(PrepareLetters());
    }

    // The font draws each letter the first time it is shown, which made the first visit to a page pause.
    // Drawing the rest a few per frame once Home is up keeps later pages quick.
    private System.Collections.IEnumerator PrepareLetters()
    {
        const string letters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·→…";
        for (int i = 0; i < letters.Length; i += 6)
        {
            yield return null;
            ModernUI.ReadableFont.TryAddCharacters(letters.Substring(i, Mathf.Min(6, letters.Length - i)));
        }
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
        refreshers.Clear();
        headerTitle.text = title;
        headerSubtitle.text = subtitle;
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
        HandMusicHero();
        Action("Start Hand Music   →", HandMusicInstructions, accent: Purple);
        var butterflies = history.Where(r => !r.IsHandMusic).ToList();
        var music = history.Where(r => r.IsHandMusic).ToList();
        // A streak counts days in a row with a finished session that caught or played something.
        int streak = ProgressStore.CurrentStreak(history, System.DateTime.Now), longest = ProgressStore.BestStreak(history);
        bool practisedToday = today.Any(r => r.completed && r.score > 0);
        var streaks = MetricRow("Streaks");
        Metric(streaks, "Day streak", streak.ToString(), streak == 0 ? "start today" : !practisedToday ? "keep it today"
            : streak == 1 ? "day" : "days in a row", new Color(1f, 0.88f, 0.8f));
        Metric(streaks, "Best streak", longest.ToString(), longest == 1 ? "day" : "days", new Color(1, 1, 1, 0.95f));
        var practice = MetricRow("Today's practice");
        Metric(practice, "Caught today", today.Where(r => !r.IsHandMusic).Sum(r => r.score).ToString(), "butterflies", Mint);
        Metric(practice, "Notes today", today.Where(r => r.IsHandMusic).Sum(r => r.score).ToString(), "played", Lavender);
        var effort = MetricRow("Today's effort");
        Metric(effort, "Sessions today", today.Count(r => r.completed).ToString(), "completed", new Color(1, 1, 1, 0.95f));
        Metric(effort, "Stars today", today.Sum(r => r.stars).ToString(), "earned", new Color(1f, 0.94f, 0.78f));
        ScoreChart(butterflies, "Butterfly catches");
        if (music.Count > 0) ScoreChart(music, "Hand Music notes");
        Text($"{butterflies.Sum(r => r.score)} catches  ·  {music.Sum(r => r.score)} notes  ·  {history.Count} sessions saved", 28, 70, Ink);
        Text("Saved on this device. Session lengths and settings can affect scores.", 25, 90, Ink);
    }

    private void HandMusicInstructions()
    {
        Clear("Hand Music", "Prop the phone up in landscape, hands in view.");
        Navigation("");
        string side = GameSettings.TrainedSide;
        string hand = $"YOUR {side.ToUpper()} HAND PLAYS";
        if (GameSettings.handMusicMode == HandMusicMode.OpenHand)
            Card(hand, "Open your hand to play", $"First we measure how far your hand opens. Then each opening plays the next note. Keep the hand on your {side} side.");
        else
            Card(hand, "Show the fingers in the circle", $"Each gold circle appears on your {side} side with a number. Hold that many fingers up inside it: 1 to 5 play C to G, a pinch plays A.");
        Card("FIND THE GOLD LINE", $"It marks the {side} edge", "Look for it when a song starts. A gold sweep also points to each new target.");
        Card("YOUR SESSION", $"{Minutes(GameSettings.handMusicDuration)} · {GameSettings.HandMusicModeNames[(int)GameSettings.handMusicMode]}",
            "Change the mode, side and time in Settings. Songs wait for you and repeat until the time is up.");
        Action("Open front camera", () => SceneManager.LoadScene("HandMusic"));
        Action("Back", Home, false);
    }

    private void Instructions()
    {
        Clear("Before you begin", "Use the settings agreed with your care team.");
        Navigation("");
        Card("01  GET READY", "Hold the phone in landscape", "Face forward comfortably before starting.");
        Card("02  FIND THE BUTTERFLY", "Move it into the centre ring", "Gently turn the phone to follow the butterfly. An arrow helps when the target is farther away.");
        Card("03  HOLD & CATCH", $"Keep it in the ring for {GameSettings.holdTime:0.0}s", "Each catch adds one point. You can pause using Exercise settings.");
        Action("Begin session", () => SceneManager.LoadScene(controller.arSceneName));
        Action("Back", Home, false);
    }

    private void ExerciseHero()
    {
        var art = Hero("Butterfly exercise", "Your next session", "Follow the\nbutterfly",
            $"{GameSettings.sessionDuration:0}s · {(GameSettings.trainLeftSide ? "Left" : "Right")} side", Teal);
        // Lightweight vector-like wings: decorative only, never behind text.
        Shape(art, "Wing", new Vector2(-42, 32), new Vector2(95, 115), 25, new Color(0.12f, 0.7f, 0.65f));
        Shape(art, "Wing", new Vector2(42, 32), new Vector2(95, 115), -25, new Color(0.12f, 0.7f, 0.65f));
        Shape(art, "Wing", new Vector2(-34, -46), new Vector2(75, 85), -25, new Color(0.62f, 0.85f, 0.91f));
        Shape(art, "Wing", new Vector2(34, -46), new Vector2(75, 85), 25, new Color(0.62f, 0.85f, 0.91f));
        Shape(art, "Body", Vector2.zero, new Vector2(16, 135), 0, Teal);
    }

    private void HandMusicHero()
    {
        var art = Hero("Hand Music", "Your music session", "Play\nHand Music",
            $"{GameSettings.HandMusicModeNames[(int)GameSettings.handMusicMode]} mode · {Minutes(GameSettings.handMusicDuration)}", Purple);
        // A small note behind two beamed ones, in the butterfly's style: decorative only, never behind text.
        var pale = new Color(0.78f, 0.74f, 0.98f);
        Shape(art, "Note head", new Vector2(-70, 22), new Vector2(48, 36), 20, pale);
        Shape(art, "Stem", new Vector2(-50, 58), new Vector2(10, 76), 0, pale);
        Shape(art, "Flag", new Vector2(-39, 80), new Vector2(11, 46), 34, pale);
        var bright = new Color(0.56f, 0.44f, 0.94f);
        Shape(art, "Note head", new Vector2(-22, -60), new Vector2(64, 48), 20, bright);
        Shape(art, "Note head", new Vector2(62, -36), new Vector2(64, 48), 20, bright);
        Shape(art, "Stem", new Vector2(5, 4), new Vector2(12, 124), 0, Purple);
        Shape(art, "Stem", new Vector2(89, 28), new Vector2(12, 124), 0, Purple);
        Shape(art, "Beam", new Vector2(47, 65), new Vector2(99, 26), 16, Purple);
    }

    // A white activity card: three lines of text on the left, and the returned box on the right for its illustration.
    private RectTransform Hero(string name, string eyebrow, string title, string detail, Color accent)
    {
        var card = Item(name, 340);
        ModernUI.Surface(card.AddComponent<Image>(), Color.white, true);
        var art = Box("Illustration", card.transform);
        art.anchorMin = art.anchorMax = new Vector2(0.8f, 0.5f);
        art.sizeDelta = new Vector2(210, 220);
        art.anchoredPosition = Vector2.zero;
        var words = Box("Description", card.transform);
        words.anchorMax = new Vector2(0.62f, 1);
        words.offsetMin = new Vector2(32, 24);
        words.offsetMax = new Vector2(0, -24);
        var layout = words.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        MakeText(words, eyebrow, 36, 80, accent);
        MakeText(words, title, 54, 135, Ink);
        MakeText(words, detail, 38, 80, Ink);
        return art;
    }

    private static void Shape(Transform parent, string name, Vector2 position, Vector2 size, float angle, Color color)
    {
        var shape = Box(name, parent);
        shape.anchorMin = shape.anchorMax = Vector2.one * 0.5f;
        shape.sizeDelta = size;
        shape.anchoredPosition = position;
        shape.localRotation = Quaternion.Euler(0, 0, angle);
        var image = shape.gameObject.AddComponent<Image>();
        ModernUI.Surface(image, color);
        image.raycastTarget = false;
    }

    private void Settings()
    {
        Clear("Settings", "Saved on this device. Change them any time.");
        Navigation("Settings");
        Text("BOTH EXERCISES", 36, 80, Teal);
        Action(() => "Training side: " + (GameSettings.trainLeftSide ? "Left" : "Right") + " · Tap to switch",
            () => RefreshSetting(() => GameSettings.trainLeftSide = !GameSettings.trainLeftSide), false);
        Text(() => $"Targets, cues and the playing hand are on the {GameSettings.TrainedSide} side.", 36, 110, Ink);
        Text("BUTTERFLY EXERCISE", 36, 80, Teal);
        Action(() => "Butterfly colour: " + GameSettings.ButterflyColourNames[GameSettings.butterflyColour],
            () => RefreshSetting(() => GameSettings.butterflyColour = (GameSettings.butterflyColour + 1) % GameSettings.ButterflyColourNames.Length), false);
        var swatch = Item("Selected butterfly colour", 30).AddComponent<Image>();
        ModernUI.Surface(swatch, GameSettings.ButterflyColours[GameSettings.butterflyColour]);
        refreshers.Add(() => swatch.color = GameSettings.ButterflyColours[GameSettings.butterflyColour]);
        Action(() => "Adaptive size: " + (GameSettings.adaptiveButterflySize ? "On" : "Off"),
            () => RefreshSetting(() => GameSettings.adaptiveButterflySize = !GameSettings.adaptiveButterflySize), false);
        Text("Gently grows and shrinks as the butterfly moves. Size stays steady while you hold it in the ring.", 38, 155, Ink);
        Adjust("Movement speed", () => $"{GameSettings.movementSpeed:0.##}x", () => GameSettings.movementSpeed = Mathf.Max(0.25f, GameSettings.movementSpeed - 0.25f),
            () => GameSettings.movementSpeed = Mathf.Min(2, GameSettings.movementSpeed + 0.25f));
        Adjust("Movement range", () => $"{GameSettings.movementRange:P0}", () => GameSettings.movementRange = Mathf.Max(0.3f, GameSettings.movementRange - 0.05f),
            () => GameSettings.movementRange = Mathf.Min(0.95f, GameSettings.movementRange + 0.05f));
        Adjust("Session duration", () => $"{GameSettings.sessionDuration:0}s", () => GameSettings.sessionDuration = Mathf.Max(15, GameSettings.sessionDuration - 15),
            () => GameSettings.sessionDuration = Mathf.Min(300, GameSettings.sessionDuration + 15));
        Adjust("Hold to catch", () => $"{GameSettings.holdTime:0.0}s", () => GameSettings.holdTime = Mathf.Max(0.5f, GameSettings.holdTime - 0.5f),
            () => GameSettings.holdTime = Mathf.Min(4, GameSettings.holdTime + 0.5f));
        // Star targets follow the difficulty, so show them next to the settings that change them.
        Text(() =>
        {
            var (two, three) = SessionRewards.Targets(GameSettings.sessionDuration,
                SessionRewards.ButterflyPace(GameSettings.holdTime, GameSettings.movementSpeed, GameSettings.movementRange));
            return $"Stars at these settings: 2 stars for {two}, 3 stars for {three} butterflies in {GameSettings.sessionDuration:0}s.";
        }, 36, 110, Teal);
        Text("HAND MUSIC", 36, 80, Teal);
        Action(() => "Mode: " + GameSettings.HandMusicModeNames[(int)GameSettings.handMusicMode] + " · Tap to change",
            () => RefreshSetting(() => GameSettings.handMusicMode = (HandMusicMode)(((int)GameSettings.handMusicMode + 1) % GameSettings.HandMusicModeNames.Length)), false);
        Text(() => GameSettings.handMusicMode == HandMusicMode.Fingers ? $"Reach the gold circle on the {GameSettings.TrainedSide} and hold up the finger count shown in it: 1 to 5 for C to G, a pinch for A."
            : "For a weak hand: open and close it to play, measured against its own range.", 36, 155, Ink);
        Adjust("Hand Music duration", () => Minutes(GameSettings.handMusicDuration),
            () => GameSettings.handMusicDuration = Mathf.Max(30, GameSettings.handMusicDuration - 30),
            () => GameSettings.handMusicDuration = Mathf.Min(600, GameSettings.handMusicDuration + 30));
        Adjust("Hold to play", () => $"{GameSettings.handMusicHold:0.0}s",
            () => GameSettings.handMusicHold = Mathf.Max(0.3f, Mathf.Round(GameSettings.handMusicHold * 10 - 1) / 10),
            () => GameSettings.handMusicHold = Mathf.Min(1.5f, Mathf.Round(GameSettings.handMusicHold * 10 + 1) / 10));
        Adjust("Gold circle size", () => $"{GameSettings.handMusicCircle:P0}",
            () => GameSettings.handMusicCircle = Mathf.Max(0.5f, Mathf.Round(GameSettings.handMusicCircle * 10 - 1) / 10),
            () => GameSettings.handMusicCircle = Mathf.Min(1, Mathf.Round(GameSettings.handMusicCircle * 10 + 1) / 10));
        Text(() =>
        {
            var (two, three) = SessionRewards.Targets(GameSettings.handMusicDuration,
                SessionRewards.HandMusicPace(GameSettings.handMusicMode, GameSettings.handMusicHold, GameSettings.handMusicCircle));
            return $"Stars at these settings: 2 stars for {two}, 3 stars for {three} notes in {Minutes(GameSettings.handMusicDuration)}.";
        }, 36, 110, Teal);
        Action("Done", Home);
        Action("Debug: reset saved statistics", ConfirmStatisticsReset, false);
    }

    private void ConfirmStatisticsReset()
    {
        Clear("Reset statistics?", "Clears saved scores and history on this device.");
        Navigation("Settings");
        Card("DEBUG TOOL", "Start with a clean record", "Today's totals, best score and the progress chart will reset. Exercise settings will stay the same.");
        Action("Cancel, keep my statistics", Settings);
        Action("Yes, reset saved statistics", () =>
        {
            ProgressStore.ResetStatistics();
            controller.RefreshAll();
            Home();
        }, false);
    }

    private void History()
    {
        Clear("Your progress", "Your recent sessions on this device.");
        Navigation("Progress");
        var history = ProgressStore.LoadHistory();
        if (history.Count == 0)
            Card("READY WHEN YOU ARE", "No saved sessions yet", "Complete an exercise to begin your history. Sessions ended early are also recorded.");
        else
        {
            var butterflies = history.Where(r => !r.IsHandMusic).ToList();
            var music = history.Where(r => r.IsHandMusic).ToList();
            if (butterflies.Count > 0) ScoreChart(butterflies, "Butterfly catches");
            if (music.Count > 0) ScoreChart(music, "Hand Music notes");
            int streak = ProgressStore.CurrentStreak(history, System.DateTime.Now), longest = ProgressStore.BestStreak(history);
            Text($"Streak: {streak} day{(streak == 1 ? "" : "s")} in a row  ·  Best: {longest} day{(longest == 1 ? "" : "s")}", 28, 70, Ink);
            var ordered = history.AsEnumerable().Reverse().ToList();
            page = Mathf.Clamp(page, 0, (ordered.Count - 1) / 6);
            foreach (var result in ordered.Skip(page * 6).Take(6))
            {
                string eyebrow = result.dateLocal + (result.completed ? " · Completed" : " · Ended early");
                string timing = result.score > 0 ? $"{result.avgReactionTime:0.0}s" : "-";
                if (result.IsHandMusic)
                    Card(eyebrow, $"{result.score} notes played",
                        $"Hand Music · {result.mode} · {result.neglectedSide} side\n{result.durationSeconds:0}s active · {timing} per note" +
                        (result.reach > 0 ? $"\nFarthest reach: {result.reach:P0} toward the {(result.neglectedSide ?? "").ToLower()}" : ""),
                        stars: result.stars);
                else
                    Card(eyebrow, $"{result.score} butterflies caught",
                        $"{result.neglectedSide} side · {result.durationSeconds:0}s active\nAverage catch time: {timing}", stars: result.stars);
            }
            Text($"Page {page + 1} of {(ordered.Count + 5) / 6}", 28, 55, Ink);
            if (page > 0) Action("Newer sessions", () => { page--; History(); }, false);
            if ((page + 1) * 6 < ordered.Count) Action("Older sessions", () => { page++; History(); }, false);
        }
        Text("Catches, notes and times describe your exercise sessions; they are not a clinical assessment.", 28, 120, Ink);
        Action("Back to home", Home);
    }

    private void Adjust(string title, System.Func<string> value, System.Action minus, System.Action plus)
    {
        Text(() => title + ": " + value(), 34, 65, Ink);
        var row = Item("Adjustment", 130);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        MakeButton(row.transform, "-  Decrease", () => RefreshSetting(minus), false);
        MakeButton(row.transform, "+  Increase", () => RefreshSetting(plus), false);
    }

    // The page stays as it is (and where it is scrolled); only the labels showing settings change.
    private void RefreshSetting(System.Action change)
    {
        change();
        GameSettings.Save();
        foreach (var refresh in refreshers) refresh();
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

    private Transform MetricRow(string name)
    {
        var row = Item(name, 330);
        var columns = row.AddComponent<HorizontalLayoutGroup>();
        columns.spacing = 24;
        columns.childControlWidth = true;
        columns.childControlHeight = true;
        columns.childForceExpandWidth = true;
        return row.transform;
    }

    private static string Minutes(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        return total < 60 ? $"{total}s" : total % 60 == 0 ? $"{total / 60} min" : $"{total / 60} min {total % 60}s";
    }

    private void ScoreChart(System.Collections.Generic.List<SessionResult> history, string chartTitle)
    {
        var card = Item(chartTitle, 340);
        ModernUI.Surface(card.AddComponent<Image>(), Color.white, true);
        var title = Box("Chart title", card.transform);
        title.anchorMin = new Vector2(0, 1);
        title.offsetMin = new Vector2(28, -65);
        title.offsetMax = new Vector2(-28, -15);
        var heading = title.gameObject.AddComponent<TextMeshProUGUI>();
        heading.font = ModernUI.ReadableFont;
        heading.text = chartTitle;
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
            value.offsetMax = new Vector2(0, 97 + height);
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
            label.offsetMax = new Vector2(0, 44);
            var caption = label.gameObject.AddComponent<TextMeshProUGUI>();
            caption.font = heading.font;
            caption.text = "#" + (history.Count - samples.Count + i + 1);
            caption.fontSize = 32;
            caption.color = Teal;
            caption.alignment = TextAlignmentOptions.Center;
            caption.raycastTarget = false;
        }
    }

    private void Card(string eyebrow, string value, string detail, Color? tint = null, int stars = 0)
    {
        float detailHeight = detail.Split('\n').Length > 2 ? 190 : 145;
        var card = Item("Progress card", 250 + detailHeight + (stars > 0 ? 64 : 0));
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
        if (stars > 0) StarRow(card.transform, stars);
        MakeText(card.transform, detail, 38, detailHeight, Ink);
    }

    // Stars a session earned, drawn like the results screen's rather than with font glyphs.
    private static void StarRow(Transform parent, int stars)
    {
        var row = new GameObject("Stars", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 56;
        for (int i = 0; i < 3; i++)
        {
            var star = Box("Star", row.transform);
            star.anchorMin = star.anchorMax = new Vector2(0, 0.5f);
            star.sizeDelta = new Vector2(52, 52);
            star.anchoredPosition = new Vector2(26 + i * 60, 0);
            var graphic = star.gameObject.AddComponent<StarGraphic>();
            graphic.raycastTarget = false;
            graphic.color = i < stars ? new Color(1f, 0.8f, 0.15f) : new Color(0.85f, 0.9f, 0.9f);
            if (i >= stars) continue;
            // Gold alone is faint on white; a darker edge keeps earned stars easy to see.
            var edge = star.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.78f, 0.52f, 0f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }

    private void Action(string title, UnityEngine.Events.UnityAction action, bool primary = true, Color? accent = null)
        => MakeButton(content, title, action, primary, accent);
    private void Text(string text, int size, float height, Color color) => MakeText(content, text, size, height, color);
    // Labels that show settings, kept up to date by RefreshSetting.
    private void Action(System.Func<string> title, UnityEngine.Events.UnityAction action, bool primary = true)
        => Live(MakeButton(content, title(), action, primary), title);
    private void Text(System.Func<string> text, int size, float height, Color color) => Live(MakeText(content, text(), size, height, color), text);
    private void Live(TMP_Text label, System.Func<string> text) => refreshers.Add(() => label.text = text());

    private GameObject Item(string name, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        obj.transform.SetParent(content, false);
        obj.GetComponent<LayoutElement>().preferredHeight = height;
        return obj;
    }

    // Sized by its text, so the fixed header can centre one or two description lines.
    private static TMP_Text HeaderText(Transform parent, int size, Color color)
    {
        var go = new GameObject("Header line", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = ModernUI.ReadableFont;
        label.fontSize = size;
        if (size >= 42) label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }

    private static TMP_Text MakeText(Transform parent, string text, int size, float height, Color color)
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
        return label;
    }

    // The accent (Teal unless given) fills a primary button and colours a secondary button's label.
    private static TMP_Text MakeButton(Transform parent, string title, UnityEngine.Events.UnityAction action, bool primary, Color? accent = null)
    {
        var tone = accent ?? Teal;
        var go = new GameObject(title, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 140;
        ModernUI.Surface(go.GetComponent<Image>(), primary ? tone : new Color(1, 1, 1, 0.9f), primary);
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
        label.color = primary ? Color.white : tone;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
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

    private void OnDestroy()
    {
        Application.targetFrameRate = previousFrameRate;
        if (root != null) Destroy(root);
    }
}
