using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyDynamicESP;

public static class MenuRenderer
{
    public const float TabWidth = 94f;
    public const float Radius = 10f;

    public const float BaseWidth = 560f;
    public const float BaseHeight = 400f;
    public const float MinScale = 0.25f;
    public const float MaxScale = 2f;

    public const float IntroDuration = 3.3f;

    private const float HeaderH = 36f;
    private const float Pad = 10f;
    private const float SidebarDividerX = 104f;
    private const float ContentX = 118f;
    private const int SectionCount = 3;

    private const string HeaderTitle = "<color=#FF8C1A>E</color>xplode";

    public static readonly Color ColPanelBg = new Color32(0x0B, 0x0F, 0x1A, 255);
    public static readonly Color ColTabActive = new Color32(0x1B, 0x23, 0x40, 255);
    public static readonly Color ColDivider = new Color32(0xA0, 0xB2, 0xDE, 255);
    public static readonly Color ColBlockBg = new Color32(0x11, 0x17, 0x2A, 255);
    public static readonly Color ColBlockBorder = new Color32(0x23, 0x2B, 0x45, 255);
    public static readonly Color ColText = new Color32(0xD5, 0xD9, 0xE6, 255);
    public static readonly Color ColTextDim = new Color32(0x7C, 0x86, 0xA2, 255);
    public static readonly Color ColToggleTrack = new Color32(0x1E, 0x26, 0x40, 255);
    public static readonly Color ColToggleKnob = new Color32(0xFB, 0xFB, 0xFB, 255);
    public static readonly Color ColAccent = new Color32(0x5B, 0x8C, 0xFF, 255);
    public static readonly Color ColOrange = new Color32(0xFF, 0x8C, 0x1A, 255);

    private static readonly Color ColLine = new Color(0.627f, 0.698f, 0.871f, 0.12f);
    private static readonly Color ColButtonHover = new Color32(0x2E, 0x39, 0x5C, 255);

    private static readonly string[] BoneNames = { "Neck", "Head", "Chest" };
    private static readonly string[] ModeNames = { "Hold", "Toggle" };

    private static Texture2D _white, _circle, _ring;
    private static readonly Dictionary<int, GUIStyle> _fillStyles = new();
    private static readonly Dictionary<int, GUIStyle> _outlineStyles = new();
    private static readonly Dictionary<int, float> _anim = new();

    private static Font _font;
    private static GUIStyle _sLeft, _sRight, _sCenter, _sTitle, _sBold, _sIntro;

    private static float _alpha = 1f;
    private static bool _inputOk = true;
    private static float _open;
    private static int _lastFrame = -10;
    private static readonly float[] _contentH = new float[SectionCount];
    private static float _ix, _iw, _y;
    private static float _sbGrab;

    private static int _introFs;
    private static float _introWE, _introWR;

    private static int _openDropdown = -1;
    private static Rect _dropdownRect;
    private static string[] _dropdownOptions;
    private static int _dropdownSelected;
    private static Action<int> _dropdownCallback;

    public static void EnsureTextures()
    {
        if (_white == null)
        {
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }
        if (_circle == null) _circle = MakeCircle(256, false, 0f);
        if (_ring == null) _ring = MakeCircle(256, true, 2.2f);
    }

    private static void EnsureStyles()
    {
        if (_sLeft != null) return;

        try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Inter", "Roboto", "Arial" }, 14); }
        catch { _font = null; }

        _sLeft = MakeStyle(TextAnchor.MiddleLeft, 13, FontStyle.Normal, true);
        _sRight = MakeStyle(TextAnchor.MiddleRight, 13, FontStyle.Normal, true);
        _sCenter = MakeStyle(TextAnchor.MiddleCenter, 12, FontStyle.Normal, true);
        _sTitle = MakeStyle(TextAnchor.MiddleLeft, 15, FontStyle.Bold, true);
        _sBold = MakeStyle(TextAnchor.MiddleCenter, 14, FontStyle.Bold, true);
        _sIntro = MakeStyle(TextAnchor.MiddleLeft, 80, FontStyle.Bold, false);
    }

    private static GUIStyle MakeStyle(TextAnchor anchor, int size, FontStyle fs, bool rich)
    {
        var s = new GUIStyle
        {
            alignment = anchor,
            fontSize = size,
            fontStyle = fs,
            richText = rich,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        if (_font != null) s.font = _font;
        s.normal.textColor = Color.white;
        return s;
    }

    private static Texture2D MakeCircle(int size, bool ring, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[size * size];
        float c = size * 0.5f;
        float outer = c - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - outer;
                float a = Mathf.Clamp01(0.5f - d);
                if (ring) a = Mathf.Clamp01(a - Mathf.Clamp01(0.5f - d - thickness));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static GUIStyle RoundedStyle(float radius, bool outline)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 64);
        var dict = outline ? _outlineStyles : _fillStyles;
        if (dict.TryGetValue(r, out var cached) && cached.normal.background != null) return cached;

        int n = r * 2 + 2;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[n * n];
        float half = n * 0.5f;
        float inner = half - r;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                float a = Mathf.Clamp01(0.5f - d);
                if (outline) a = Mathf.Clamp01(a - Mathf.Clamp01(-0.5f - d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();

        var st = new GUIStyle { border = new RectOffset(r, r, r, r) };
        st.normal.background = tex;
        dict[r] = st;
        return st;
    }

    private static bool IsRepaint() => Event.current != null && Event.current.type == EventType.Repaint;
    private static Color Tint(Color c) => new Color(c.r, c.g, c.b, c.a * _alpha);
    private static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    private static int Key(string s, int salt) => s.GetHashCode() * 397 ^ (UiState.MenuSection * 7919 + salt);

    private static float Anim(int key, float target, float speed)
    {
        if (!_anim.TryGetValue(key, out float v)) { v = target; _anim[key] = v; }
        if (IsRepaint())
        {
            v = Mathf.MoveTowards(v, target, Time.unscaledDeltaTime * speed);
            _anim[key] = v;
        }
        return v;
    }

    private static float Scale()
    {
        float s = Mathf.Clamp(UiState.MenuScale, MinScale, MaxScale);
        float fit = Mathf.Min((Screen.width - 20f) / BaseWidth, (Screen.height - 20f) / BaseHeight);
        return Mathf.Max(0.3f, Mathf.Min(s, fit));
    }

    public static Rect GetPanelRect()
    {
        float s = Scale();
        float w = BaseWidth * s, h = BaseHeight * s;
        Vector2 p = UiState.MenuPosition;
        if (!UiState.MenuPositionSet || p.x < 0f || p.y < 0f)
        {
            p = new Vector2((Screen.width - w) / 2f, (Screen.height - h) / 2f);
            UiState.MenuPosition = p;
            UiState.MenuPositionSet = true;
        }
        p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - w));
        p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - h));
        return new Rect(p.x, p.y, w, h);
    }

    public static void DrawRect(Rect rect, Color col, float radius = 0f)
    {
        if (!IsRepaint()) return;
        EnsureTextures();
        GUI.color = Tint(col);
        if (radius <= 0.5f) GUI.DrawTexture(rect, _white);
        else
        {
            float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            RoundedStyle(r, false).Draw(rect, GUIContent.none, false, false, false, false);
        }
        GUI.color = Color.white;
    }

    public static void DrawRoundedRect(Color col, Rect rect, float radius) => DrawRect(rect, col, radius);

    private static void DrawOutline(Rect rect, Color col, float radius)
    {
        if (!IsRepaint()) return;
        EnsureTextures();
        GUI.color = Tint(col);
        float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
        RoundedStyle(Mathf.Max(1f, r), true).Draw(rect, GUIContent.none, false, false, false, false);
        GUI.color = Color.white;
    }

    private static void Text(Rect r, string s, GUIStyle st, Color c)
    {
        if (!IsRepaint()) return;
        GUI.color = Tint(c);
        GUI.Label(r, s, st);
        GUI.color = Color.white;
    }

    private static bool Click(Rect r)
    {
        Event e = Event.current;
        if (_inputOk && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
        {
            e.Use();
            return true;
        }
        return false;
    }

    private static bool Hover(Rect r)
    {
        Event e = Event.current;
        if (e == null) return false;
        return _inputOk && r.Contains(e.mousePosition);
    }

    public static void DrawFovCircle()
    {
        if (!IsRepaint()) return;
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
        GUI.DrawTexture(rect, _circle);
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(rect, _ring);
        GUI.color = Color.white;
    }

    public static void DrawIntroAnimation(Texture2D whiteTex, float t)
    {
        if (!IsRepaint()) return;
        EnsureTextures();
        EnsureStyles();

        float sw = Screen.width, sh = Screen.height;

        float master = 1f - Smooth01((t - (IntroDuration - 0.4f)) / 0.4f);
        float bgA = 0.65f * Smooth01(t / 0.3f) * master;
        if (bgA > 0.001f)
        {
            GUI.color = new Color(0.04f, 0.05f, 0.09f, bgA);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }
        if (master <= 0.001f) return;

        int fs = Mathf.Clamp(Mathf.RoundToInt(sh * 0.11f), 56, 140);
        if (fs != _introFs)
        {
            _introFs = fs;
            _sIntro.fontSize = fs;
            _introWE = _sIntro.CalcSize(new GUIContent("E")).x;
            _introWR = _sIntro.CalcSize(new GUIContent("xplode")).x;
        }

        float eAlpha = Smooth01(t / 0.35f) * master;
        float paint = Smooth01((t - 0.75f) / 0.6f);
        float move = Smooth01((t - 1.45f) / 0.7f);
        float restA = Smooth01((t - 2.1f) / 0.6f) * master;

        float total = _introWE + _introWR;
        float xFinal = sw / 2f - total / 2f;
        float xStart = sw / 2f - _introWE / 2f;
        float xE = Mathf.Lerp(xStart, xFinal, move);

        float h = fs * 1.6f;
        float y = sh / 2f - h / 2f;
        Color eCol = Color.Lerp(Color.white, ColOrange, paint);

        GUI.color = new Color(0f, 0f, 0f, eAlpha * 0.55f);
        GUI.Label(new Rect(xE + 3f, y + 3f, _introWE + 16f, h), "E", _sIntro);
        GUI.color = new Color(0f, 0f, 0f, restA * 0.55f);
        GUI.Label(new Rect(xFinal + _introWE + 3f, y + 3f, _introWR + 16f, h), "xplode", _sIntro);

        GUI.color = new Color(eCol.r, eCol.g, eCol.b, eAlpha);
        GUI.Label(new Rect(xE, y, _introWE + 16f, h), "E", _sIntro);
        GUI.color = new Color(1f, 1f, 1f, restA);
        GUI.Label(new Rect(xFinal + _introWE, y, _introWR + 16f, h), "xplode", _sIntro);

        GUI.color = Color.white;
    }

    public static void DrawMenu(Texture2D whiteTex)
    {
        Event e = Event.current;
        if (e == null) return;
        EnsureTextures();
        EnsureStyles();

        int fr = Time.frameCount;
        if (e.type == EventType.Repaint)
        {
            if (fr - _lastFrame > 1) _open = 0f;
            _lastFrame = fr;
            _open = Mathf.MoveTowards(_open, 1f, Time.unscaledDeltaTime / 0.18f);
        }

        if (!UiState.MenuVisible && UiDragState.AnyDragging)
        {
            UiDragState.AnyDragging = false;
            UiState.MenuDragging = false;
        }

        if (_anim.Count > 200) _anim.Clear();

        float eo = EaseOut(_open);
        Rect panelRect = GetPanelRect();
        HandleDrag(panelRect);
        panelRect = GetPanelRect();

        float s = Scale();
        float k = Mathf.Lerp(0.95f, 1f, eo);
        _alpha = eo;
        _inputOk = true;

        Matrix4x4 oldMatrix = GUI.matrix;
        Vector2 c = panelRect.center;
        GUI.matrix = oldMatrix
            * Matrix4x4.TRS(new Vector3(c.x, c.y, 0f), Quaternion.identity, new Vector3(s * k, s * k, 1f))
            * Matrix4x4.TRS(new Vector3(-BaseWidth / 2f, -BaseHeight / 2f, 0f), Quaternion.identity, Vector3.one);

        Rect panel = new Rect(0f, 0f, BaseWidth, BaseHeight);

        for (int i = 0; i < 4; i++)
        {
            float g = 4f + i * 4f;
            DrawRect(new Rect(panel.x - g, panel.y - g + 4f, panel.width + g * 2f, panel.height + g * 2f),
                     new Color(0f, 0f, 0f, 0.10f - i * 0.02f), Radius + g);
        }
        DrawRect(panel, ColPanelBg, Radius);
        DrawOutline(panel, ColBlockBorder, Radius);

        Text(new Rect(16f, 0f, 220f, HeaderH), HeaderTitle, _sTitle, Color.white);
        DrawRect(new Rect(Pad, HeaderH, BaseWidth - Pad * 2f, 1f), ColLine);

        float tabX = 8f;
        float tabY = HeaderH + 10f;

        Text(new Rect(tabX + 8f, tabY, TabWidth, 18f), "Main", _sLeft, ColTextDim);
        tabY += 22f;
        if (SectionButton(new Rect(tabX, tabY, TabWidth, 32f), "Visuals", 0)) SetSection(0);
        tabY += 36f;
        if (SectionButton(new Rect(tabX, tabY, TabWidth, 32f), "Legit", 1)) SetSection(1);
        tabY += 36f;

        tabY += 8f;
        Text(new Rect(tabX + 8f, tabY, TabWidth, 18f), "Screen", _sLeft, ColTextDim);
        tabY += 22f;
        if (SectionButton(new Rect(tabX, tabY, TabWidth, 32f), "Hud", 2)) SetSection(2);

        DrawRect(new Rect(SidebarDividerX, HeaderH + Pad, 1f, BaseHeight - HeaderH - Pad * 2f), ColLine);

        float cy = HeaderH + Pad;
        Rect view = new Rect(ContentX, cy, BaseWidth - ContentX - Pad, BaseHeight - cy - Pad);
        DrawScrollable(view, Mathf.Clamp(UiState.MenuSection, 0, SectionCount - 1));

        GUI.matrix = oldMatrix;
        _alpha = 1f;
        _inputOk = true;

        if (panelRect.Contains(e.mousePosition) &&
            (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel))
            e.Use();
    }

    private static void SetSection(int i)
    {
        UiState.MenuSection = i;
        UiState.MenuScroll = 0f;
    }

    private static void HandleDrag(Rect panelRect)
    {
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Rect handle = new Rect(panelRect.x, panelRect.y, panelRect.width, HeaderH * Scale());
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
            Vector2 p = e.mousePosition - UiState.MenuDragOffset;
            p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - panelRect.width));
            p.y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - panelRect.height));
            UiState.MenuPosition = p;
            e.Use();
        }

        if (UiState.MenuDragging && e.rawType == EventType.MouseUp)
        {
            UiState.MenuDragging = false;
            UiDragState.AnyDragging = false;
            if (e.type == EventType.MouseUp) e.Use();
        }
    }

    private static bool SectionButton(Rect rect, string label, int index)
    {
        bool active = UiState.MenuSection == index;
        float h = Anim(index * 977 + 13, Hover(rect) ? 1f : 0f, 10f);

        if (active)
        {
            DrawRect(rect, ColTabActive, Radius);
            DrawRect(new Rect(rect.x + 4f, rect.y + 8f, 3f, rect.height - 16f), ColAccent, 1.5f);
        }
        else if (h > 0.01f)
            DrawRect(rect, new Color(1f, 1f, 1f, 0.05f * h), Radius);

        Text(new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height), label, _sLeft,
             active ? Color.white : Color.Lerp(ColText, Color.white, h));

        return Click(rect);
    }

    private static void DrawScrollable(Rect view, int section)
    {
        Event e = Event.current;
        float contentH = _contentH[section];
        float maxScroll = Mathf.Max(0f, contentH - view.height);
        bool hasBar = maxScroll > 0.5f;
        bool mouseIn = view.Contains(e.mousePosition);

        if (e.type == EventType.ScrollWheel && mouseIn)
        {
            UiState.MenuScroll += e.delta.y * 24f;
            e.Use();
        }

        int sid = GUIUtility.GetControlID(FocusType.Passive);
        Rect track = new Rect(view.xMax - 6f, view.y, 6f, view.height);
        float thumbH = hasBar ? Mathf.Max(24f, view.height * view.height / contentH) : 0f;
        float travel = Mathf.Max(1f, view.height - thumbH);
        float cur = Mathf.Clamp(UiState.MenuScroll, 0f, maxScroll);
        float thumbY = view.y + travel * (hasBar ? cur / maxScroll : 0f);
        Rect hit = new Rect(track.x - 5f, view.y, 16f, view.height);

        if (hasBar)
        {
            EventType type = e.type;
            bool grabbing = false;
            if (type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = sid;
                Rect thumbRect = new Rect(track.x, thumbY, track.width, thumbH);
                _sbGrab = thumbRect.Contains(e.mousePosition) ? e.mousePosition.y - thumbY : thumbH * 0.5f;
                grabbing = true;
                e.Use();
            }
            else if (type == EventType.MouseDrag && GUIUtility.hotControl == sid)
            {
                grabbing = true;
                e.Use();
            }
            if (grabbing)
                UiState.MenuScroll = Mathf.Clamp01((e.mousePosition.y - _sbGrab - view.y) / travel) * maxScroll;
            if (GUIUtility.hotControl == sid && e.rawType == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        UiState.MenuScroll = Mathf.Clamp(UiState.MenuScroll, 0f, maxScroll);
        float scroll = UiState.MenuScroll;

        bool prevInput = _inputOk;
        _inputOk = prevInput && (mouseIn || GUIUtility.hotControl != 0);
        GUI.BeginGroup(view);

        float bw = view.width - (hasBar ? 10f : 0f);
        _ix = 16f;
        _iw = bw - 32f;
        _y = -scroll;
        float top = _y;

        Rect block = new Rect(0f, top, bw, Mathf.Max(contentH, view.height));
        DrawRect(block, ColBlockBg, Radius);
        DrawOutline(block, ColBlockBorder, Radius);

        switch (section)
        {
            case 0: DrawEspBlock(); break;
            case 1: DrawAimBlock(); break;
            default: DrawHudBlock(); break;
        }

        _contentH[section] = _y - top + 8f;
        GUI.EndGroup();
        _inputOk = prevInput;

        if (hasBar)
        {
            cur = Mathf.Clamp(UiState.MenuScroll, 0f, maxScroll);
            thumbY = view.y + travel * (cur / maxScroll);
            bool active = GUIUtility.hotControl == sid || hit.Contains(e.mousePosition);
            DrawRect(track, new Color(1f, 1f, 1f, 0.06f), 3f);
            DrawRect(new Rect(track.x, thumbY, track.width, thumbH),
                     new Color(ColDivider.r, ColDivider.g, ColDivider.b, active ? 0.65f : 0.35f), 3f);
        }

        // Draw dropdown overlay AFTER everything else, so it's on top
        DrawDropdownOverlay();
    }

    private static void DrawDropdownOverlay()
    {
        if (_openDropdown == -1 || _dropdownOptions == null) return;

        Event e = Event.current;
        Rect listRect = new Rect(_dropdownRect.x, _dropdownRect.y, _dropdownRect.width, _dropdownOptions.Length * 22f + 4f);
        DrawRect(listRect, ColBlockBg, 6f);
        DrawOutline(listRect, ColBlockBorder, 6f);

        for (int i = 0; i < _dropdownOptions.Length; i++)
        {
            Rect item = new Rect(listRect.x + 2f, listRect.y + 2f + i * 22f, listRect.width - 4f, 22f);
            bool hover = Hover(item);
            if (hover) DrawRect(item, new Color(1f, 1f, 1f, 0.06f), 4f);
            Text(item, _dropdownOptions[i], _sLeft, i == _dropdownSelected ? ColAccent : ColText);
            if (Click(item))
            {
                _dropdownCallback?.Invoke(i);
                _openDropdown = -1;
            }
        }

        if (e != null && e.type == EventType.MouseDown && !listRect.Contains(e.mousePosition) && !_dropdownRect.Contains(e.mousePosition))
            _openDropdown = -1;
    }

    private static void DrawEspBlock()
    {
        BlockTitle("ESP");
        ToggleRow("Enabled", ref EspModule.BoxesEnabled);
        ToggleRow("Show Spec", ref TeamFilter.ShowSpectator);
        ToggleRow("Show T", ref TeamFilter.ShowTr);
        ToggleRow("Show CT", ref TeamFilter.ShowCt);

        Space(8f);
        BlockTitle("World");
        // Пока пусто
    }

    private static void DrawAimBlock()
    {
        BlockTitle("AIM");
        ToggleRow("Enabled", ref AimbotModule.Enabled);
        ToggleRow("FOV Circle", ref AimbotModule.DrawFovCircle);
        ToggleRow("Aim on Teammates", ref AimbotModule.AimOnTeammates);
        Space(6f);

        float fov = AimbotModule.Fov;
        bool fovDone = Slider("FOV", ref fov, 1f, 180f, "F0", "°", 1f);
        if (!Mathf.Approximately(fov, AimbotModule.Fov)) AimbotModule.Fov = fov;
        if (fovDone) Plugin.CfgAimbotFov.Value = AimbotModule.Fov;

        float sens = AimbotModule.Sensitivity;
        bool sensDone = Slider("Sensitivity", ref sens, 0.01f, 3f, "F2", "", 0.01f);
        if (!Mathf.Approximately(sens, AimbotModule.Sensitivity)) AimbotModule.Sensitivity = sens;
        if (sensDone) Plugin.CfgAimbotSensitivity.Value = AimbotModule.Sensitivity;

        Space(4f);

        int boneIdx = AimbotModule.Bone == AimBone.Neck ? 0
                    : AimbotModule.Bone == AimBone.Head ? 1
                    : AimbotModule.Bone == AimBone.Chest ? 2 : -1;
        Dropdown("Bone", BoneNames, boneIdx, (i) =>
        {
            AimBone b = i == 0 ? AimBone.Neck : i == 1 ? AimBone.Head : AimBone.Chest;
            AimbotModule.Bone = b;
            Plugin.CfgAimbotBone.Value = b.ToString();
        });

        int modeIdx = KeybindState.ToggleMode ? 1 : 0;
        Dropdown("Mode", ModeNames, modeIdx, (i) =>
        {
            KeybindState.ToggleMode = i == 1;
            Plugin.CfgAimbotToggleMode.Value = KeybindState.ToggleMode;
        });

        Rect r = Next(30f);
        Text(new Rect(r.x + 8f, r.y, r.width / 2f, r.height), "Bind", _sLeft, ColText);
        Rect b2 = new Rect(r.xMax - 8f - 150f, r.y + 3f, 150f, 24f);
        bool cap = KeybindState.Capturing;
        string keyName = cap ? "Press key... (Esc)" : KeybindState.AimbotKey.ToString();
        if (Button(b2, keyName, cap))
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

    private static void DrawHudBlock()
    {
        BlockTitle("Hud");
        ToggleRow("Watermark", ref UiState.HudWatermark);
        ToggleRow("Show Bind List", ref BindListState.Enabled);
        ToggleRow("Show Bomb Status", ref BombStatusRenderer.Enabled);
        Space(6f);

        float cur = Mathf.Clamp(UiState.MenuScale, MinScale, MaxScale);
        Dropdown("Menu size", new[] { "25%", "50%", "75%", "100%", "125%", "150%", "200%" },
            Mathf.Clamp(Mathf.RoundToInt((cur - 0.25f) / 0.25f), 0, 6),
            (i) =>
            {
                float[] vals = { 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
                UiState.MenuScale = vals[i];
                Plugin.CfgMenuScale.Value = UiState.MenuScale;
            });
    }

    private static Rect Next(float h, float gap = 0f)
    {
        var r = new Rect(_ix, _y, _iw, h);
        _y += h + gap;
        return r;
    }

    private static void Space(float h) => _y += h;

    private static void BlockTitle(string title)
    {
        Text(new Rect(_ix, _y + 6f, _iw, 20f), title, _sBold, ColText);
        DrawRect(new Rect(_ix, _y + 31f, _iw, 1f), ColLine);
        _y += 38f;
    }

    private static void ToggleRow(string label, ref bool value)
    {
        Rect r = Next(30f);
        float h = Anim(Key(label, 1), Hover(r) ? 1f : 0f, 10f);
        float a = Anim(Key(label, 2), value ? 1f : 0f, 8f);

        if (h > 0.01f) DrawRect(r, new Color(1f, 1f, 1f, 0.04f * h), 6f);
        Text(new Rect(r.x + 8f, r.y, r.width - 60f, r.height), label, _sLeft, Color.Lerp(ColText, Color.white, h));

        Rect tg = new Rect(r.xMax - 8f - 34f, r.y + (r.height - 18f) / 2f, 34f, 18f);
        DrawRect(tg, Color.Lerp(ColToggleTrack, ColAccent, a), 9f);
        float ks = 14f;
        float kx = Mathf.Lerp(tg.x + 2f, tg.xMax - 2f - ks, Smooth01(a));
        DrawRect(new Rect(kx, tg.y + 2f, ks, ks), ColToggleKnob, 7f);

        if (Click(r)) value = !value;
    }

    private static bool Slider(string label, ref float v, float min, float max, string fmt, string suffix, float step)
    {
        Rect r = Next(40f);
        Text(new Rect(r.x + 8f, r.y, r.width * 0.6f, 20f), label, _sLeft, ColText);
        Text(new Rect(r.x, r.y, r.width - 8f, 20f), v.ToString(fmt) + suffix, _sRight, ColTextDim);

        Rect track = new Rect(r.x + 8f, r.y + 28f, r.width - 16f, 6f);
        Rect hit = new Rect(track.x - 6f, r.y + 20f, track.width + 12f, 20f);

        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        bool commit = false;

        if (e.type == EventType.MouseDown && e.button == 0 && _inputOk && hit.Contains(e.mousePosition))
        {
            GUIUtility.hotControl = id;
            SetFromMouse(ref v, track, min, max, step);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
        {
            SetFromMouse(ref v, track, min, max, step);
            e.Use();
        }
        else if (e.rawType == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            commit = true;
            if (e.type == EventType.MouseUp) e.Use();
        }

        float t = Mathf.InverseLerp(min, max, v);
        DrawRect(track, ColToggleTrack, 3f);
        if (t > 0.001f) DrawRect(new Rect(track.x, track.y, track.width * t, track.height), ColAccent, 3f);
        bool active = GUIUtility.hotControl == id || Hover(hit);
        float ks = active ? 14f : 12f;
        DrawRect(new Rect(track.x + track.width * t - ks / 2f, track.y + 3f - ks / 2f, ks, ks), ColToggleKnob, ks / 2f);

        return commit;
    }

    private static void SetFromMouse(ref float v, Rect track, float min, float max, float step)
    {
        float t = Mathf.Clamp01((Event.current.mousePosition.x - track.x) / track.width);
        float val = Mathf.Lerp(min, max, t);
        if (step > 0f) val = Mathf.Round(val / step) * step;
        v = Mathf.Clamp(val, min, max);
    }

    // Dropdown widget
    private static void Dropdown(string label, string[] options, int selected, Action<int> onSelect)
    {
        Rect r = Next(30f);
        Text(new Rect(r.x + 8f, r.y, r.width / 2f, r.height), label, _sLeft, ColText);

        Rect btn = new Rect(r.xMax - 8f - 150f, r.y + 3f, 150f, 24f);
        int id = label.GetHashCode();
        bool open = _openDropdown == id;

        if (Button(btn, options[selected] + (open ? " ▲" : " ▼"), open))
        {
            if (open) _openDropdown = -1;
            else
            {
                _openDropdown = id;
                _dropdownRect = btn;
                _dropdownOptions = options;
                _dropdownSelected = selected;
                _dropdownCallback = onSelect;
            }
        }
    }

    private static bool Button(Rect r, string text, bool active)
    {
        bool hov = Hover(r);
        Color fill = active ? new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.55f)
                            : (hov ? ColButtonHover : ColBlockBorder);
        DrawRect(r, fill, 6f);
        Text(r, text, _sCenter, active ? Color.white : Color.Lerp(ColText, Color.white, hov ? 1f : 0f));
        return Click(r);
    }
}