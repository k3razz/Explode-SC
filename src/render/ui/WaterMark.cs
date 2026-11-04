using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    private static Texture2D _roundedTex;
    private static Vector2 _lastSize;
    private static string _lastText = "";
    private static float _lastUpdate = 0f;

    private static void EnsureRoundedTexture()
    {
        if (_roundedTex != null) return;
        int size = 32;
        int radius = 8;
        _roundedTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _roundedTex.filterMode = FilterMode.Bilinear;
        _roundedTex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = 0, dy = 0;
                if (x < radius) dx = radius - x;
                else if (x >= size - radius) dx = x - (size - radius - 1);
                if (y < radius) dy = radius - y;
                else if (y >= size - radius) dy = y - (size - radius - 1);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = dist <= radius ? (byte)255 : (byte)0;
                // Сглаживание
                if (dist > radius - 1 && dist <= radius + 1)
                    a = (byte)(Mathf.Clamp01(radius + 1 - dist) * 255);

                pixels[y * size + x] = new Color32(0, 0, 0, a); // Черный цвет с прозрачностью
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
        float padX = 12f;
        float padY = 6f;
        float w = _lastSize.x + padX * 2f;
        float h = _lastSize.y + padY * 2f;
        return new Rect(WatermarkModule.Position.x - w / 2f, WatermarkModule.Position.y - h / 2f, w, h);
    }

    public static void DrawText()
    {
        if (!WatermarkModule.Enabled) return;
        Rect rect = GetRect();
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, rect.height - 12f), _lastText);
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
        EnsureRoundedTexture();
        Rect rect = GetRect();
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(rect, _roundedTex); // Используем кастомную скругленную текстуру
        GUI.color = Color.white;
    }
}
