using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ModernUI
{
    private static Sprite rounded;
    private static TMP_FontAsset readableFont;
    public static TMP_FontAsset ReadableFont
    {
        get
        {
            if (readableFont == null)
            {
                var source = Resources.Load<Font>("Fonts/Inter-Regular");
                readableFont = source != null ? TMP_FontAsset.CreateFontAsset(source) :
                    Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
            return readableFont;
        }
    }

    public static void Surface(Image image, Color color, bool shadow = false)
    {
        if (rounded == null)
        {
            const int size = 96;
            const float radius = 24f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Rounded UI surface";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius), 0);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0,
                SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        }
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        if (shadow && image.GetComponent<Shadow>() == null)
        {
            var effect = image.gameObject.AddComponent<Shadow>();
            effect.effectColor = new Color(0.12f, 0.34f, 0.44f, 0.07f);
            effect.effectDistance = new Vector2(0, -5);
        }
    }
}
