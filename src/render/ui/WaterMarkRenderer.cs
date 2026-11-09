using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyDynamicESP;

public static class Watermark
{
    // ── Что показывать (можно привязать к тогглам в меню Hud) ──
    public static bool ShowFps = true;
    public static bool ShowTime = true;
    public static bool ShowDate = true;
    public static bool ShowSession = true;
    public static bool ShowResolution = false;
    public static bool ShowExtraText = false;   // текст из WatermarkModule.GetText()

    // ── Размеры ──
    private const float Height = 40f;
    private const float Radius = 10f;
    private const float PadL = 6f;
    private const float PadR = 16f;
    private const float LogoSize = 28f;
    private const float Gap = 12f;      // отступ до/после разделителя

    // ── Цвета ──
    private static readonly Color ColBg = new Color32(0x0F, 0x14, 0x24, 240);
    private static readonly Color ColBorder = new Color32(0x2A, 0x33, 0x52, 255);
    private static readonly Color ColAccent = new Color32(0x5B, 0x8C, 0xFF, 255); // СИНИЙ (вместо оранжевого)
    private static readonly Color ColLabel = new Color32(0x7C, 0x86, 0xA2, 255);
    private static readonly Color ColValue = new Color32(0xE6, 0xE9, 0xF2, 255);
    private static readonly Color ColLine = new Color(0.63f, 0.70f, 0.87f, 0.14f);
    private static readonly Color ColGood = new Color32(0x5B, 0xE0, 0x8A, 255);
    private static readonly Color ColMid = new Color32(0xF2, 0xC9, 0x4C, 255);
    private static readonly Color ColBad = new Color32(0xEB, 0x57, 0x57, 255);

    private class Seg
    {
        public string Label, Value;
        public Color ValueColor;
        public float W;
    }

    private static readonly List<Seg> _segs = new();
    private static GUIStyle _sLabel, _sValue, _sLogo;
    private static float _fps = 60f;
    private static float _lastBuild = -10f;
    private static float _totalW = 100f;
    private static readonly GUIContent _gc = new GUIContent();

    private static void EnsureStyles()
    {
        if (_sLabel != null) return;
        _sLabel = Make(TextAnchor.MiddleLeft, 10, FontStyle.Normal);
        _sValue = Make(TextAnchor.MiddleLeft, 13, FontStyle.Bold);
        _sLogo = Make(TextAnchor.MiddleCenter, 18, FontStyle.Bold);
    }

    private static GUIStyle Make(TextAnchor a, int size, FontStyle fs)
    {
        var s = new GUIStyle
        {
            alignment = a,
            fontSize = size,
            fontStyle = fs,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        s.normal.textColor = Color.white;
        return s;
    }

    private static float Measure(GUIStyle st, string text)
    {
        _gc.text = text;
        return st.CalcSize(_gc).x;
    }

    private static Color FpsColor(float f) => f >= 100f ? ColGood : f >= 60f ? ColMid : ColBad;

    private static void Add(string label, string value, string template, Color col)
    {
        float w = Mathf.Max(Measure(_sLabel, label), Measure(_sValue, template)) + 2f;
        _segs.Add(new Seg { Label = label, Value = value, ValueColor = col, W = w });
    }

    private static void Refresh()
    {
        EnsureStyles();

        Event e = Event.current;
        if (e != null && e.type == EventType.Repaint && Time.unscaledDeltaTime > 0f)
            _fps = Mathf.Lerp(_fps, 1f / Time.unscaledDeltaTime, 0.05f);

        if (_segs.Count > 0 && Time.unscaledTime - _lastBuild < 0.25f) return;
        _lastBuild = Time.unscaledTime;

        _segs.Clear();
        DateTime now = DateTime.Now;

        if (ShowFps) Add("FPS", Mathf.RoundToInt(_fps).ToString(), "000", FpsColor(_fps));
        if (ShowTime) Add("TIME", now.ToString("HH:mm:ss"), "00:00:00", ColValue);
        if (ShowDate) Add("DATE", now.ToString("dd.MM.yyyy"), "00.00.0000", ColValue);
        if (ShowSession)
        {
            TimeSpan ts = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
            string v = ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
            Add("SESSION", v, "0:00:00", ColValue);
        }
        if (ShowResolution) Add("RES", Screen.width + "x" + Screen.height, "0000x0000", ColValue);
        if (ShowExtraText)
        {
            string extra = WatermarkModule.GetText();
            if (!string.IsNullOrEmpty(extra)) Add("INFO", extra, extra, ColValue);
        }

        float w = PadL + LogoSize + PadR;
        for (int i = 0; i < _segs.Count; i++)
        {
            w += Gap + _segs[i].W;
            if (i < _segs.Count - 1) w += Gap + 1f;
        }
        _totalW = w;
    }

    public static Rect GetRect()
    {
        Refresh();
        Vector2 p = WatermarkModule.Position;
        float x = Mathf.Clamp(p.x - _totalW / 2f, 0f, Mathf.Max(0f, Screen.width - _totalW));
        float y = Mathf.Clamp(p.y - Height / 2f, 0f, Mathf.Max(0f, Screen.height - Height));
        return new Rect(x, y, _totalW, Height);
    }

    public static void DrawBackground(Texture2D whiteTex)
    {
        if (!WatermarkModule.Enabled) return;
        if (Event.current.type != EventType.Repaint) return;

        Rect r = GetRect();

        MenuRenderer.DrawRect(new Rect(r.x - 2f, r.y, r.width + 4f, r.height + 4f), new Color(0f, 0f, 0f, 0.22f), Radius + 2f);
        MenuRenderer.DrawRect(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), ColBorder, Radius + 1f);
        MenuRenderer.DrawRect(r, ColBg, Radius);

        // логотип (теперь СИНИЙ)
        Rect logo = new Rect(r.x + PadL, r.y + (r.height - LogoSize) / 2f, LogoSize, LogoSize);
        MenuRenderer.DrawRect(logo, ColAccent, 7f);

        // разделители
        float x = r.x + PadL + LogoSize + Gap;
        for (int i = 0; i < _segs.Count; i++)
        {
            x += _segs[i].W;
            if (i < _segs.Count - 1)
            {
                x += Gap;
                MenuRenderer.DrawRect(new Rect(x, r.y + 10f, 1f, r.height - 20f), ColLine);
                x += 1f + Gap;
            }
        }

        // нижняя линия (теперь СИНЯЯ)
        MenuRenderer.DrawRect(new Rect(r.x + Radius, r.yMax - 2f, r.width - Radius * 2f, 1.5f),
            new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.55f));
    }

    public static void DrawText()
    {
        if (!WatermarkModule.Enabled) return;
        if (Event.current.type != EventType.Repaint) return;

        Rect r = GetRect();

        GUI.color = new Color(0.05f, 0.06f, 0.1f, 1f);
        GUI.Label(new Rect(r.x + PadL, r.y + (r.height - LogoSize) / 2f, LogoSize, LogoSize), "E", _sLogo);

        float x = r.x + PadL + LogoSize + Gap;
        for (int i = 0; i < _segs.Count; i++)
        {
            Seg s = _segs[i];
            GUI.color = ColLabel;
            GUI.Label(new Rect(x, r.y + 5f, s.W + 4f, 14f), s.Label, _sLabel);
            GUI.color = s.ValueColor;
            GUI.Label(new Rect(x, r.y + 19f, s.W + 4f, 16f), s.Value, _sValue);

            x += s.W;
            if (i < _segs.Count - 1) x += Gap + 1f + Gap;
        }
        GUI.color = Color.white;
    }

    public static void HandleDrag()
    {
        if (!WatermarkModule.Enabled) return;
        Event e = Event.current;
        if (e == null) return;

        Rect rect = GetRect();
        Vector2 mouse = e.mousePosition;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (rect.Contains(mouse) && !UiDragState.AnyDragging)
            {
                WatermarkModule.IsDragging = true;
                UiDragState.AnyDragging = true;
                WatermarkModule.DragOffset = mouse - new Vector2(rect.x, rect.y);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && WatermarkModule.IsDragging)
        {
            Vector2 tl = mouse - WatermarkModule.DragOffset;
            tl.x = Mathf.Clamp(tl.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            tl.y = Mathf.Clamp(tl.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            WatermarkModule.Position = new Vector2(tl.x + rect.width / 2f, tl.y + rect.height / 2f);
            e.Use();
        }

        if (WatermarkModule.IsDragging && e.rawType == EventType.MouseUp)
        {
            WatermarkModule.IsDragging = false;
            UiDragState.AnyDragging = false;
            if (Plugin.CfgWatermarkX != null) Plugin.CfgWatermarkX.Value = WatermarkModule.Position.x;
            if (Plugin.CfgWatermarkY != null) Plugin.CfgWatermarkY.Value = WatermarkModule.Position.y;
            if (e.type == EventType.MouseUp) e.Use();
        }
    }
}