using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class MenuRenderer
{
    public const float TabWidth = 94f;

    private static Texture2D _whiteTex;
    private static Texture2D _circleTex;
    private static Texture2D _ringTex;
    private static readonly Dictionary<string, Vector2> _textCache = new();

    public static readonly Color ColPanelBg = new Color32(0x00, 0x05, 0x19, 255);
    public static readonly Color ColTabActive = new Color32(0x01, 0x09, 0x2C, 255);
    public static readonly Color ColDivider = new Color32(0xA0, 0xB2, 0xDE, 255);
    public static readonly Color ColBlockBg = new Color32(0x00, 0x05, 0x1B, 255);
    public static readonly Color ColBlockBorder = new Color32(0x20, 0x33, 0x50, 255);
    public static readonly Color ColText = new Color32(0xBD, 0xBC, 0xBC, 255);
    public static readonly Color ColTextDim = new Color32(0x66, 0x66, 0x66, 255);
    public static readonly Color ColToggleTrack = new Color32(0x07, 0x1A, 0x3D, 255);
    public static readonly Color ColToggleKnob = new Color32(0xFB, 0xFB, 0xFB, 255);
    public static readonly Color ColWatermarkBg = new Color32(0x01, 0x09, 0x2C, 255);

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

    public static Rect GetPanelRect()
    {
        float w = Screen.width * UiState.MenuScale;
        float h = Screen.height * UiState.MenuScale;
        Vector2 pos = UiState.MenuPosition;
        if (pos.x < 0 || pos.y < 0)
            pos = new Vector2((Screen.width - w) / 2f, (Screen.height - h) / 2f);
        return new Rect(pos.x, pos.y, w, h);
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

        Rect panelRect = GetPanelRect();
        HandleDrag(panelRect);

        DrawRect(panelRect, ColPanelBg);

        // Левая колонка — разделы
        float tabX = panelRect.x + 5f;
        float tabY = panelRect.y + 10f;
        if (DrawSectionButton(new Rect(tabX, tabY, TabWidth, 36f), "Main", UiState.MenuSection == 0))
        { UiState.MenuSection = 0; UiState.MenuScroll = 0f; }
        tabY += 39f;
        if (DrawSectionButton(new Rect(tabX, tabY, TabWidth, 36f), "Screen", UiState.MenuSection == 1))
        { UiState.MenuSection = 1; UiState.MenuScroll = 0f; }

        DrawRect(new Rect(panelRect.x + 104f, panelRect.y, 2f, panelRect.height), ColDivider);

        float contentX = panelRect.x + 118f;
        float contentY = panelRect.y + 10f;
        float contentW = panelRect.width - 128f;
        float contentH = panelRect.height - 20f;

        if (UiState.MenuSection == 0) DrawMainSection(contentX, contentY, contentW, contentH);
        else DrawScreenSection(contentX, contentY, contentW, contentH);

        DrawMenuWatermark(panelRect);
    }

    private static void HandleDrag(Rect panelRect)
    {
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Rect handle = new Rect(panelRect.x, panelRect.y, panelRect.width, 26f);
            if (handle.Contains(e.mousePosition) && !UiDragState.AnyDragging)
            {
                UiState.MenuDragging = true;
                UiState.MenuDragOffset = e.mousePosition - new Vector2(panelRect.x, panelRect.y);
                UiDragState.AnyDragging = true;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && UiState.MenuDragging)
        {
            Vector2 newPos = e.mousePosition - UiState.MenuDragOffset;
            newPos.x = Mathf.Clamp(newPos.x, -panelRect.width + 60f, Screen.width - 60f);
            newPos.y = Mathf.Clamp(newPos.y, 0f, Screen.height - 30f);
            UiState.MenuPosition = newPos;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && UiState.MenuDragging)
        {
            UiState.MenuDragging = false;
            UiDragState.AnyDragging = false;
            e.Use();
        }
    }

    private static void DrawMainSection(float bx, float y, float bw, float bh)
    {
        // Вкладки ESP и AIM — одна строка, стабильные клики
        float tabW = 90f;
        float tabH = 28f;
        float gap = 8f;

        Rect espRect = new Rect(bx, y, tabW, tabH);
        Rect aimRect = new Rect(bx + tabW + gap, y, tabW, tabH);

        if (DrawSubTab(espRect, "ESP", UiState.MenuTab == 0)) { UiState.MenuTab = 0; UiState.MenuScroll = 0f; }
        if (DrawSubTab(aimRect, "AIM", UiState.MenuTab == 1)) { UiState.MenuTab = 1; UiState.MenuScroll = 0f; }

        float blockY = y + tabH + 8f;
        float blockH = bh - tabH - 8f;

        DrawScrollableContent(new Rect(bx, blockY, bw, blockH), UiState.MenuTab == 0 ? 0 : 1);
    }

    private static void DrawScreenSection(float bx, float y, float bw, float bh)
    {
        float tabW = 90f;
        float tabH = 28f;

        Rect hudRect = new Rect(bx, y, tabW, tabH);
        if (DrawSubTab(hudRect, "Hud", UiState.MenuTabScreen == 0)) { UiState.MenuTabScreen = 0; UiState.MenuScroll = 0f; }

        float blockY = y + tabH + 8f;
        float blockH = bh - tabH - 8f;

        DrawScrollableContent(new Rect(bx, blockY, bw, blockH), 2);
    }

    // 0 = ESP, 1 = AIM, 2 = Hud
    private static void DrawScrollableContent(Rect view, int tab)
    {
        // Считаем полную высоту контента «виртуально»
        float contentHeight = MeasureContent(tab, view.width - 32f);

        float viewH = view.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewH);

        // Колесо мыши
        Event e = Event.current;
        if (e != null && view.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
        {
            UiState.MenuScroll += e.delta.y * 24f;
            UiState.MenuScroll = Mathf.Clamp(UiState.MenuScroll, 0f, maxScroll);
            e.Use();
        }
        UiState.MenuScroll = Mathf.Clamp(UiState.MenuScroll, 0f, maxScroll);

        // Группа с обрезкой
        GUI.BeginGroup(view);
        Rect inner = new Rect(0f, -UiState.MenuScroll, view.width, contentHeight);

        switch (tab)
        {
            case 0: DrawEspBlock(inner.x, inner.y, inner.width, inner.height); break;
            case 1: DrawAimBlock(inner.x, inner.y, inner.width, inner.height); break;
            default: DrawRenderBlock(inner.x, inner.y, inner.width, inner.height); break;
        }

        GUI.EndGroup();

        // Скроллбар
        if (maxScroll > 0f)
        {
            float sbW = 6f;
            Rect track = new Rect(view.xMax - sbW - 2f, view.y, sbW, viewH);
            DrawRect(track, new Color(1f, 1f, 1f, 0.08f), 3f);
            float thumbH = Mathf.Max(24f, viewH * (viewH / contentHeight));
            float thumbY = view.y + (viewH - thumbH) * (UiState.MenuScroll / maxScroll);
            DrawRect(new Rect(track.x, thumbY, sbW, thumbH), ColDivider, 3f);
        }
    }

    private static float MeasureContent(int tab, float innerW)
    {
        // Приблизительная высота для скролла — считаем по строкам
        switch (tab)
        {
            case 0: // ESP: заголовок + 4 строки по 30
                return 36f + 30f * 4f + 16f;
            case 1: // AIM: заголовок + 3 toggle + 2 label/btn + 1 btn + 1 btn + 1 btn
                return 36f + 30f * 3f + 22f + 28f + 22f + 28f + 28f + 28f + 28f + 16f;
            default: // Hud: заголовок + 3 toggle + label + 2 btn
                return 36f + 30f * 3f + 22f + 28f + 16f;
        }
    }

    private static void DrawMenuWatermark(Rect panelRect)
    {
        if (!UiState.HudWatermark) return;
        string label = $"Explode • Beta • {System.DateTime.Now:HH:mm dd.MM}";

        Vector2 size = GetTextSize(label);
        float wmW = size.x + 24f;
        float wmH = size.y + 10f;
        float wmX = panelRect.x + (panelRect.width - wmW) / 2f;
        float wmY = panelRect.yMax - wmH - 8f;

        DrawRect(new Rect(wmX, wmY, wmW, wmH), ColWatermarkBg, 8f);
        GUI.color = ColText;
        GUI.Label(new Rect(wmX + 12f, wmY + 5f, size.x + 2f, size.y + 2f), label);
        GUI.color = Color.white;
    }

    private static void DrawEspBlock(float x, float y, float bw, float bh)
    {
        DrawRect(new Rect(x, y, bw, bh), ColBlockBg);
        DrawBorder(new Rect(x, y, bw, bh), ColBlockBorder, 1f);

        DrawCentered("ESP", new Rect(x, y + 8f, bw, 20f), ColText, true);
        y += 36f;

        float itemX = x + 16f;
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

    private static void DrawAimBlock(float x, float y, float bw, float bh)
    {
        DrawRect(new Rect(x, y, bw, bh), ColBlockBg);
        DrawBorder(new Rect(x, y, bw, bh), ColBlockBorder, 1f);

        DrawCentered("AIM", new Rect(x, y + 8f, bw, 20f), ColText, true);
        y += 36f;

        float itemX = x + 16f;
        float itemW = bw - 32f;
        float rowH = 30f;

        ToggleRow(new Rect(itemX, y, itemW, rowH), "Enabled", ref AimbotModule.Enabled);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "FOV Circle", ref AimbotModule.DrawFovCircle);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Aim on Teammates", ref AimbotModule.AimOnTeammates);
        y += rowH;

        LabelRow(new Rect(itemX, y, itemW, 20f), $"FOV: {AimbotModule.Fov:F0}°");
        y += 22f;
        if (SmallButton(new Rect(itemX, y, (itemW - 4f) / 2f, 24f), "-"))
        { AimbotModule.Fov = Mathf.Max(1f, AimbotModule.Fov - 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        if (SmallButton(new Rect(itemX + (itemW + 4f) / 2f, y, (itemW - 4f) / 2f, 24f), "+"))
        { AimbotModule.Fov = Mathf.Min(180f, AimbotModule.Fov + 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        y += 28f;

        LabelRow(new Rect(itemX, y, itemW, 20f), $"Sens: {AimbotModule.Sensitivity:F2}");
        y += 22f;
        if (SmallButton(new Rect(itemX, y, (itemW - 4f) / 2f, 24f), "-"))
        { AimbotModule.Sensitivity = Mathf.Max(0.01f, AimbotModule.Sensitivity - 0.05f); Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity; }
        if (SmallButton(new Rect(itemX + (itemW + 4f) / 2f, y, (itemW - 4f) / 2f, 24f), "+"))
        { AimbotModule.Sensitivity = Mathf.Min(3f, AimbotModule.Sensitivity + 0.05f); Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity; }
        y += 28f;

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

    private static void DrawRenderBlock(float x, float y, float bw, float bh)
    {
        DrawRect(new Rect(x, y, bw, bh), ColBlockBg);
        DrawBorder(new Rect(x, y, bw, bh), ColBlockBorder, 1f);

        DrawCentered("Hud", new Rect(x, y + 8f, bw, 20f), ColText, true);
        y += 36f;

        float itemX = x + 16f;
        float itemW = bw - 32f;
        float rowH = 30f;

        ToggleRow(new Rect(itemX, y, itemW, rowH), "Watermark", ref UiState.HudWatermark);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show Bind List", ref BindListState.Enabled);
        y += rowH;
        ToggleRow(new Rect(itemX, y, itemW, rowH), "Show Bomb Status", ref BombStatusRenderer.Enabled);
        y += rowH;

        LabelRow(new Rect(itemX, y, itemW, 20f), $"Menu size: {UiState.MenuScale * 100f:F0}%");
        y += 22f;
        if (SmallButton(new Rect(itemX, y, (itemW - 4f) / 2f, 24f), "-"))
        {
            UiState.MenuScale = Mathf.Clamp(UiState.MenuScale - 0.05f, 0.25f, 2f);
            Plugin.CfgMenuScale.Value = UiState.MenuScale;
            // Пересчёт позиции, чтобы меню не улетало
            UiState.MenuPosition = new Vector2(
                Mathf.Clamp(UiState.MenuPosition.x, 0f, Screen.width - Screen.width * UiState.MenuScale),
                Mathf.Clamp(UiState.MenuPosition.y, 0f, Screen.height - Screen.height * UiState.MenuScale));
        }
        if (SmallButton(new Rect(itemX + (itemW + 4f) / 2f, y, (itemW - 4f) / 2f, 24f), "+"))
        {
            UiState.MenuScale = Mathf.Clamp(UiState.MenuScale + 0.05f, 0.25f, 2f);
            Plugin.CfgMenuScale.Value = UiState.MenuScale;
            UiState.MenuPosition = new Vector2(
                Mathf.Clamp(UiState.MenuPosition.x, 0f, Screen.width - Screen.width * UiState.MenuScale),
                Mathf.Clamp(UiState.MenuPosition.y, 0f, Screen.height - Screen.height * UiState.MenuScale));
        }
    }

    // === Хелперы ===

    private static bool DrawSectionButton(Rect rect, string label, bool active)
    {
        if (active) DrawRect(rect, ColTabActive, 9f);
        bool clicked = ClickZone(rect);
        DrawCentered(label, rect, active ? Color.white : ColText, false);
        return clicked;
    }

    private static bool DrawSubTab(Rect rect, string label, bool active)
    {
        DrawRect(rect, active ? ColTabActive : ColBlockBorder, 6f);
        bool clicked = ClickZone(rect);
        DrawCentered(label, rect, active ? Color.white : ColText, false);
        return clicked;
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
        DrawRect(new Rect(knobX, rect.y + (rect.height - knobSize) / 2f, knobSize, knobSize), ColToggleKnob, 8.5f);

        if (ClickZone(rect)) value = !value;
    }

    private static void LabelRow(Rect rect, string label) => DrawLeft(label, rect, ColText);

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
        DrawRoundedRect(rect, col, radius);
    }

    private static void DrawRoundedRect(Rect rect, Color col, float radius)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(rect.x + radius, rect.y, rect.width - radius * 2f, rect.height), _whiteTex);
        GUI.DrawTexture(new Rect(rect.x, rect.y + radius, rect.width, rect.height - radius * 2f), _whiteTex);

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
