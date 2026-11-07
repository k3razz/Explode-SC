using UnityEngine;

namespace MyDynamicESP;

public static class MediaModule
{
    public static bool Enabled = false;
    public static Vector2 Position = new Vector2(-1f, -1f);
    public static bool IsDragging = false;
    public static Vector2 DragOffset;

    public static string TrackTitle = "";
    public static string Artist = "";
    public static float Progress = 0f;
    public static float Duration = 0f;
    public static Texture2D AlbumArt = null;
    public static bool IsPlaying = false;
    public static string PlayerName = "";

    public static void Update()
    {
        // Заглушка. Медиа-плеер отключен.
    }

    public static string GetTimeString(float seconds)
    {
        return "0:00";
    }
}
