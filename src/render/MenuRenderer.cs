using UnityEngine;

namespace MyDynamicESP;

public static class MenuRenderer
{
    public const float MenuWidth = 500f;
    public const float MenuHeight = 540f;
    public const float TabWidth = 120f;

    // Тайминги анимации (секунды)
    private const float FadeBgStart = 0.0f;
    private const float FadeBgEnd = 0.3f;
    private const float FadeLogoStart = 0.2f;
    private const float FadeLogoEnd = 0.6f;
    private const float FadeMenuStart = 0.5f;
    private const float FadeMenuEnd = 0.9f;

    private static Texture2D _circleTex;
    private static Texture2D _ringTex;

    public static void EnsureTextures()
    {
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

    public static void DrawFovCircle()
    {
        MenuRenderer.EnsureTextures();
        float fovScreen = AimbotModule.Fov * (Screen.height / 90f);
        if (fovScreen <= 0) return;
        GUI.color = new Color(1f, 1f, 1f, 0.04f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - fovScreen / 2f, Screen.height / 2f - fovScreen / 2f, fovScreen, fovScreen), _circleTex);
        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - fovScreen / 2f, Screen.height / 2f - fovScreen / 2f, fovScreen, fovScreen), _ringTex);
        GUI.color = Color.white;
    }

    // === Анимация меню: фон → логотип → меню, всё по центру ===
    public static void DrawMenuAnimation(Texture2D whiteTex, float animTime)
    {
        float sw = Screen.width, sh = Screen.height;

        // 1. Тёмно-серый полупрозрачный фон
        float bgAlpha = Mathf.Clamp01((animTime - FadeBgStart) / (FadeBgEnd - FadeBgStart)) * 0.65f;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, bgAlpha);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), whiteTex);
        GUI.color = Color.white;

        // 2. Надпись "Explode" — E красная, остальное белое
        float logoAlpha = Mathf.Clamp01((animTime - FadeLogoStart) / (FadeLogoEnd - FadeLogoStart));
        if (logoAlpha > 0f)
        {
            string eLetter = "E";
            string rest = "xplode";
            float logoY = sh * 0.25f;
            float fontSize = 72f;
            // Рисуем каждую часть по центру, вычисляя ширину через GUI.skin.label.CalcSize
            var style = GUI.skin.label;
            Vector2 sizeE = style.CalcSize(new GUIContent(eLetter));
            Vector2 sizeRest = style.CalcSize(new GUIContent(rest));
            float totalWidth = sizeE.x + sizeRest.x;
            float startX = sw / 2f - totalWidth / 2f;

            // Тень
            GUI.color = new Color(0f, 0f, 0f, logoAlpha * 0.6f);
            GUI.Label(new Rect(startX + 2f, logoY + 2f, totalWidth, 80f), eLetter + rest);
            // E красная
            GUI.color = new Color(1f, 0.2f, 0.2f, logoAlpha);
            GUI.Label(new Rect(startX, logoY, sizeE.x + 4f, 80f), eLetter);
            // xplode белое
            GUI.color = new Color(1f, 1f, 1f, logoAlpha);
            GUI.Label(new Rect(startX + sizeE.x, logoY, sizeRest.x + 4f, 80f), rest);
            GUI.color = Color.white;
        }

        // 3. Меню с плавным появлением
        float menuAlpha = Mathf.Clamp01((animTime - FadeMenuStart) / (FadeMenuEnd - FadeMenuStart));
        if (menuAlpha > 0f)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, menuAlpha);
            DrawMenuContent(whiteTex);
            GUI.color = oldColor;
        }
    }

    // === Само меню (строго по центру) ===
    private static void DrawMenuContent(Texture2D whiteTex)
    {
        float sw = Screen.width, sh = Screen.height;
        float mw = MenuWidth, mh = MenuHeight;
        Rect panelRect = new Rect((sw - mw) / 2f, (sh - mh) / 2f, mw, mh);

        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        GUI.DrawTexture(panelRect, whiteTex);
        GUI.color = Color.white;

        float bx = panelRect.x + 10, by = panelRect.y + 10;
        float contentX = bx + TabWidth + 10f;
        float contentW = panelRect.width - (TabWidth + 30f);

        GUI.Label(new Rect(bx, by, panelRect.width - 20f, 24f), "<b>DynamicESP</b>");
        by += 30;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "ESP", UiState.MenuTab == 0, whiteTex)) UiState.MenuTab = 0;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "AimBot", UiState.MenuTab == 1, whiteTex)) UiState.MenuTab = 1;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "Hide", false, whiteTex))
        {
            KeybindState.MenuOpen = false;
            UiState.MenuVisible = false;
        }

        float cy = panelRect.y + 40;
        if (UiState.MenuTab == 0) DrawEspTab(contentX, cy, contentW, whiteTex);
        else DrawAimTab(contentX, cy, contentW, whiteTex);
    }

    private static void DrawEspTab(float bx, float y, float bw, Texture2D whiteTex)
    {
        float bh = 30;
        if (CustomButton(new Rect(bx, y, bw, bh), $"ESP Boxes: {(EspModule.BoxesEnabled ? "ON" : "OFF")}", EspModule.BoxesEnabled, whiteTex))
        { EspModule.BoxesEnabled = !EspModule.BoxesEnabled; Plugin.CfgEspBoxes.Value = EspModule.BoxesEnabled; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Names: {(EspModule.NamesEnabled ? "ON" : "OFF")}", EspModule.NamesEnabled, whiteTex))
        { EspModule.NamesEnabled = !EspModule.NamesEnabled; Plugin.CfgEspNames.Value = EspModule.NamesEnabled; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Distance: {(EspModule.DistanceEnabled ? "ON" : "OFF")}", EspModule.DistanceEnabled, whiteTex))
        { EspModule.DistanceEnabled = !EspModule.DistanceEnabled; Plugin.CfgEspDistance.Value = EspModule.DistanceEnabled; }
        y += 46;

        GUI.Label(new Rect(bx, y, bw, 22), "<b>Team filter:</b>");
        y += 26;
        if (CustomButton(new Rect(bx, y, bw, bh), $"TR: {(TeamFilter.ShowTr ? "ON" : "OFF")}", TeamFilter.ShowTr, whiteTex))
        { TeamFilter.ShowTr = !TeamFilter.ShowTr; Plugin.CfgShowTr.Value = TeamFilter.ShowTr; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"CT: {(TeamFilter.ShowCt ? "ON" : "OFF")}", TeamFilter.ShowCt, whiteTex))
        { TeamFilter.ShowCt = !TeamFilter.ShowCt; Plugin.CfgShowCt.Value = TeamFilter.ShowCt; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Spec: {(TeamFilter.ShowSpectator ? "ON" : "OFF")}", TeamFilter.ShowSpectator, whiteTex))
        { TeamFilter.ShowSpectator = !TeamFilter.ShowSpectator; Plugin.CfgShowSpectator.Value = TeamFilter.ShowSpectator; }
        y += 46;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Only enemies: {(TeamFilter.OnlyEnemies ? "ON" : "OFF")}", TeamFilter.OnlyEnemies, whiteTex))
        { TeamFilter.OnlyEnemies = !TeamFilter.OnlyEnemies; Plugin.CfgOnlyEnemies.Value = TeamFilter.OnlyEnemies; }
    }

    private static void DrawAimTab(float bx, float y, float bw, Texture2D whiteTex)
    {
        float bh = 30;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Aimbot: {(AimbotModule.Enabled ? "ON" : "OFF")}", AimbotModule.Enabled, whiteTex))
        { AimbotModule.Enabled = !AimbotModule.Enabled; Plugin.CfgAimbotEnabled.Value = AimbotModule.Enabled; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"FOV Circle: {(AimbotModule.DrawFovCircle ? "ON" : "OFF")}", AimbotModule.DrawFovCircle, whiteTex))
        { AimbotModule.DrawFovCircle = !AimbotModule.DrawFovCircle; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>FOV:</b> {AimbotModule.Fov:F0}°");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false, whiteTex))
        { AimbotModule.Fov = Mathf.Max(1f, AimbotModule.Fov - 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true, whiteTex))
        { AimbotModule.Fov = Mathf.Min(90f, AimbotModule.Fov + 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>Smooth:</b> {AimbotModule.Smooth:F0}");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false, whiteTex))
        { AimbotModule.Smooth = Mathf.Max(1f, AimbotModule.Smooth - 5f); Plugin.CfgAimbotSmooth.Value = AimbotModule.Smooth; }
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true, whiteTex))
        { AimbotModule.Smooth = Mathf.Min(50f, AimbotModule.Smooth + 5f); Plugin.CfgAimbotSmooth.Value = AimbotModule.Smooth; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Aim Teammates: {(AimbotModule.TargetTeammates ? "ON" : "OFF")}", AimbotModule.TargetTeammates, whiteTex))
        { AimbotModule.TargetTeammates = !AimbotModule.TargetTeammates; Plugin.CfgAimbotTargetTeammates.Value = AimbotModule.TargetTeammates; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Visibility: {(AimbotModule.VisibilityCheck ? "ON" : "OFF")}", AimbotModule.VisibilityCheck, whiteTex))
        { AimbotModule.VisibilityCheck = !AimbotModule.VisibilityCheck; Plugin.CfgAimbotVisibilityCheck.Value = AimbotModule.VisibilityCheck; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Mode: {(KeybindState.ToggleMode ? "TOGGLE" : "HOLD")}", KeybindState.ToggleMode, whiteTex))
        {
            KeybindState.ToggleMode = !KeybindState.ToggleMode;
            Plugin.CfgAimbotToggleMode.Value = KeybindState.ToggleMode;
        }
        y += 36;

        string keyName = KeybindState.Capturing ? "PRESS KEY... (Esc)" : KeybindState.AimbotKey.ToString();
        if (CustomButton(new Rect(bx, y, bw, bh), $"Bind: {keyName}", KeybindState.Capturing, whiteTex))
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

    private static bool CustomButton(Rect rect, string label, bool state, Texture2D whiteTex)
    {
        Event e = Event.current;
        bool clicked = e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        GUI.color = state ? new Color(0.2f, 0.55f, 0.3f, 0.95f) : new Color(0.55f, 0.2f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = Color.white;
        float lw = Mathf.Min(label.Length * 7.5f + 4f, rect.width - 6f);
        GUI.Label(new Rect(rect.x + (rect.width - lw) / 2f, rect.y + (rect.height - 16f) / 2f, lw, 16f), label);
        return clicked;
    }

    private static bool TabButton(Rect rect, string label, bool active, Texture2D whiteTex)
    {
        Event e = Event.current;
        bool clicked = e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        GUI.color = active ? new Color(0.2f, 0.4f, 0.7f, 0.95f) : new Color(0.15f, 0.15f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = Color.white;
        float lw = Mathf.Min(label.Length * 7.5f + 4f, rect.width - 6f);
        GUI.Label(new Rect(rect.x + (rect.width - lw) / 2f, rect.y + (rect.height - 16f) / 2f, lw, 16f), label);
        return clicked;
    }
}
