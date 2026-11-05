using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class BindListState
{
    public static bool Enabled = true;
}

public static class BindListRenderer
{
    private const float PadX = 12f;
    private const float PadY = 8f;
    private const float RowH = 18f;
    private const float Gap = 4f;
    private const float HeaderH = 22f;
    private const float BaseY = 80f;
    private const float RightOffset = 20f;
    private const float PanelWidth = 260f;

    public static float GetHeight()
    {
        if (!BindListState.Enabled || !KeybindState.IntroDone) return 0f;
        int count = 0;
        if (AimbotModule.Enabled && KeybindState.AimbotActive) count++;
        if (count == 0) return 0f;
        return PadY * 2f + HeaderH + 4f + count * (RowH + Gap);
    }

    public static void Draw(float y)
    {
        if (!BindListState.Enabled) return;
        if (!KeybindState.IntroDone) return;

        var rows = new List<string>();
        if (AimbotModule.Enabled && KeybindState.AimbotActive)
        {
            string mode = KeybindState.ToggleMode ? "TOGGLE" : "HOLD";
            rows.Add($"AimLock    [{mode}]    [{KeybindState.AimbotKey}]");
        }

        if (rows.Count == 0) return;

        float h = PadY * 2f + HeaderH + 4f + rows.Count * (RowH + Gap);
        float x = Screen.width - RightOffset - PanelWidth;
        var rect = new Rect(x, y, PanelWidth, h);

        UiPanel.Draw(rect, 0.85f);

        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, h), Texture2D.whiteTexture);
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
