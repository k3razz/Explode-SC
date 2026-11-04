using UnityEngine;

namespace MyDynamicESP;

public class OverlayRenderer : MonoBehaviour
{
    private Texture2D _whiteTex;

    void Awake()
    {
        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
    }

    void OnDestroy()
    {
        try { if (_whiteTex != null) Destroy(_whiteTex); } catch { }
    }

    void OnPostRender()
    {
        if (_whiteTex == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // Рисуем боксы
        if (EspModule.BoxesEnabled)
        {
            foreach (var t in EspModule.ToDraw)
            {
                try
                {
                    if (t.Transform == null) continue;
                    var go = t.Transform.gameObject;
                    if (go == null || !go.activeInHierarchy) continue;

                    Vector3 hw = t.Transform.position + Vector3.up * EspModule.HeadOffset;
                    Vector3 fw = t.Transform.position - Vector3.up * EspModule.FootOffset;
                    Vector3 sh = _cam.WorldToScreenPoint(hw);
                    Vector3 sf = _cam.WorldToScreenPoint(fw);
                    if (sh.z < 0 || sf.z < 0) continue;

                    float x = sh.x;
                    float top = Screen.height - sh.y;
                    float bottom = Screen.height - sf.y;
                    float height = Mathf.Abs(bottom - top);
                    float width = height * EspModule.BoxWidthRatio;
                    if (width < 4f) width = 4f;
                    if (height < 8f) height = 8f;
                    float left = x - width / 2f;

                    // Заливка
                    Color fill = t.Team == 1 ? new Color(1f, 0f, 0f, 0.12f) : new Color(0f, 0.5f, 1f, 0.12f);
                    Graphics.DrawTexture(new Rect(left, top, width, height), _whiteTex, fill);

                    // Обводка
                    float th = 2f;
                    Color outl = t.Team == 1 ? Color.red : Color.cyan;
                    Graphics.DrawTexture(new Rect(left, top, width, th), _whiteTex, outl);
                    Graphics.DrawTexture(new Rect(left, top + height - th, width, th), _whiteTex, outl);
                    Graphics.DrawTexture(new Rect(left, top, th, height), _whiteTex, outl);
                    Graphics.DrawTexture(new Rect(left + width - th, top, th, height), _whiteTex, outl);
                }
                catch { }
            }
        }

        // FOV-круг
        if (AimbotModule.Enabled && AimbotModule.DrawFovCircle)
        {
            MenuRenderer.DrawFovCircleGPU();
        }

        // Ватермарка (фон)
        if (WatermarkModule.Enabled)
        {
            Watermark.DrawBackgroundGPU(_whiteTex);
        }
    }

    private Camera _cam;
}