using UnityEngine;

namespace MyDynamicESP;

public static class MenuRenderer
{
    public const float MenuWidth = 500f;
    public const float MenuHeight = 540f;
    public const float MenuOffset = 30f;
    public const float TabWidth = 120f;
    public const float ButtonRadius = 22f;

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

    public static void DrawButton(Texture2D whiteTex)
    {
        if (!UiState.ButtonInitialized)
        {
            UiState.ButtonPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            UiState.ButtonInitialized = true;
        }

        float r = ButtonRadius;
        Rect btnRect = new Rect(UiState.ButtonPos.x - r, UiState.ButtonPos.y - r, r * 2f, r * 2f);
        if (GUI.Button(btnRect, "E"))
        {
            UiState.MenuVisible = true;
        }
    }

    public static void DrawMenu(Texture2D whiteTex)
    {
        Rect panelRect = GetMenuRect();
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        GUI.DrawTexture(panelRect, whiteTex);
        GUI.color = Color.white;

        float bx = panelRect.x + 10, by = panelRect.y + 10;
        float contentX = bx + TabWidth + 10f;
        float contentW = panelRect.width - (TabWidth + 30f);

        GUI.Label(new Rect(bx, by, panelRect.width - 20f, 24f), "<b>DynamicESP</b>");
        by += 30;
        if (MenuRenderer.TabButton(new Rect(bx, by, TabWidth, 30f), "ESP", UiState.MenuTab == 0, whiteTex)) UiState.MenuTab = 0;
        by += 34;
        if (MenuRenderer.TabButton(new Rect(bx, by, TabWidth, 30f), "AimBot", UiState.MenuTab == 1, whiteTex)) UiState.MenuTab = 1;
        by += 34;
        if (MenuRenderer.TabButton(new Rect(bx, by, TabWidth, 30f), "Hide", false, whiteTex)) UiState.MenuVisible = false;

        float cy = panelRect.y + 40;
        if (UiState.MenuTab == 0) MenuRenderer.DrawEspTab(contentX, cy, contentW, whiteTex);
        else MenuRenderer.DrawAimTab(contentX, cy, contentW, whiteTex);
    }

    private static void DrawEspTab(float bx, float y, float bw, Texture2D whiteTex)
    {
        float bh = 30;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"ESP Boxes: {(EspModule.BoxesEnabled ? "ON" : "OFF")}", EspModule.BoxesEnabled, whiteTex))
        { EspModule.BoxesEnabled = !EspModule.BoxesEnabled; Plugin.CfgEspBoxes.Value = EspModule.BoxesEnabled; }
        y += 36;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Names: {(EspModule.NamesEnabled ? "ON" : "OFF")}", EspModule.NamesEnabled, whiteTex))
        { EspModule.NamesEnabled = !EspModule.NamesEnabled; Plugin.CfgEspNames.Value = EspModule.NamesEnabled; }
        y += 36;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Distance: {(EspModule.DistanceEnabled ? "ON" : "OFF")}", EspModule.DistanceEnabled, whiteTex))
        { EspModule.DistanceEnabled = !EspModule.DistanceEnabled; Plugin.CfgEspDistance.Value = EspModule.DistanceEnabled; }
        y += 46;

        GUI.Label(new Rect(bx, y, bw, 22), "<b>Team filter:</b>");
        y += 26;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"TR: {(TeamFilter.ShowTr ? "ON" : "OFF")}", TeamFilter.ShowTr, whiteTex))
        { TeamFilter.ShowTr = !TeamFilter.ShowTr; Plugin.CfgShowTr.Value = TeamFilter.ShowTr; }
        y += 36;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"CT: {(TeamFilter.ShowCt ? "ON" : "OFF")}", TeamFilter.ShowCt, whiteTex))
        { TeamFilter.ShowCt = !TeamFilter.ShowCt; Plugin.CfgShowCt.Value = TeamFilter.ShowCt; }
        y += 36;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Spec: {(TeamFilter.ShowSpectator ? "ON" : "OFF")}", TeamFilter.ShowSpectator, whiteTex))
        { TeamFilter.ShowSpectator = !TeamFilter.ShowSpectator; Plugin.CfgShowSpectator.Value = TeamFilter.ShowSpectator; }
        y += 46;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Only enemies: {(TeamFilter.OnlyEnemies ? "ON" : "OFF")}", TeamFilter.OnlyEnemies, whiteTex))
        { TeamFilter.OnlyEnemies = !TeamFilter.OnlyEnemies; Plugin.CfgOnlyEnemies.Value = TeamFilter.OnlyEnemies; }
    }

    private static void DrawAimTab(float bx, float y, float bw, Texture2D whiteTex)
    {
        float bh = 30;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Aimbot: {(AimbotModule.Enabled ? "ON" : "OFF")}", AimbotModule.Enabled, whiteTex))
        { AimbotModule.Enabled = !AimbotModule.Enabled; Plugin.CfgAimbotEnabled.Value = AimbotModule.Enabled; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>FOV:</b> {AimbotModule.Fov:F0}°");
        y += 24;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false, whiteTex))
        { AimbotModule.Fov = Mathf.Max(1f, AimbotModule.Fov - 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        if (MenuRenderer.CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true, whiteTex))
        { AimbotModule.Fov = Mathf.Min(90f, AimbotModule.Fov + 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>Smooth:</b> {AimbotModule.Smooth:F0}");
        y += 24;
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false, whiteTex))
        { AimbotModule.Smooth = Mathf.Max(1f, AimbotModule.Smooth - 5f); Plugin.CfgAimbotSmooth.Value = AimbotModule.Smooth; }
        if (MenuRenderer.CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true, whiteTex))
        { AimbotModule.Smooth = Mathf.Min(50f, AimbotModule.Smooth + 5f); Plugin.CfgAimbotSmooth.Value = AimbotModule.Smooth; }
        y += 36;

        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Aim Teammates: {(AimbotModule.TargetTeammates ? "ON" : "OFF")}", AimbotModule.TargetTeammates, whiteTex))
        { AimbotModule.TargetTeammates = !AimbotModule.TargetTeammates; Plugin.CfgAimbotTargetTeammates.Value = AimbotModule.TargetTeammates; }
        y += 36;

        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Visibility: {(AimbotModule.VisibilityCheck ? "ON" : "OFF")}", AimbotModule.VisibilityCheck, whiteTex))
        { AimbotModule.VisibilityCheck = !AimbotModule.VisibilityCheck; Plugin.CfgAimbotVisibilityCheck.Value = AimbotModule.VisibilityCheck; }
        y += 36;

        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Mode: {(KeybindState.ToggleMode ? "TOGGLE" : "HOLD")}", KeybindState.ToggleMode, whiteTex))
        {
            KeybindState.ToggleMode = !KeybindState.ToggleMode;
            Plugin.CfgAimbotToggleMode.Value = KeybindState.ToggleMode;
        }
        y += 36;

        string keyName = KeybindState.Capturing ? "PRESS KEY... (Esc)" : KeybindState.AimbotKey.ToString();
        if (MenuRenderer.CustomButton(new Rect(bx, y, bw, bh), $"Bind: {keyName}", KeybindState.Capturing, whiteTex))
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

    private static Rect GetMenuRect()
    {
        float sw = Screen.width, sh = Screen.height, mw = MenuWidth, mh = MenuHeight;
        float x = UiState.ButtonPos.x + 22f + MenuOffset;
        if (x + mw > sw) x = UiState.ButtonPos.x - 22f - MenuOffset - mw;
        if (x < 0) x = Mathf.Max(0, (sw - mw) / 2f);
        float y = UiState.ButtonPos.y - mh / 2f;
        if (y < 0) y = 0;
        if (y + mh > sh) y = sh - mh;
        return new Rect(x, y, mw, mh);
    }
}
