using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class BindListState
{
    public static bool Enabled = true;
    public static Vector2 Position = new Vector2(-1f, -1f);
    public static bool IsDragging = false;
    public static Vector2 DragOffset = Vector2.zero;
}

public static class BindListRenderer
{
    private const float PadX = 12f;
    private const float PadY = 8f;
    private const float RowH = 18f;
    private const float Gap = 4f;
    private const float HeaderH = 22f;
    private const float PanelWidth = 260f;
    private const float DefaultX = 20f;
    private const float DefaultY = 80f;

    private static Vector2 GetPosition()
    {
        if (BindListState.Position.x < 0f || BindListState.Position.y < 0f)
            return new Vector2(Screen.width - PanelWidth - DefaultX, DefaultY);
        return BindListState.Position;
    }

    private static List<string> GetRows()
    {
        var rows = new List<string>();
        if (AimbotModule.Enabled && KeybindState.AimbotActive)
        {
            string mode = KeybindState.ToggleMode ? "toggle" : "hold";
            rows.Add($"AimLock    [{mode}]    [{KeybindState.AimbotKey}]");
        }
        if (rows.Count == 0 && KeybindState.MenuOpen)
            rows.Add("Example    [hold]    [J]");
        return rows;
    }

    public static Rect GetRect()
    {
        var rows = GetRows();
        float h = PadY * 2f + HeaderH + 4f + rows.Count * (RowH + Gap);
        var pos = GetPosition();
        return new Rect(pos.x, pos.y, PanelWidth, h);
    }

    public static void HandleDrag()
    {
        var rect = GetRect();
        Event e = Event.current;
        if (e == null) return;
        Vector2 mouse = e.mousePosition;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (rect.Contains(mouse) && !UiDragState.AnyDragging)
            {
                BindListState.IsDragging = true;
                UiDragState.AnyDragging = true;
                BindListState.DragOffset = mouse - new Vector2(rect.x, rect.y);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && BindListState.IsDragging)
        {
            Vector2 newPos = mouse - BindListState.DragOffset;
            BindListState.Position = newPos;
            Plugin.CfgBindListX.Value = newPos.x;
            Plugin.CfgBindListY.Value = newPos.y;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && BindListState.IsDragging)
        {
            BindListState.IsDragging = false;
            UiDragState.AnyDragging = false;
            e.Use();
        }
    }

    public static void Draw()
    {
        if (!BindListState.Enabled) return;
        if (!KeybindState.IntroDone) return;

        var rows = GetRows();
        if (rows.Count == 0) return;

        var rect = GetRect();
        UiPanel.Draw(rect, 0.85f);

        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY, PanelWidth - PadX * 2f, RowH), "Binds");

        float ry = rect.y + PadY + HeaderH + 4f;
        foreach (var r in rows)
        {
            GUI.Label(new Rect(rect.x + PadX, ry, PanelWidth - PadX * 2f, RowH), r);
            ry += RowH + Gap;
        }

        GUI.color = Color.white;
    }
}
