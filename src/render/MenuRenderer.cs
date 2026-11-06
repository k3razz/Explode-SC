using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class MenuRenderer
{
    public const float MenuWidth = 608f;
    public const float MenuHeight = 316f;
    public const float TabWidth = 94f;

    private static Texture2D _whiteTex;
    private static Texture2D _circleTex;
    private static Texture2D _ringTex;
    private static readonly Dictionary<string, Vector2> _textCache = new();

    // Цвета из SVG
    public static readonly Color ColPanelBg = new Color32(0x00, 0x05, 0x19, 255);
    public static readonly Color ColTabActive = new Color32(0x01, 0x09, 0x2C, 255);
    public static readonly Color ColDivider = new Color32(0xA0, 0xB2, 0xDE, 255);
    public static readonly Color ColBlockBg = new Color32(0x00, 0x05, 0x1B, 255);
    public static readonly Color ColBlockBorder = new Color32(0x20, 0x33, 0x50, 255);
    public static readonly Color ColText = new Color32(0xBD, 0xBC, 0xBC, 255);
    public static readonly Color ColTextDim = new Color32(0x66, 0x66, 0x66, 255);
    public static readonly Color ColToggleTrack = new Color32(0x07, 0x1A, 0x3D, 255);
    public static readonly Color ColToggleKnob = new Color32(0xFB, 0xFB, 0xFB, 255);

    public static void EnsureTextures()
    {
        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }
        if (_circleTex == null) _circleTex = MakeCircle(128, false, 0f);
        if (_ringTex == null) _ringTex = MakeCircle(128, true, 2f);
    }

    private static Texture2D MakeCircle(int size, bool ring, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float center = size / 2f;
        float outer = size / 2f - 1f;
        float inner = outer - thickness;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f, dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (ring)
                {
                    if (dist <= outer && dist >= inner)
                        a = Mathf.Clamp01(Mathf.Min(outer - dist, dist - inner) / 1.5f);
                }
                else
                {
                    a = Mathf.Clamp01((outer - dist) / 1.5f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Vector2 GetTextSize(string text)
    {
        if (_textCache.TryGetValue(text, out var size)) return size;
        size = GUI.skin.label.CalcSize(new GUIContent(text));
        _textCache[text] = size;
        return size;
    }

    public static void DrawFovCircle()
    {
        EnsureTextures();
        if (!AimbotModule.DrawFovCircle) return;
        if (!AimbotModule.Enabled) return;

        float fovScreen = AimbotModule.Fov * (Screen.height / 90f);
        if (fovScreen <= 0) return;

        var rect = new Rect(
            Screen.width / 2f - fovScreen / 2f,
            Screen.height / 2f - fovScreen / 2f,
            fovScreen, fovScreen);

        GUI.color = new Color(1f, 1f, 1f, 0.04f);
        GUI.DrawTexture(rect, _circleTex);
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(rect, _ringTex);
        GUI.color = Color.white;
    }

    public static void DrawIntroAnimation(Texture2D whiteTex, float t)
    {
        float sw = Screen.width, sh = Screen.height;

        float bgAlpha = 0f;
        if (t >= 0f && t < 0.3f) bgAlpha = Mathf.Lerp(0f, 0.65f, (t - 0f) / 0.3f);
        else if (t >= 0.3f && t < 1.6f) bgAlpha = 0.65f;
        else if (t >= 1.6f && t < 2.0f) bgAlpha = Mathf.Lerp(0.65f, 0f, (t - 1.6f) / 0.4f);

        if (bgAlpha > 0f)
        {
            GUI.color = new Color(0.1f, 0.1f, 0.1f, bgAlpha);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), whiteTex);
            GUI.color = Color.white;
        }

        float logoAlpha = 0f;
        if (t >= 0.3f && t < 0.8f) logoAlpha = Mathf.Lerp(0f, 1f, (t - 0.3f) / 0.5f);
        else if (t >= 0.8f && t < 1.2f) logoAlpha = 1f;
        else if (t >= 1.2f && t < 1.6f) logoAlpha = Mathf.Lerp(1f, 0f, (t - 1.2f) / 0.4f);

        if (logoAlpha > 0f)
        {
            string eLetter = "E";
            string rest = "xplode";
            float logoY = sh * 0.35f;
            float eWidth = 55f;
            float restWidth = rest.Length * 55f;
            float totalWidth = eWidth + restWidth;
            float startX = sw / 2f - totalWidth / 2f;

            GUI.color = new Color(0f, 0f, 0f, logoAlpha * 0.6f);
            GUI.Label(new Rect(startX + 3f, logoY + 3f, totalWidth, 120f), eLetter + rest);
            GUI.color = new Color(1f, 0.2f, 0.2f, logoAlpha);
            GUI.Label(new Rect(startX, logoY, eWidth + 4f, 120f), eLetter);
            GUI.color = new Color(1f, 1f, 1f, logoAlpha);
            GUI.Label(new Rect(startX + eWidth, logoY, restWidth + 4f, 120f), rest);
            GUI.color = Color.white;
        }
    }

    public static void DrawMenu(Texture2D whiteTex)
    {
        EnsureTextures();
        _whiteTex = whiteTex;

        float sw = Screen.width, sh = Screen.height;
        float mw = MenuWidth, mh = MenuHeight;
        Rect panelRect = new Rect((sw - mw) / 2f, (sh - mh) / 2f, mw, mh);

        // Панель
        DrawRect(panelRect, ColPanelBg);

        // Левая колонка вкладок
        float tabX = panelRect.x + 5f;
        float tabY = panelRect.y + 10f;

        DrawTabText(new Rect(tabX, tabY, TabWidth, 22f), "Main", UiState.MenuTab == 0, ColTextDim);
        if (ClickZone(new Rect(tabX, tabY, TabWidth, 30f))) UiState.MenuTab = 0;
        tabY += 33f;

        if (DrawTabButton(new Rect(tabX, tabY, TabWidth, 36f), "Visuals", UiState.MenuTab == 1)) UiState.MenuTab = 1;
        tabY += 39f;

        if (DrawTabButton(new Rect(tabX, tabY, TabWidth, 36f), "Aim", UiState.MenuTab == 2)) UiState.MenuTab = 2;
        tabY += 39f;

        DrawTabText(new Rect(tabX, tabY, TabWidth, 22f), "Screen", UiState.MenuTab == 3, ColTextDim);
        if (ClickZone(new Rect(tabX, tabY, TabWidth, 30f))) UiState.MenuTab = 3;
        tabY += 33f;

        if (DrawTabButton(new Rect(tabX, tabY, TabWidth, 36f), "Hud", UiState.MenuTab == 4)) UiState.MenuTab = 4;

        // Разделитель
        DrawRect(new Rect(panelRect.x + 104f, panelRect.y, 2f, panelRect.height), ColDivider);

        // Контент
        float contentX = panelRect.x + 118f;
        float contentY = panelRect.y + 10f;
        float contentW = panelRect.width - 128f;
        float contentH = panelRect.height - 20f;

        if (UiState.MenuTab == 1) DrawEspBlock(contentX, contentY, contentW, contentH);
        else if (UiState.MenuTab == 2) DrawAimBlock(contentX, contentY, contentW, contentH);
        else if (UiState.MenuTab == 4) DrawRenderBlock(contentX, contentY, contentW, contentH);

        // Ватермарка снизу
        DrawWatermark(sw, sh);
    }

    private static void DrawWatermark(float sw, float sh)
    {
        float wmW = 235f;
        float wmH = 29f;
        float wmX = sw / 2f - wmW / 2f;
        float wmY = sh - 40f;
        DrawRect(new Rect(wmX, wmY, wmW, wmH), ColTabActive, 9f);

        string label = "Explode • Beta • 13:01 06.11";
        Vector2 size = GetTextSize(label);
        GUI.color = ColText;
        GUI.Label(new Rect(wmX + (wmW - size.x) / 2f, wmY + (wmH - size.y) / 2f, size.x + 2f, size.y + 2f), label);
        GUI.color = Color.white;
    }

    private static void DrawEspBlock(float bx, float y, float bw, float bh)
    {
        Rect block = new Rect(bx, y, bw, bh);
        DrawRect(block, ColBlockBg);
        DrawBorder(block, ColBlockBorder, 1f);

        DrawCentered("ESP", new Rect(bx, y + 8f, bw, 20f), ColText, true);
        y += 36f;

        float itemX = bx + 16f;
        float itemW = bw - 32f;
        float rowH = 30f;

        ToggleRow(new Rect(itemX, y, itemW, rowH), "Enabled", ref EspModule.BoxesEnabled);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show Spec", ref TeamFilter.ShowSpectator);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show T", ref TeamFilter.ShowTr);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show CT", ref TeamFilter.ShowCt);
    }

    private static void DrawAimBlock(float bx, float y, float bw, float bh)
    {
        Rect block = new Rect(bx, y, bw, bh);
        DrawRect(block, ColBlockBg);
        DrawBorder(block, ColBlockBorder, 1f);

        DrawCentered("AimAssist", new Rect(bx, y + 8f, bw, 20f), ColText, true);
        y += 36f;

        float itemX = bx + 16f;
        float itemW = bw - 32f;
        float rowH = 30f;

        ToggleRow(new Rect(itemX, y, itemW, rowH), "Enabled", ref AimbotModule.Enabled);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "FOV Circle", ref AimbotModule.DrawFovCircle);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Aim on Teammates", ref AimbotModule.AimOnTeammates);
        y += rowH;

        // FOV
        LabelRow(new Rect(itemX, y, itemW, 20f), $"FOV: {AimbotModule.Fov:F0}°");
        y += 22f;
        if (SmallButton(new Rect(itemX, y, (itemW - 4f) / 2f, 24f), "-"))
        { AimbotModule.Fov = Mathf.Max(1f, AimbotModule.Fov - 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        if (SmallButton(new Rect(itemX + (itemW + 4f) / 2f, y, (itemW - 4f) / 2f, 24f), "+"))
        { AimbotModule.Fov = Mathf.Min(180f, AimbotModule.Fov + 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        y += 28f;

        // Sensitivity
        LabelRow(new Rect(itemX, y, itemW, 20f), $"Sens: {AimbotModule.Sensitivity:F2}");
        y += 22f;
        if (SmallButton(new Rect(itemX, y, (itemW - 4f) / 2f, 24f), "-"))
        { AimbotModule.Sensitivity = Mathf.Max(0.01f, AimbotModule.Sensitivity - 0.05f); Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity; }
        if (SmallButton(new Rect(itemX + (itemW + 4f) / 2f, y, (itemW - 4f) / 2f, 24f), "+"))
        { AimbotModule.Sensitivity = Mathf.Min(3f, AimbotModule.Sensitivity + 0.05f); Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity; }
        y += 28f;

        // Bone
        if (SmallButton(new Rect(itemX, y, itemW, 24f), $"Bone: {AimbotModule.Bone}"))
        {
            var next = AimbotModule.Bone == AimBone.Head ? AimBone.Neck
                     : AimbotModule.Bone == AimBone.Neck ? AimBone.Chest
                     : AimBone.Head;
            AimbotModule.Bone = next;
            Plugin.CfgAimbotBone.Value = next.ToString();
        }
        y += 28f;

        if (SmallButton(new Rect(itemX, y, itemW, 24f), $"Mode: {(KeybindState.ToggleMode ? "TOGGLE" : "HOLD")}"))
        {
            KeybindState.ToggleMode = !KeybindState.ToggleMode;
            Plugin.CfgAimbotToggleMode.Value = KeybindState.ToggleMode;
        }
        y += 28f;

        string keyName = KeybindState.Capturing ? "PRESS KEY... (Esc)" : KeybindState.AimbotKey.ToString();
        if (SmallButton(new Rect(itemX, y, itemW, 24f), $"Bind: {keyName}"))
        {
            if (!KeybindState.Capturing)
            {
                KeybindState.CapturingBackup = KeybindState.AimbotKey;
                KeybindState.Capturing = true;
            }
            else
            {
                KeybindState.AimbotKey = KeybindState.CapturingBackup;
                KeybindState.Capturing = false;
            }
        }
    }

    private static void DrawRenderBlock(float bx, float y, float bw, float bh)
    {
        Rect block = new Rect(bx, y, bw, bh);
        DrawRect(block, ColBlockBg);
        DrawBorder(block, ColBlockBorder, 1f);

        DrawCentered("Render", new Rect(bx, y + 8f, bw, 20f), ColText, true);
        y += 36f;

        float itemX = bx + 16f;
        float itemW = bw - 32f;
        float rowH = 30f;

        ToggleRow(new Rect(itemX, y, itemW, rowH), "Watermark", ref WatermarkModule.Enabled);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show Bind List", ref BindListState.Enabled);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show Bomb Status", ref BombStatusRenderer.Enabled);
    }

    // === Хелперы ===

    private static bool DrawTabButton(Rect rect, string label, bool active)
    {
        if (active) DrawRect(rect, ColTabActive, 9f);
        bool clicked = ClickZone(rect);
        DrawCentered(label, rect, active ? Color.white : ColText, false);
        return clicked;
    }

    private static void DrawTabText(Rect rect, string label, bool active, Color col)
    {
        DrawCentered(label, rect, active ? Color.white : col, false);
    }

    private static bool ClickZone(Rect rect)
    {
        Event e = Event.current;
        return e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
    }

    private static void ToggleRow(Rect rect, string label, ref bool value)
    {
        DrawLeft(label, rect, ColText);
        Rect toggle = new Rect(rect.xMax - 40f, rect.y + (rect.height - 16f) / 2f, 30f, 16f);
        DrawRect(toggle, ColToggleTrack, 8f);

        float knobSize = 17f;
        float knobX = value ? toggle.xMax - knobSize + 0.5f : toggle.x - 0.5f;
        Rect knob = new Rect(knobX, rect.y + (rect.height - knobSize) / 2f, knobSize, knobSize);
        DrawRect(knob, ColToggleKnob, 8.5f);

        if (ClickZone(rect)) value = !value;
    }

    private static void LabelRow(Rect rect, string label)
    {
        DrawLeft(label, rect, ColText);
    }

    private static bool SmallButton(Rect rect, string label)
    {
        DrawRect(rect, ColBlockBorder, 6f);
        DrawCentered(label, rect, ColText, false);
        return ClickZone(rect);
    }

    private static void DrawLeft(string text, Rect rect, Color col)
    {
        Vector2 size = GetTextSize(text);
        GUI.color = col;
        GUI.Label(new Rect(rect.x, rect.y + (rect.height - size.y) / 2f, rect.width, size.y + 2f), text);
        GUI.color = Color.white;
    }

    private static void DrawCentered(string text, Rect rect, Color col, bool bold)
    {
        Vector2 size = GetTextSize(text);
        GUI.color = col;
        string render = bold ? $"<b>{text}</b>" : text;
        GUI.Label(new Rect(rect.x + (rect.width - size.x) / 2f, rect.y + (rect.height - size.y) / 2f, size.x + 2f, size.y + 2f), render);
        GUI.color = Color.white;
    }

    private static void DrawRect(Rect rect, Color col, float radius = 0f)
    {
        if (radius <= 0f)
        {
            GUI.color = col;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
            return;
        }
        // Простое скругление: 3 прямоугольника (центр + 2 полосы) + 4 угла текстурами не рисуем,
        // для IMGUI ограничимся прямоугольником + скруглённым верхом/низом через круги.
        DrawRoundedRect(rect, col, radius);
    }

    private static void DrawRoundedRect(Rect rect, Color col, float radius)
    {
        // Центр
        GUI.color = col;
        GUI.DrawTexture(new Rect(rect.x + radius, rect.y, rect.width - radius * 2f, rect.height), _whiteTex);
        GUI.DrawTexture(new Rect(rect.x, rect.y + radius, rect.width, rect.height - radius * 2f), _whiteTex);

        // Углы через круги
        float d = radius * 2f;
        GUI.DrawTexture(new Rect(rect.x, rect.y, d, d), _circleTex);
        GUI.DrawTexture(new Rect(rect.xMax - d, rect.y, d, d), _circleTex);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - d, d, d), _circleTex);
        GUI.DrawTexture(new Rect(rect.xMax - d, rect.yMax - d, d, d), _circleTex);

        GUI.color = Color.white;
    }

    private static void DrawBorder(Rect rect, Color col, float thickness)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTex);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _whiteTex);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTex);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _whiteTex);
        GUI.color = Color.white;
    }
}
