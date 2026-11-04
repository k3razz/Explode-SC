using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class BindListState
{
    public static bool Enabled = true;
}

public static class BindListRenderer
{
    private static Texture2D _bgTex;
    private static readonly Dictionary<string, Vector2> _cache = new();

    private const float PadX = 10f;
    private const float PadY = 6f;
    private const float RowH = 18f;
    private const float Gap = 6f;
    private const float BaseY = 80f;
    private const float RightOffset = 20f;

    public static void EnsureTextures()
    {
        if (_bgTex == null)
        {
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, Color.white);
            _bgTex.Apply();
        }
    }

    private static Vector2 Size(string s)
    {
        if (_cache.TryGetValue(s, out var v)) return v;
        v = GUI.skin.label.CalcSize(new GUIContent(s));
        _cache[s] = v;
        return v;
    }

    /// <summary>
    /// Рисует панель активных биндов. Показывается всегда, если есть хоть один активный бинд.
    /// Строка = один бинд: "Aimbot [Mouse0] HOLD".
    /// </summary>
    public static void Draw()
    {
        if (!BindListState.Enabled) return;
        if (!KeybindState.IntroDone) return; // в интро не мешаем

        var active = new List<string>();

        // Aimbot — показываем, только когда он реально активен
        if (AimbotModule.Enabled && KeybindState.AimbotActive)
        {
            string mode = KeybindState.ToggleMode ? "TOGGLE" : "HOLD";
            active.Add($"Aimbot [{KeybindState.AimbotKey}] {mode}");
        }

        if (active.Count == 0) return;

        EnsureTextures();
        if (_bgTex == null) return;

        float maxW = 0f;
        foreach (var s in active)
        {
            var sz = Size(s);
            if (sz.x > maxW) maxW = sz.x;
        }
        float panelW = maxW + PadX * 2f;
        float panelH = active.Count * RowH + PadY * 2f + (active.Count - 1) * Gap;

        float px = Screen.width - panelW - RightOffset;
        float py = BaseY;

        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _bgTex);
        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(px, py, 3f, panelH), _bgTex);
        GUI.color = Color.white;

        float y = py + PadY;
        foreach (var s in active)
        {
            GUI.Label(new Rect(px + PadX, y, panelW - PadX, RowH), s);
            y += RowH + Gap;
        }

        GUI.color = Color.white;
    }
}
