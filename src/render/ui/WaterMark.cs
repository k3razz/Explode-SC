using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    private static Vector2 _lastSize;
    private static string _lastText = "";
    private static float _lastUpdate = 0f;

    private const float PadX = 14f;
    private const float PadY = 8f;

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

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (rect.Contains(mouse) && !UiDragState.AnyDragging)
            {
                WatermarkModule.IsDragging = true;
                UiDragState.AnyDragging = true;
                WatermarkModule.DragOffset = mouse - new Vector2(rect.x, rect.y);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && WatermarkModule.IsDragging)
        {
            Vector2 newPos = mouse - WatermarkModule.DragOffset;
            WatermarkModule.Position = new Vector2(newPos.x + rect.width / 2f, newPos.y + rect.height / 2f);
            e.Use();
            if (Plugin.CfgWatermarkX != null) Plugin.CfgWatermarkX.Value = WatermarkModule.Position.x;
            if (Plugin.CfgWatermarkY != null) Plugin.CfgWatermarkY.Value = WatermarkModule.Position.y;
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && WatermarkModule.IsDragging)
        {
            WatermarkModule.IsDragging = false;
            UiDragState.AnyDragging = false;
            e.Use();
        }
    }

    public static void DrawBackground(Texture2D whiteTex)
    {
        if (!WatermarkModule.Enabled) return;
        UiPanel.Draw(GetRect(), 0.85f);
    }
}
