using UnityEngine;

namespace MyDynamicESP;

public static class UiPanel
{
    private static Texture2D _tex;
    private static int _lastW = -1, _lastH = -1;
    private const float Radius = 8f;
    private const int AA = 2;

    public static void EnsureTexture(int w, int h)
    {
        if (_tex != null && Mathf.Abs(_lastW - w) <= 2 && Mathf.Abs(_lastH - h) <= 2) return;
        if (_tex != null) { try { Object.Destroy(_tex); } catch { } _tex = null; }
        _lastW = w; _lastH = h;
        _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Bilinear;
        _tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        float r = Mathf.Min(Radius, Mathf.Min(w, h) * 0.5f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = 1f;
                if (x < r && y < r) { float dx = r - (x + 0.5f), dy = r - (y + 0.5f); float d = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01((r - d) / AA + 0.5f); }
                else if (x >= w - r && y < r) { float dx = (x + 0.5f) - (w - r), dy = r - (y + 0.5f); float d = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01((r - d) / AA + 0.5f); }
                else if (x < r && y >= h - r) { float dx = r - (x + 0.5f), dy = (y + 0.5f) - (h - r); float d = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01((r - d) / AA + 0.5f); }
                else if (x >= w - r && y >= h - r) { float dx = (x + 0.5f) - (w - r), dy = (y + 0.5f) - (h - r); float d = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01((r - d) / AA + 0.5f); }
                px[y * w + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255));
            }
        _tex.SetPixels32(px);
        _tex.Apply();
    }

    public static void Draw(Rect rect, float alpha = 0.85f)
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        EnsureTexture(w, h);
        if (_tex == null) return;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(rect.x, rect.y, w, h), _tex);
        GUI.color = Color.white;
    }
}
