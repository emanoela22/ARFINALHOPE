using UnityEngine;

public static class GameSettings
{
    public static bool trainLeftSide = true;
    public static float sessionDuration = 30f;
    public static float holdTime = 1.0f;
    public static float movementRange = 0.5f;
    public static float movementSpeed = 1f;
    public static bool adaptiveButterflySize = false;
    public static int butterflyColour = 0;
    public static readonly string[] ButterflyColourNames = { "Original blue", "Gold", "Pink", "Purple", "Mint", "Orange" };
    public static readonly Color[] ButterflyColours = {
        new Color(0.1f, 0.7f, 1f), new Color(1f, 0.85f, 0.15f),
        new Color(1f, 0.35f, 0.65f), new Color(0.7f, 0.4f, 1f),
        new Color(0.2f, 1f, 0.65f), new Color(1f, 0.5f, 0.12f)
    };
}
