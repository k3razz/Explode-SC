using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class MenuRenderer
{
    public const float MenuWidth = 500f;
    public const float MenuHeight = 540f;
    public const float TabWidth = 120f;

    private static Texture2D _circleTex;
    private static Texture2D _ringTex;
    private static readonly Dictionary<string, Vector2> _textCache = new();

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
        else bgAlpha = 0f;

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
        else logoAlpha = 0f;

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
        float sw = Screen.width, sh = Screen.height;
        float mw = MenuWidth, mh = MenuHeight;
        Rect panelRect = new Rect((sw - mw) / 2f, (sh - mh) / 2f, mw, mh);

        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        GUI.DrawTexture(panelRect, whiteTex);
        GUI.color = Color.white;

        float bx = panelRect.x + 10, by = panelRect.y + 10;
        float contentX = bx + TabWidth + 10f;
        float contentW = panelRect.width - (TabWidth + 30f);

        GUI.Label(new Rect(bx, by, panelRect.width - 20f, 24f), "<b>Explode</b>");
        by += 30;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "ESP", UiState.MenuTab == 0, whiteTex)) UiState.MenuTab = 0;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "AimAssist", UiState.MenuTab == 1, whiteTex)) UiState.MenuTab = 1;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "Render", UiState.MenuTab == 2, whiteTex)) UiState.MenuTab = 2;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "Hide", false, whiteTex))
        {
            KeybindState.MenuOpen = false;
            UiState.MenuVisible = false;
        }

        float cy = panelRect.y + 40;
        if (UiState.MenuTab == 0) DrawEspTab(contentX, cy, contentW, whiteTex);
        else if (UiState.MenuTab == 1) DrawAimAssistTab(contentX, cy, contentW, whiteTex);
        else if (UiState.MenuTab == 2) DrawRenderTab(contentX, cy, contentW, whiteTex);
    }

    private static void DrawRenderTab(float bx, float y, float bw, Texture2D whiteTex)
    {
        float bh = 30;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Watermark: {(WatermarkModule.Enabled ? "ON" : "OFF")}", WatermarkModule.Enabled, whiteTex))
        {
            WatermarkModule.Enabled = !WatermarkModule.Enabled;
            Plugin.CfgWatermarkEnabled.Value = WatermarkModule.Enabled;
        }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), "Reset Watermark Position", false, whiteTex))
        {
            WatermarkModule.Position = new Vector2(Screen.width / 2f, Screen.height - 40f);
            Plugin.CfgWatermarkX.Value = WatermarkModule.Position.x;
            Plugin.CfgWatermarkY.Value = WatermarkModule.Position.y;
        }
        y += 46;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Show Bind List: {(BindListState.Enabled ? "ON" : "OFF")}", BindListState.Enabled, whiteTex))
        {
            BindListState.Enabled = !BindListState.Enabled;
            Plugin.CfgShowBindList.Value = BindListState.Enabled;
        }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Show Bomb Status: {(BombStatusRenderer.Enabled ? "ON" : "OFF")}", BombStatusRenderer.Enabled, whiteTex))
        {
            BombStatusRenderer.Enabled = !BombStatusRenderer.Enabled;
            Plugin.CfgShowBombStatus.Value = BombStatusRenderer.Enabled;
        }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), "Panels shown in the top-right corner.");
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
    }

    private static void DrawAimAssistTab(float bx, float y, float bw, Texture2D whiteTex)
    {
        float bh = 30;

        if (CustomButton(new Rect(bx, y, bw, bh), $"AimAssist: {(AimbotModule.Enabled ? "ON" : "OFF")}", AimbotModule.Enabled, whiteTex))
        { AimbotModule.Enabled = !AimbotModule.Enabled; Plugin.CfgAimbotEnabled.Value = AimbotModule.Enabled; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"FOV Circle: {(AimbotModule.DrawFovCircle ? "ON" : "OFF")}", AimbotModule.DrawFovCircle, whiteTex))
        { AimbotModule.DrawFovCircle = !AimbotModule.DrawFovCircle; Plugin.CfgAimbotDrawFov.Value = AimbotModule.DrawFovCircle; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>FOV:</b> {AimbotModule.Fov:F0}°");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false, whiteTex))
        { AimbotModule.Fov = Mathf.Max(1f, AimbotModule.Fov - 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true, whiteTex))
        { AimbotModule.Fov = Mathf.Min(180f, AimbotModule.Fov + 2f); Plugin.CfgAimbotFov.Value = AimbotModule.Fov; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>Sensitivity:</b> {AimbotModule.Sensitivity:F2}");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false, whiteTex))
        { AimbotModule.Sensitivity = Mathf.Max(0.01f, AimbotModule.Sensitivity - 0.05f); Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity; }
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true, whiteTex))
        { AimbotModule.Sensitivity = Mathf.Min(1.5f, AimbotModule.Sensitivity + 0.05f); Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Bone: {AimbotModule.Bone}", true, whiteTex))
        {
            var next = AimbotModule.Bone == AimBone.Head ? AimBone.Neck
                     : AimbotModule.Bone == AimBone.Neck ? AimBone.Body
                     : AimBone.Head;
            AimbotModule.Bone = next;
            Plugin.CfgAimbotBone.Value = next.ToString();
        }
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

        Vector2 size = GetTextSize(label);
        float x = rect.x + (rect.width - size.x) / 2f;
        float y = rect.y + (rect.height - size.y) / 2f;
        GUI.Label(new Rect(x, y, size.x + 2f, size.y + 2f), label);
        return clicked;
    }

    private static bool TabButton(Rect rect, string label, bool active, Texture2D whiteTex)
    {
        Event e = Event.current;
        bool clicked = e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        GUI.color = active ? new Color(0.2f, 0.4f, 0.7f, 0.95f) : new Color(0.15f, 0.15f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = Color.white;

        Vector2 size = GetTextSize(label);
        float x = rect.x + (rect.width - size.x) / 2f;
        float y = rect.y + (rect.height - size.y) / 2f;
        GUI.Label(new Rect(x, y, size.x + 2f, size.y + 2f), label);
        return clicked;
    }
}