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
    private const float RowH = 20f;
    private const float HeaderH = 22f;
    private const float PanelWidth = 220f;
    private const float DefaultX = 20f;
    private const float DefaultY = 80f;
    private const float Radius = 10f;

    private static Vector2 GetPosition()
    {
        if (BindListState.Position.x < 0f || BindListState.Position.y < 0f)
            return new Vector2(Screen.width - PanelWidth - DefaultX, DefaultY);
        return BindListState.Position;
    }

    private static List<(string module, string mode, string bind)> GetRows()
    {
        var rows = new List<(string, string, string)>();
        if (AimbotModule.Enabled && KeybindState.AimbotActive)
        {
            string mode = KeybindState.ToggleMode ? "Toggle" : "Hold";
            rows.Add(("AimLock", mode, KeybindState.AimbotKey.ToString()));
        }
        // Убрали добавление "Example" — если нет реальных биндов, список пуст
        return rows;
    }

    public static Rect GetRect()
    {
        var rows = GetRows();
        float rowTotal = rows.Count > 0 ? rows.Count * RowH + (rows.Count - 1) * 4f : 0f;
        float h = PadY * 2f + HeaderH + rowTotal + (rows.Count > 0 ? 6f : 0f);
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
            if (Plugin.CfgBindListX != null) Plugin.CfgBindListX.Value = newPos.x;
            if (Plugin.CfgBindListY != null) Plugin.CfgBindListY.Value = newPos.y;
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
        if (rows.Count == 0) return; // Скрываем панель, если нет биндов

        var rect = GetRect();

        // Фон панели без прозрачности (альфа 255)
        MenuRenderer.DrawRoundedRect(new Color32(0x01, 0x09, 0x2C, 255), rect, Radius);

        // Заголовок
        var headerCol = new Color32(0xBD, 0xBC, 0xBC, 255);
        Vector2 headerSize = GUI.skin.label.CalcSize(new GUIContent("Binds"));
        GUI.color = headerCol;
        GUI.Label(new Rect(rect.x + (rect.width - headerSize.x) / 2f, rect.y + PadY, headerSize.x + 2f, headerSize.y + 2f), "Binds");

        // Разделитель под заголовком
        float sepY = rect.y + PadY + HeaderH;
        MenuRenderer.DrawRect(new Rect(rect.x + PadX, sepY, rect.width - PadX * 2f, 1f), new Color(1f, 1f, 1f, 0.08f));

        // Строки: "Module • Mode   Bind"
        float ry = sepY + 6f;
        foreach (var r in rows)
        {
            string left = $"{r.module} • {r.mode}";
            Vector2 leftSize = GUI.skin.label.CalcSize(new GUIContent(left));
            GUI.color = headerCol;
            GUI.Label(new Rect(rect.x + PadX, ry, leftSize.x + 2f, RowH), left);

            Vector2 bindSize = GUI.skin.label.CalcSize(new GUIContent(r.bind));
            GUI.Label(new Rect(rect.xMax - PadX - bindSize.x, ry, bindSize.x + 2f, RowH), r.bind);

            ry += RowH + 4f;
        }

        GUI.color = Color.white;
    }
}
