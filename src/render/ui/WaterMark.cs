using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    public static void Draw(Texture2D whiteTex)
    {
        if (!WatermarkModule.Enabled) return;

        string text = WatermarkModule.GetText();
        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));

        float padX = 12f;
        float padY = 6f;
        float w = size.x + padX * 2f;
        float h = size.y + padY * 2f;

        Rect rect = new Rect(WatermarkModule.Position.x - w / 2f, WatermarkModule.Position.y - h / 2f, w, h);

        Event e = Event.current;
        if (e != null)
        {
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
                WatermarkModule.Position = new Vector2(newPos.x + w / 2f, newPos.y + h / 2f);
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

        // Фон (черный полупрозрачный)
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(rect, whiteTex);

        // Текст по центру
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + padX, rect.y + padY, size.x + 2f, size.y + 2f), text);
        GUI.color = Color.white;
    }
}