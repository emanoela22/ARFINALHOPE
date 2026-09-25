using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

// Unity -batchmode -quit -projectPath <project> -executeMethod MenuCheck.Run [-menuPreview <folder>]
public static class MenuCheck
{
    // Canvas of a 19.5:9 phone (the menu scales to 1080 wide) with notch and home-indicator insets.
    private static readonly Vector2 Phone = new Vector2(1080, 2340);
    private static readonly Rect PhoneSafe = new Rect(0, 94f / 2340, 1, 1 - (94f + 130f) / 2340);
    private static readonly string[] Pages = { "Home", "History", "Settings", "Instructions", "HandMusicInstructions", "ConfirmStatisticsReset" };

    public static void Run()
    {
        string folder = CheckTools.Argument("-menuPreview");
        var problems = new List<string>();
        CheckTools.KeepSavedData(() =>
        {
            GameSettings.trainLeftSide = true;
            GameSettings.sessionDuration = 30;
            GameSettings.holdTime = GameSettings.DefaultHoldTime;
            GameSettings.movementSpeed = GameSettings.DefaultMovementSpeed;
            GameSettings.movementRange = GameSettings.DefaultMovementRange;
            GameSettings.handMusicMode = HandMusicMode.Fingers;
            GameSettings.handMusicDuration = 120;
            GameSettings.handMusicHold = GameSettings.DefaultHandMusicHold;
            GameSettings.handMusicCircle = GameSettings.DefaultHandMusicCircle;
            SeedHistory();
            EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
            var host = UnityEngine.Object.FindFirstObjectByType<MenuUIController>();
            Require(host != null, "menu controller in MenuScene");
            var menu = host.gameObject.AddComponent<ClinicalMenu>();
            Call(menu, "Start");
            var canvas = GameObject.Find("Clinical menu").GetComponent<Canvas>();
            CheckTools.UseWorldSpace(canvas, Phone);
            var safe = (RectTransform)canvas.transform.Find("Safe area");
            safe.anchorMin = PhoneSafe.min;
            safe.anchorMax = PhoneSafe.max;
            CheckHeader(safe);
            foreach (string page in Pages) problems.AddRange(CheckPage(menu, canvas, page, folder));
            CheckStreaksShown(menu);
            CheckSettingsAreSaved(menu);
        });
        Require(problems.Count == 0, string.Join("\n", problems));
        Debug.Log("MENU_CHECK_PASS: fixed header, every page without overflow, settings saved.");
    }

    // The header stays out of the scroll content, and the scroll area starts below it.
    private static void CheckHeader(RectTransform safe)
    {
        Canvas.ForceUpdateCanvases();
        var header = (RectTransform)safe.Find("Page header");
        var viewport = (RectTransform)safe.Find("Scroll viewport");
        Require(header != null && viewport != null && header.GetComponentInParent<ScrollRect>() == null, "header outside the scrolling page");
        var headerCorners = new Vector3[4];
        var viewportCorners = new Vector3[4];
        header.GetWorldCorners(headerCorners);
        viewport.GetWorldCorners(viewportCorners);
        Require(viewportCorners[1].y <= headerCorners[0].y, "scroll area starts below the header");
        Require(viewport.GetComponent<RectMask2D>() != null, "scrolled content is clipped at the header");
    }

    private static IEnumerable<string> CheckPage(ClinicalMenu menu, Canvas canvas, string page, string folder)
    {
        Show(menu, page);
        var problems = CheckTools.OverflowingText(canvas).Select(text => page + " text overflows: " + text).ToList();
        var title = (TMP_Text)Get(menu, "headerTitle");
        var subtitle = (TMP_Text)Get(menu, "headerSubtitle");
        if (title.textInfo.lineCount != 1) problems.Add(page + " title wraps: " + title.text);
        if (subtitle.textInfo.lineCount > 2) problems.Add(page + " description is longer than two lines: " + subtitle.text);
        var headerText = (RectTransform)title.transform.parent;
        if (LayoutUtility.GetPreferredHeight(headerText) > headerText.rect.height) problems.Add(page + " header text does not fit");
        // Anything that is not maskable ignores the scroll clip and would slide over the header.
        var unclipped = ((Transform)Get(menu, "content")).GetComponentsInChildren<Graphic>(true)
            .Where(graphic => !(graphic is MaskableGraphic)).Select(graphic => graphic.name).Distinct().ToList();
        if (unclipped.Count > 0) problems.Add(page + " not clipped when scrolled: " + string.Join(", ", unclipped));
        if (folder == null) return problems;
        // Two half-size shots side by side: the top of the page, then scrolled to the bottom.
        var scroll = (ScrollRect)Get(menu, "scroll");
        var image = new Texture2D(1080, 1170, TextureFormat.RGB24, false);
        scroll.verticalNormalizedPosition = 1;
        CheckTools.Render(Phone, image, 0, 0, 540, 1170);
        scroll.verticalNormalizedPosition = 0;
        CheckTools.Render(Phone, image, 540, 0, 540, 1170);
        scroll.verticalNormalizedPosition = 1;
        CheckTools.SavePng(image, Path.Combine(folder, "menu-" + page + ".png"));
        return problems;
    }

    // Taps Settings buttons like a patient would, then reloads settings as on the next app start.
    private static void CheckSettingsAreSaved(ClinicalMenu menu)
    {
        Show(menu, "Settings");
        Require(PageText(menu).Contains("2 stars for 3, 3 stars for 5 butterflies in 30s") &&
            PageText(menu).Contains("2 stars for 8, 3 stars for 16 notes in 2 min."), "star targets shown for the default settings");
        var firstItem = ((Transform)Get(menu, "content")).GetChild(0);
        Tap(Find(menu, "Training side: Left · Tap to switch"));
        Tap(Find(menu, "Mode: Fingers · Tap to change"));
        Tap(Row(menu, 4, "+  Increase")); // Hand Music duration
        Tap(Row(menu, 6, "-  Decrease")); // gold circle size
        // A harder butterfly setting lowers its star target right on the page.
        Tap(Row(menu, 1, "+  Increase"));
        Tap(Row(menu, 1, "+  Increase")); // movement range 50% -> 60%
        Require(PageText(menu).Contains("3 stars for 4 butterflies"), "harder settings need fewer catches");
        Require(PageText(menu).Contains("Training side: Right · Tap to switch") && PageText(menu).Contains("Mode: Open hand · Tap to change") &&
            PageText(menu).Contains("Hand Music duration: 2 min 30s") && PageText(menu).Contains($"Gold circle size: {.7f:P0}"), "labels follow each tap");
        Require(((Transform)Get(menu, "content")).GetChild(0) == firstItem, "taps update the page in place instead of rebuilding it");
        Require(!GameSettings.trainLeftSide && GameSettings.handMusicMode == HandMusicMode.OpenHand &&
            Mathf.Approximately(GameSettings.handMusicDuration, 150) && Mathf.Approximately(GameSettings.handMusicCircle, .7f) &&
            Mathf.Approximately(GameSettings.movementRange, .6f), "taps change the side, mode, duration, circle size and range");
        GameSettings.trainLeftSide = true;
        GameSettings.handMusicMode = HandMusicMode.Fingers;
        GameSettings.handMusicDuration = 30;
        GameSettings.handMusicCircle = 1;
        GameSettings.movementRange = .3f;
        GameSettings.Load();
        Require(!GameSettings.trainLeftSide && GameSettings.handMusicMode == HandMusicMode.OpenHand &&
            Mathf.Approximately(GameSettings.handMusicDuration, 150) && Mathf.Approximately(GameSettings.handMusicCircle, .7f) &&
            Mathf.Approximately(GameSettings.movementRange, .6f), "settings survive a restart");
    }

    // The seeded history practises on six days in a row, ending today.
    private static void CheckStreaksShown(ClinicalMenu menu)
    {
        Show(menu, "Home");
        Require(PageText(menu).Contains("Day streak\n6\ndays in a row") && PageText(menu).Contains("Best streak\n6\ndays"),
            "Home shows the day streak and best streak");
        Show(menu, "History");
        Require(PageText(menu).Contains("Streak: 6 days in a row  ·  Best: 6 days"), "Progress shows the streak");
    }

    private static string PageText(ClinicalMenu menu) =>
        string.Join("\n", ((Transform)Get(menu, "content")).GetComponentsInChildren<TMP_Text>().Select(label => label.text));

    private static Button Find(ClinicalMenu menu, string name)
    {
        var button = ((Transform)Get(menu, "content")).Find(name);
        Require(button != null, "button " + name);
        return button.GetComponent<Button>();
    }

    // A +/- button in the index-th adjustment row of the Settings page.
    private static Button Row(ClinicalMenu menu, int index, string name)
    {
        var rows = ((Transform)Get(menu, "content")).Cast<Transform>().Where(child => child.name == "Adjustment").ToList();
        Require(index < rows.Count && rows[index].Find(name) != null, $"adjustment {index} {name}");
        return rows[index].Find(name).GetComponent<Button>();
    }

    // Settings taps change the page's labels in place, as on a phone.
    private static void Tap(Button button) => button.onClick.Invoke();

    private static void Show(ClinicalMenu menu, string page)
    {
        Empty(menu);
        Call(menu, page);
        Canvas.ForceUpdateCanvases();
    }

    private static void Empty(ClinicalMenu menu)
    {
        foreach (string list in new[] { "content", "navigation" })
            foreach (var child in ((Transform)Get(menu, list)).Cast<Transform>().ToList())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    // A butterfly session on each of the last five days (the oldest saved before stars existed), then
    // today's mix of both exercises: a six-day streak.
    private static void SeedHistory()
    {
        ProgressStore.ResetStatistics();
        string today = ProgressStore.TodayDateLocal();
        int[] scores = { 4, 6, 5, 7, 9, 3, 8, 10 };
        for (int i = 0; i < scores.Length; i++)
            ProgressStore.Record(new SessionResult
            {
                sessionId = "seed" + i,
                dateLocal = (i < 5 ? DateTime.Now.AddDays(i - 5).ToString("yyyy-MM-dd") : today) + " 10:3" + i,
                score = scores[i],
                avgReactionTime = 3.2f - i * 0.1f,
                neglectedSide = "Left",
                durationSeconds = i == 5 ? 12.4f : 30.02f,
                completed = i != 5,
                stars = i < 3 ? 0 : 1 + i % 3
            });
        string[] modes = { "Fingers", "Open hand", "Fingers" };
        for (int i = 0; i < modes.Length; i++)
            ProgressStore.Record(new SessionResult
            {
                sessionId = "music" + i,
                dateLocal = today + " 11:0" + i,
                exercise = SessionResult.HandMusicExercise,
                mode = modes[i],
                score = 18 + i * 7,
                avgReactionTime = 4.1f - i * 0.4f,
                neglectedSide = "Left",
                durationSeconds = 120.02f,
                reach = i == 0 ? 0.6f : 0,
                completed = true,
                stars = i + 1
            });
    }

    private static object Get(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    private static void Require(bool ok, string name) { if (!ok) throw new Exception("Menu check failed: " + name); }
}
