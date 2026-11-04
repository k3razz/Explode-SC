using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    private static Vector2 _lastSize;
    private static string _lastText = "";
    private static float _lastUpdate = 0f;

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
        Vector2 size = _lastSize;

        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, size.x + 2f, size.y + 2f), _lastText);
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
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = Color.white;
    }
}