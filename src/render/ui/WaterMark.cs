using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    private static Texture2D _roundedTex;
    private static int _lastW = -1;
    private static int _lastH = -1;
    private static Vector2 _lastSize;
    private static string _lastText = "";
    private static float _lastUpdate = 0f;

    private const float PadX = 14f;
    private const float PadY = 8f;
    private const float CornerRadius = 8f; // радиус скругления углов
    private const int AA = 2;              // сглаживание краёв (пикселей)

    private static void EnsureRoundedTexture(int w, int h)
    {
        if (_roundedTex != null && _lastW == w && _lastH == h) return;

        // Освобождаем старую текстуру
        if (_roundedTex != null)
        {
            try { Object.Destroy(_roundedTex); } catch { }
            _roundedTex = null;
        }

        _lastW = w;
        _lastH = h;

        _roundedTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _roundedTex.filterMode = FilterMode.Bilinear;
        _roundedTex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[w * h];
        float r = Mathf.Min(CornerRadius, Mathf.Min(w, h) * 0.5f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Расстояние от угла (только в угловых зонах)
                float a = 1f;

                // Левый верхний
                if (x < r && y < r)
                {
                    float dx = r - (x + 0.5f);
                    float dy = r - (y + 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01((r - d) / AA + 0.5f);
                }
                // Правый верхний
                else if (x >= w - r && y < r)
                {
                    float dx = (x + 0.5f) - (w - r);
                    float dy = r - (y + 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01((r - d) / AA + 0.5f);
                }
                // Левый нижний
                else if (x < r && y >= h - r)
                {
                    float dx = r - (x + 0.5f);
                    float dy = (y + 0.5f) - (h - r);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01((r - d) / AA + 0.5f);
                }
                // Правый нижний
                else if (x >= w - r && y >= h - r)
                {
                    float dx = (x + 0.5f) - (w - r);
                    float dy = (y + 0.5f) - (h - r);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01((r - d) / AA + 0.5f);
                }

                byte alpha = (byte)(Mathf.Clamp01(a) * 255);
                pixels[y * w + x] = new Color32(0, 0, 0, alpha);
            }
        }

        _roundedTex.SetPixels32(pixels);
        _roundedTex.Apply();
    }

    public static Rect GetRect()
    {
        string text = WatermarkModule.GetText();
        if (text != _lastText || Time.time - _lastUpdate > 1f)
        {
            _lastSize = GUI.skin.label.CalcSize(new GUIContent(text));
            _lastText = text;
            _lastUpdate = Time.time;
        }
        float w = _lastSize.x + PadX * 2f;
        float h = _lastSize.y + PadY * 2f;
        return new Rect(WatermarkModule.Position.x - w / 2f, WatermarkModule.Position.y - h / 2f, w, h);
    }

    public static void DrawText()
    {
        if (!WatermarkModule.Enabled) return;
        Rect rect = GetRect();
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY, rect.width - PadX * 2f, rect.height - PadY * 2f), _lastText);
        GUI.color = Color.white;
    }

    public static void HandleDrag()
    {
        if (!WatermarkModule.Enabled) return;
        Rect rect = GetRect();
        Event e = Event.current;
        if (e == null) return;
        Vector2 mouse = e.mousePosition;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(mouse))
        {
            WatermarkModule.IsDragging = true;
            WatermarkModule.DragOffset = mouse - new Vector2(rect.x, rect.y);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && WatermarkModule.IsDragging)
        {
            Vector2 newPos = mouse - WatermarkModule.DragOffset;
            WatermarkModule.Position = new Vector2(newPos.x + rect.width / 2f, newPos.y + rect.height / 2f);
            e.Use();
            if (Plugin.CfgWatermarkX != null) Plugin.CfgWatermarkX.Value = WatermarkModule.Position.x;
            if (Plugin.CfgWatermarkY != null) Plugin.CfgWatermarkY.Value = WatermarkModule.Position.y;
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            WatermarkModule.IsDragging = false;
            e.Use();
        }
    }

    public static void DrawBackground(Texture2D whiteTex)
    {
        if (!WatermarkModule.Enabled) return;
        Rect rect = GetRect();
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        EnsureRoundedTexture(w, h);
        if (_roundedTex == null) return;

        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, w, h), _roundedTex);
        GUI.color = Color.white;
    }
}
