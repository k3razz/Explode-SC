using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class BindListState
{
    public static bool Enabled = true; // управляется из Misc
}

public static class BindListRenderer
{
    private static Texture2D _bgTex;
    private static readonly Dictionary<string, Vector2> _cache = new();

    private const float PadX = 10f;
    private const float PadY = 6f;
    private const float RowH = 18f;
    private const float Gap = 6f;
    private const float BaseY = 80f;      // от верхнего края
    private const float RightOffset = 20f; // от правого края

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
    /// Рисует панель активных биндов. Если ничего не активно — панель не показывается.
    /// </summary>
    public static void Draw()
    {
        if (!BindListState.Enabled) return;
        if (!KeybindState.IntroDone || !KeybindState.MenuOpen) return; // не мешаем в интро/закрытом меню

        // Собираем активные бинды
        var active = new List<(string label, bool on)>();

        // Aimbot: активен, если AimbotModule.Enabled
        if (AimbotModule.Enabled)
            active.Add(("Aimbot", true));

        // Toggle/Hold-статус — только если сам aimbot включён
        if (AimbotModule.Enabled && KeybindState.AimbotActive)
            active.Add(("Aimbot [HOLD]" + (KeybindState.ToggleMode ? " (toggle)" : ""), true));

        // ESP
        if (EspModule.BoxesEnabled) active.Add(("ESP Boxes", true));
        if (EspModule.NamesEnabled) active.Add(("ESP Names", true));
        if (EspModule.DistanceEnabled) active.Add(("ESP Distance", true));

        // Watermark
        if (WatermarkModule.Enabled) active.Add(("Watermark", true));

        if (active.Count == 0) return;

        EnsureTextures();
        if (_bgTex == null) return;

        // Вычисляем ширину/высоту панели
        float maxW = 0f;
        foreach (var a in active)
        {
            var sz = Size(a.label);
            if (sz.x > maxW) maxW = sz.x;
        }
        float panelW = maxW + PadX * 2f;
        float panelH = active.Count * RowH + PadY * 2f + (active.Count - 1) * Gap;

        float px = Screen.width - panelW - RightOffset;
        float py = BaseY;

        // Фон
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _bgTex);
        // Акцентная полоса слева
        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(px, py, 3f, panelH), _bgTex);
        GUI.color = Color.white;

        // Строки
        float y = py + PadY;
        foreach (var a in active)
        {
            GUI.Label(new Rect(px + PadX, y, panelW - PadX, RowH), a.label);
            y += RowH + Gap;
        }

        GUI.color = Color.white;
    }
}
