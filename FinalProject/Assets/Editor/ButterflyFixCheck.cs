using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public static class ButterflyFixCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene 1.unity");
        var tracker = Object.FindFirstObjectByType<ButterflyAngularTracker>();
        var raw = tracker.ButterflyRect.GetComponent<RawImage>();
        if (raw == null) throw new System.Exception("Expected the scene butterfly RawImage.");
        for (int i = 0; i < GameSettings.ButterflyColours.Length; i++)
        {
            GameSettings.butterflyColour = i;
            tracker.ApplySettings();
            if (raw.material.shader.name != "UI/ButterflyColour")
                throw new System.Exception("Colour material was not applied to RawImage.");
            if (raw.material.GetColor("_ButterflyColour") != GameSettings.ButterflyColours[i])
                throw new System.Exception("Colour mismatch: " + i);
            if (raw.material.GetFloat("_Recolour") != (i == 0 ? 0f : 1f))
                throw new System.Exception("Original colour reset failed.");
        }
        GameSettings.butterflyColour = 0;
        tracker.ApplySettings();
        Debug.Log("BUTTERFLY_FIX_CHECK: all six colours applied to scene RawImage; original restored.");
    }
}
