using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    public static Rect GetRect()
    {
        string text = WatermarkModule.GetText();
        float w = text.Length * 7f + 24f;
        float h = 24f;
        return new Rect(WatermarkModule.Position.x - w / 2f, WatermarkModule.Position.y - h / 2f, w, h);
    }

    public static void DrawText()
    {
        if (!WatermarkModule.Enabled) return;
        Rect rect = GetRect();
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, rect.height - 12f), WatermarkModule.GetText());
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