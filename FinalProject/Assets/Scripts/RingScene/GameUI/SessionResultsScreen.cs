using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// End-of-session results, built at runtime like ExerciseControls. SessionManager shows it.
public class SessionResultsScreen : MonoBehaviour
{
    // Same yellow as the exercise's direction arrow; about 5.6:1 contrast on Teal.
    private static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
    // Opaque, because translucent white turns grey on Teal in linear colour space.
    private static readonly Color EmptyStar = Color.Lerp(ClinicalMenu.Teal, ClinicalMenu.Mint, 0.3f);
    private const float BandHeight = 375f, CountSeconds = 0.8f, FirstStarAt = 0.45f, StarInterval = 0.35f,
        DetailsAt = 0.9f, ButtonsAt = 1.2f;

    private SessionSummary summary;
    private System.Action starSound;
    private RectTransform safeArea, burstPoint, badge;
    private readonly RectTransform[] earnedStars = new RectTransform[3];
    private TMP_Text countText;
    private TMP_Text[] details;
    private CanvasGroup buttons;
    private ConfettiGraphic confetti;
    private float elapsed;
    private int shownCount = -1, starsPlayed;
    private bool celebrated;

    public static SessionResultsScreen Show(SessionSummary summary, string menuSceneName,
        System.Action playAgain, System.Action starSound)
    {
        var root = new GameObject("Session results", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the exercise controls (50)
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // Opaque like the settings overlay, which also stops taps reaching the exercise.
        root.AddComponent<Image>().color = ClinicalMenu.Paper;
        var screen = root.AddComponent<SessionResultsScreen>();
        screen.Build(summary, menuSceneName, playAgain, starSound);
        return screen;
    }

    private void Build(SessionSummary summary, string menuSceneName, System.Action playAgain, System.Action starSound)
    {
        this.summary = summary;
        this.starSound = starSound;
        var top = new Vector2(0.5f, 1);
        safeArea = Rect("Safe Area", transform, Vector2.zero, Vector2.zero, Vector2.zero);
        safeArea.anchorMax = Vector2.one;

        var band = Rect("Teal band", safeArea, Vector2.one, Vector2.zero, Vector2.zero);
        band.anchorMin = new Vector2(0, 1);
        // Reaches past the safe area so the colour meets the screen edges.
        band.offsetMin = new Vector2(-400, -BandHeight);
        band.offsetMax = new Vector2(400, 400);
        band.gameObject.AddComponent<Image>().color = ClinicalMenu.Teal;
        // Behind the text, stars and buttons, so every word stays readable while it falls.
        var confettiArea = Rect("Confetti", safeArea, Vector2.zero, Vector2.zero, Vector2.zero);
        confettiArea.anchorMax = Vector2.one;
        confetti = confettiArea.gameObject.AddComponent<ConfettiGraphic>();
        confetti.raycastTarget = false;
        Label(safeArea, summary.result.completed ? "SESSION COMPLETE" : "SESSION ENDED EARLY", top,
            new Vector2(0, -58), new Vector2(1200, 56), 36, ClinicalMenu.Mint);
        Label(safeArea, Title(summary), top, new Vector2(0, -128), new Vector2(1200, 84), 64, Color.white);
        for (int i = 0; i < 3; i++)
        {
            // Empty slots stay visible and earned stars pop in over them; the middle one sits higher.
            float size = i == 1 ? 160 : 132;
            var position = new Vector2((i - 1) * 190, i == 1 ? -265 : -278);
            var slot = Star(safeArea, position, size, EmptyStar);
            if (i == 1) burstPoint = slot;
            if (i >= summary.stars) continue;
            earnedStars[i] = Star(safeArea, position, size, Gold);
            var shadow = earnedStars[i].gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.2f);
            shadow.effectDistance = new Vector2(0, -6);
        }

        // Centred between the band and the buttons, whatever the screen height.
        var info = Rect("Session details", safeArea, Vector2.zero, Vector2.zero, Vector2.zero);
        info.anchorMax = Vector2.one;
        info.offsetMin = new Vector2(80, 140);
        info.offsetMax = new Vector2(-80, -BandHeight - 10);
        var layout = info.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        countText = Line(info, "", 140, 44, ClinicalMenu.Ink);
        string best = summary.newPersonalBest ? "New personal best!" : summary.matchedPersonalBest ? "You matched your best!" : null;
        string streak = !summary.streakGrew ? null : summary.streak == 1 ? "Streak started!" : $"{summary.streak}-day streak!";
        if (best != null || streak != null)
        {
            var badgeRow = new GameObject("Badge row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            badgeRow.transform.SetParent(info, false);
            badgeRow.GetComponent<LayoutElement>().preferredHeight = 84;
            var pills = badgeRow.GetComponent<HorizontalLayoutGroup>();
            pills.childAlignment = TextAnchor.MiddleCenter;
            pills.spacing = 24;
            pills.childControlWidth = pills.childControlHeight = pills.childForceExpandWidth = pills.childForceExpandHeight = false;
            badge = (RectTransform)badgeRow.transform;
            if (best != null) Pill(badge, best, summary.newPersonalBest ? Gold : ClinicalMenu.Mint, ClinicalMenu.Ink);
            if (streak != null) Pill(badge, streak, ClinicalMenu.Teal, Color.white);
        }
        details = new[]
        {
            Line(info, ComparisonText(summary), 56, 36, ClinicalMenu.Ink),
            Line(info, GoalText(summary), 56, 36, ClinicalMenu.Teal)
        };

        var buttonRow = Rect("Buttons", safeArea, new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(980, 104));
        buttons = buttonRow.gameObject.AddComponent<CanvasGroup>();
        Button(buttonRow, "Play again", new Vector2(-250, 0), new Vector2(460, 104), false, () =>
        {
            Destroy(gameObject);
            playAgain();
        });
        Button(buttonRow, "Back to menu", new Vector2(250, 0), new Vector2(460, 104), true,
            () => SceneManager.LoadScene(menuSceneName));
        Tick(0);
    }

    private void Update()
    {
        Rect safe = Screen.safeArea;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        // Capped so a slow first frame does not skip the animation.
        Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
    }

    // Count up, pop the stars, then celebrate. Public so the editor check can render set moments.
    public void Tick(float deltaTime)
    {
        elapsed += deltaTime;
        float counting = Mathf.Clamp01(elapsed / CountSeconds);
        int count = Mathf.RoundToInt(summary.result.score * (1 - Mathf.Pow(1 - counting, 3)));
        if (count != shownCount)
        {
            shownCount = count;
            countText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ClinicalMenu.Teal)}><size=110>{count}</size></color>  " +
                Noun(summary.result, count) + " " + Verb(summary.result);
        }
        for (int i = 0; i < summary.stars; i++)
        {
            float age = elapsed - (FirstStarAt + StarInterval * i);
            if (age >= 0 && starsPlayed == i)
            {
                starsPlayed++;
                starSound?.Invoke();
            }
            earnedStars[i].localScale = Vector3.one * Pop(age);
        }
        // Celebrate once the last earned star has landed.
        float party = elapsed - (FirstStarAt + StarInterval * summary.stars);
        if (!celebrated && party >= 0)
        {
            celebrated = true;
            Celebrate();
        }
        if (badge != null)
            badge.localScale = Vector3.one * Pop(party) * (1 + 0.03f * Mathf.Sin(Mathf.Max(0, party - 0.35f) * 4));
        float reveal = Mathf.Clamp01((elapsed - DetailsAt) / 0.4f);
        foreach (var line in details) line.alpha = reveal;
        // The buttons wait a moment so a stray tap from the exercise cannot skip the results.
        buttons.alpha = Mathf.Clamp01((elapsed - ButtonsAt) / 0.3f);
        buttons.interactable = buttons.blocksRaycasts = elapsed >= ButtonsAt;
        confetti.Tick(deltaTime);
    }

    private void Celebrate()
    {
        // Stars alone mark a session without catches; confetti is for catching butterflies.
        if (summary.result.score == 0) return;
        var palette = GameSettings.ButterflyColours;
        confetti.Burst(confetti.rectTransform.InverseTransformPoint(burstPoint.position), 30 + 30 * summary.stars, palette);
        if (summary.newPersonalBest) confetti.Rain(90, palette);
    }

    // Grows from nothing with a small overshoot (ease-out-back).
    private static float Pop(float age)
    {
        if (age <= 0) return 0;
        float x = Mathf.Min(age / 0.35f, 1) - 1;
        return 1 + 2.70158f * x * x * x + 1.70158f * x * x;
    }

    // Only a session that scored is congratulated.
    private static string Title(SessionSummary summary) =>
        summary.stars >= 3 ? "Wonderful focus!" : summary.stars == 2 ? "Great work!" : summary.stars == 1 ? "Well done for practising!"
        : summary.result.IsHandMusic ? "No notes this time" : "No butterflies this time";

    // What a session counts: butterflies caught, or Hand Music notes played.
    private static string Noun(SessionResult result, int count) => result.IsHandMusic
        ? (count == 1 ? "note" : "notes") : (count == 1 ? "butterfly" : "butterflies");

    private static string Verb(SessionResult result) => result.IsHandMusic ? "played" : "caught";

    public static string ComparisonText(SessionSummary summary)
    {
        var result = summary.result;
        var last = summary.lastSession;
        if (last == null) return "Your first session. This is your score to beat.";
        string text;
        if (SessionRewards.Comparable(result, last))
        {
            int gain = result.score - last.score;
            // Improvements are celebrated; a lower score is stated plainly, never as a minus.
            text = gain > 0 ? $"{gain} more than last time ({last.score})"
                : gain == 0 ? $"Same as last time ({last.score})"
                : $"Last session: {last.score} {Verb(last)}";
        }
        else
            text = $"Last session: {last.score} {Verb(last)} " + (!last.completed ? "(ended early)"
                : last.IsHandMusic && last.mode != result.mode ? $"in {last.mode} mode" : $"in {last.durationSeconds:0}s");
        if (result.score == 0) return text;
        string unit = result.IsHandMusic ? "note" : "catch";
        float quicker = last.score > 0 ? last.avgReactionTime - result.avgReactionTime : 0;
        return text + (quicker >= 0.1f ? $"  ·  {quicker:0.0}s quicker per {unit}" : $"  ·  {result.avgReactionTime:0.0}s per {unit}");
    }

    public static string GoalText(SessionSummary summary)
    {
        var result = summary.result;
        if (summary.stars >= 3) return "All three stars: top pace for your settings!";
        int next = summary.NextStarCatches, stars = summary.stars + 1;
        return $"{(result.IsHandMusic ? "Play" : "Catch")} {next} {Noun(result, next)} next time for {stars} star{(stars == 1 ? "" : "s")}";
    }

    // A rounded label sized to its text, for the personal-best and streak badges.
    private static void Pill(Transform row, string text, Color fill, Color ink)
    {
        var pill = Rect(text, row, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 76));
        ModernUI.Surface(pill.gameObject.AddComponent<Image>(), fill, true);
        var label = Label(pill, text, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 70), 40, ink);
        float width = label.GetPreferredValues(text).x + 40;
        label.rectTransform.sizeDelta = new Vector2(width, 70);
        pill.sizeDelta = new Vector2(width + 40, 76);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Label(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size,
        int fontSize, Color color)
    {
        var label = Rect(text.Length > 0 ? text : "Label", parent, anchor, position, size).gameObject
            .AddComponent<TextMeshProUGUI>();
        label.font = ModernUI.ReadableFont;
        label.text = text;
        label.fontSize = Mathf.Max(36, fontSize);
        if (fontSize >= 40) label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_Text Line(Transform parent, string text, float height, int fontSize, Color color)
    {
        var label = Label(parent, text, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, color);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return label;
    }

    private static RectTransform Star(Transform parent, Vector2 position, float size, Color color)
    {
        var rect = Rect("Star", parent, new Vector2(0.5f, 1), position, new Vector2(size, size));
        var star = rect.gameObject.AddComponent<StarGraphic>();
        star.color = color;
        star.raycastTarget = false;
        return rect;
    }

    // Primary and secondary styles match ClinicalMenu's buttons.
    private static void Button(Transform parent, string text, Vector2 position, Vector2 size, bool primary,
        UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(text, parent, new Vector2(0.5f, 0.5f), position, size);
        var background = rect.gameObject.AddComponent<Image>();
        ModernUI.Surface(background, primary ? ClinicalMenu.Teal : new Color(1, 1, 1, 0.9f), primary);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);
        Label(rect, text, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24, 8), 40,
            primary ? Color.white : ClinicalMenu.Teal);
    }
}
