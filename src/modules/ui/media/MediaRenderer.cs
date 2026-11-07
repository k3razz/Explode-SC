using UnityEngine;

namespace MyDynamicESP;

public static class MediaRenderer
{
    public const float PanelWidth = 220f;
    public const float PanelHeight = 80f;
    public const float Radius = 10f;

    public static Rect GetRect()
    {
        return new Rect(MediaModule.Position.x, MediaModule.Position.y, PanelWidth, PanelHeight);
    }

    public static void HandleDrag()
    {
        if (!MediaModule.Enabled) return;
        Rect rect = GetRect();
        Event e = Event.current;
        if (e == null) return;
        Vector2 mouse = e.mousePosition;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (rect.Contains(mouse) && !UiDragState.AnyDragging)
            {
                MediaModule.IsDragging = true;
                UiDragState.AnyDragging = true;
                MediaModule.DragOffset = mouse - new Vector2(rect.x, rect.y);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && MediaModule.IsDragging)
        {
            Vector2 newPos = mouse - MediaModule.DragOffset;
            MediaModule.Position = newPos;
            if (Plugin.CfgMediaX != null) Plugin.CfgMediaX.Value = newPos.x;
            if (Plugin.CfgMediaY != null) Plugin.CfgMediaY.Value = newPos.y;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && MediaModule.IsDragging)
        {
            MediaModule.IsDragging = false;
            UiDragState.AnyDragging = false;
            e.Use();
        }
    }

    public static void Draw()
    {
        if (!MediaModule.Enabled) return;
        if (!KeybindState.IntroDone) return;
        if (string.IsNullOrEmpty(MediaModule.TrackTitle)) return; // Нет трека — не рисуем

        Rect rect = GetRect();

        // Фон
        MenuRenderer.DrawRoundedRect(new Color32(0x01, 0x09, 0x2C, 255), rect, Radius);

        // Обложка
        if (MediaModule.AlbumArt != null)
        {
            GUI.DrawTexture(new Rect(rect.x + 10f, rect.y + 10f, 60f, 60f), MediaModule.AlbumArt);
        }
        else
        {
            MenuRenderer.DrawRect(new Rect(rect.x + 10f, rect.y + 10f, 60f, 60f), new Color32(0x20, 0x33, 0x50, 255), 5f);
        }

        var textCol = new Color32(0xBD, 0xBC, 0xBC, 255);
        GUI.color = textCol;

        // Название трека
        GUI.Label(new Rect(rect.x + 80f, rect.y + 10f, 130f, 20f), MediaModule.TrackTitle);

        // Таймер
        string time = $"{MediaModule.GetTimeString(MediaModule.Progress)}/{MediaModule.GetTimeString(MediaModule.Duration)}";
        GUI.Label(new Rect(rect.x + 80f, rect.y + 30f, 130f, 20f), time);

        // Полоса прогресса
        float barX = rect.x + 80f;
        float barY = rect.y + 55f;
        float barW = 130f;
        float barH = 6f;

        MenuRenderer.DrawRect(new Rect(barX, barY, barW, barH), new Color32(0x07, 0x1A, 0x3D, 255), 3f);

        if (MediaModule.Duration > 0f)
        {
            float fillW = barW * Mathf.Clamp01(MediaModule.Progress / MediaModule.Duration);
            MenuRenderer.DrawRect(new Rect(barX, barY, fillW, barH), new Color32(0x1A, 0x4D, 0x82, 255), 3f);
        }

        GUI.color = Color.white;
    }
}
