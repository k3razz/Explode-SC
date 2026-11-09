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
    private const float PadY = 10f;
    private const float RowH = 22f;
    private const float HeaderH = 24f;
    private const float PanelWidth = 220f;
    private const float DefaultX = 20f;
    private const float DefaultY = 80f;
    private const float Radius = 10f;

    // Цвета из MenuRenderer
    private static readonly Color ColPanelBg = new Color32(0x0B, 0x0F, 0x1A, 255);
    private static readonly Color ColBlockBg = new Color32(0x11, 0x17, 0x2A, 255);
    private static readonly Color ColBorder = new Color32(0x23, 0x2B, 0x45, 255);
    private static readonly Color ColText = new Color32(0xD5, 0xD9, 0xE6, 255);
    private static readonly Color ColTextDim = new Color32(0x7C, 0x86, 0xA2, 255);
    private static readonly Color ColAccent = new Color32(0x5B, 0x8C, 0xFF, 255);

    private static Texture2D _whiteTex;

    private static void EnsureTex()
    {
        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }
    }

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
            newPos.x = Mathf.Clamp(newPos.x, 0f, Screen.width - PanelWidth);
            newPos.y = Mathf.Clamp(newPos.y, 0f, Screen.height - rect.height);
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
        if (rows.Count == 0) return;

        EnsureTex();
        var rect = GetRect();

        // Фон
        MenuRenderer.DrawRoundedRect(ColBlockBg, rect, Radius);

        // Заголовок
        GUI.color = ColText;
        GUI.Label(new Rect(rect.x + PadX, rect.y + 4f, rect.width - PadX * 2f, HeaderH), "Binds", GUI.skin.label);
        GUI.color = Color.white;

        // Разделитель
        MenuRenderer.DrawRect(new Rect(rect.x + PadX, rect.y + HeaderH + 4f, rect.width - PadX * 2f, 1f), new Color(1f, 1f, 1f, 0.08f));

        // Строки
        float ry = rect.y + HeaderH + 10f;
        foreach (var r in rows)
        {
            string left = $"{r.module} • {r.mode}";
            Vector2 leftSize = GUI.skin.label.CalcSize(new GUIContent(left));
            GUI.color = ColText;
            GUI.Label(new Rect(rect.x + PadX, ry, leftSize.x + 2f, RowH), left);

            Vector2 bindSize = GUI.skin.label.CalcSize(new GUIContent(r.bind));
            GUI.color = ColAccent;
            GUI.Label(new Rect(rect.xMax - PadX - bindSize.x, ry, bindSize.x + 2f, RowH), r.bind);

            ry += RowH + 4f;
        }

        GUI.color = Color.white;
    }
}