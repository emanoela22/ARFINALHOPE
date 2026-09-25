using UnityEngine;

public enum HandMusicMode { Fingers, OpenHand }

public static class GameSettings
{
    // The defaults also anchor the star targets: a person with neglect should earn 3 stars at these.
    public const float DefaultHoldTime = 1f, DefaultMovementSpeed = 1f, DefaultMovementRange = .5f;
    public const float DefaultHandMusicHold = .6f, DefaultHandMusicCircle = .8f;
    public static bool trainLeftSide = true;
    public static float sessionDuration = 30f;
    public static float holdTime = DefaultHoldTime;
    public static float movementRange = DefaultMovementRange;
    public static float movementSpeed = DefaultMovementSpeed;
    public static bool adaptiveButterflySize = false;
    public static int butterflyColour = 0;
    public static HandMusicMode handMusicMode = HandMusicMode.Fingers;
    public static float handMusicDuration = 120f;
    public static float handMusicHold = DefaultHandMusicHold;
    // Gold circle size as a share of the largest one (1 = the original size).
    public static float handMusicCircle = DefaultHandMusicCircle;
    public static readonly string[] ButterflyColourNames = { "Original blue", "Gold", "Pink", "Purple", "Mint", "Orange" };
    public static readonly Color[] ButterflyColours = {
        new Color(0.1f, 0.7f, 1f), new Color(1f, 0.85f, 0.15f),
        new Color(1f, 0.35f, 0.65f), new Color(0.7f, 0.4f, 1f),
        new Color(0.2f, 1f, 0.65f), new Color(1f, 0.5f, 0.12f)
    };
    public static readonly string[] HandMusicModeNames = { "Fingers", "Open hand" };

    // "left" or "right", for instructions that name the trained side.
    public static string TrainedSide => trainLeftSide ? "left" : "right";
    public static string OtherSide => trainLeftSide ? "right" : "left";

    [System.Serializable]
    private class Snapshot
    {
        public bool trainLeftSide;
        public float sessionDuration, holdTime, movementRange, movementSpeed;
        public bool adaptiveButterflySize;
        public int butterflyColour;
        public HandMusicMode handMusicMode;
        public float handMusicDuration, handMusicHold, handMusicCircle;
    }
    private const string SettingsKey = "NEGLECT_SETTINGS_V1";

    // Settings survive restarts. Loaded before the first scene, so every screen sees them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        if (!PlayerPrefs.HasKey(SettingsKey)) return;
        var saved = Capture();
        try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(SettingsKey), saved); }
        catch (System.Exception) { return; }
        // Fields missing from an older snapshot keep their current values; out-of-range ones are clamped.
        trainLeftSide = saved.trainLeftSide;
        sessionDuration = Mathf.Clamp(saved.sessionDuration, 15, 300);
        holdTime = Mathf.Clamp(saved.holdTime, 0.5f, 4);
        movementRange = Mathf.Clamp(saved.movementRange, 0.3f, 0.95f);
        movementSpeed = Mathf.Clamp(saved.movementSpeed, 0.25f, 2);
        adaptiveButterflySize = saved.adaptiveButterflySize;
        butterflyColour = Mathf.Clamp(saved.butterflyColour, 0, ButterflyColours.Length - 1);
        // Saved as a number. Earlier builds had Reach (0), Open hand (1) and Fingers (2); anything but
        // Open hand now plays in Fingers mode.
        handMusicMode = saved.handMusicMode == HandMusicMode.OpenHand ? HandMusicMode.OpenHand : HandMusicMode.Fingers;
        handMusicDuration = Mathf.Clamp(saved.handMusicDuration, 30, 600);
        handMusicHold = Mathf.Clamp(saved.handMusicHold, 0.3f, 1.5f);
        handMusicCircle = Mathf.Clamp(saved.handMusicCircle, 0.5f, 1);
    }

    public static void Save()
    {
        PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(Capture()));
        PlayerPrefs.Save();
    }

    private static Snapshot Capture() => new Snapshot
    {
        trainLeftSide = trainLeftSide,
        sessionDuration = sessionDuration,
        holdTime = holdTime,
        movementRange = movementRange,
        movementSpeed = movementSpeed,
        adaptiveButterflySize = adaptiveButterflySize,
        butterflyColour = butterflyColour,
        handMusicMode = handMusicMode,
        handMusicDuration = handMusicDuration,
        handMusicHold = handMusicHold,
        handMusicCircle = handMusicCircle
    };
}
