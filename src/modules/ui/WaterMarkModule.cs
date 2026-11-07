using UnityEngine;

namespace MyDynamicESP;

public static class WatermarkModule
{
    public static bool Enabled = true;
    public static Vector2 Position = new Vector2(-1f, -1f);
    public static bool IsDragging = false;
    public static Vector2 DragOffset;

    private static float _lastFpsUpdate = 0f;
    private static int _fps = 0;
    private static int _ping = -1;

    public static void Update()
    {
        // Считаем FPS каждые 0.5 секунды
        if (Time.time - _lastFpsUpdate >= 0.5f)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.RoundToInt(1f / dt);
            _lastFpsUpdate = Time.time;
            UpdatePing();
        }

        if (Position.x < 0 || Position.y < 0)
            Position = new Vector2(Screen.width / 2f, Screen.height - 40f);
    }

    private static void UpdatePing()
    {
        try
        {
            System.Type pingType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name.Contains("Ping") || t.Name.Contains("NetworkStats"))
                        {
                            pingType = t;
                            break;
                        }
                    }
                }
                catch { }
                if (pingType != null) break;
            }

            if (pingType != null)
            {
                var p = pingType.GetProperty("Ping",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
                if (p != null)
                {
                    _ping = (int)p.GetValue(null);
                    return;
                }
            }
        }
        catch { }
        _ping = -1;
    }

    public static string GetText()
    {
        string fpsStr = $"{_fps} FPS";
        return $"Explode • {fpsStr} • BETA";
    }
}
