using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

// Shared by the batch-mode checks: saved-data sandboxing and headless UI previews.
public static class CheckTools
{
    // Value after a command-line flag such as -menuPreview <folder>, or null.
    public static string Argument(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    // Runs body against the real ProgressStore and GameSettings, then restores the editor's saved
    // statistics, saved settings and in-memory settings exactly as they were.
    public static void KeepSavedData(Action body)
    {
        var keys = new[] { typeof(ProgressStore), typeof(GameSettings) }
            .SelectMany(type => type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()).ToList();
        var saved = keys.Where(PlayerPrefs.HasKey).ToDictionary(key => key, key => PlayerPrefs.GetString(key));
        var settings = typeof(GameSettings).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => !field.IsInitOnly && !field.IsLiteral).ToDictionary(field => field, field => field.GetValue(null));
        try { body(); }
        finally
        {
            foreach (var key in keys)
                if (saved.TryGetValue(key, out var value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            foreach (var setting in settings) setting.Key.SetValue(null, setting.Value);
        }
    }

    // A world-space canvas of a device's canvas size lays out like the overlay and renders without a screen.
    public static void UseWorldSpace(Canvas canvas, Vector2 size)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        var root = (RectTransform)canvas.transform;
        root.sizeDelta = size;
        root.position = Vector3.zero;
        root.localScale = Vector3.one;
        Canvas.ForceUpdateCanvases();
    }

    public static IEnumerable<string> OverflowingText(Component root)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var label in root.GetComponentsInChildren<TMP_Text>())
        {
            label.ForceMeshUpdate();
            if (label.isTextOverflowing) yield return label.text;
        }
    }

    // Renders the world-space canvas at the origin into image at (x, y).
    public static void Render(Vector2 canvasSize, Texture2D image, int x, int y, int width, int height)
    {
        Canvas.ForceUpdateCanvases();
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = canvasSize.y / 2;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, width, height), x, y);
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }

    public static void SavePng(Texture2D image, string path)
    {
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
}
