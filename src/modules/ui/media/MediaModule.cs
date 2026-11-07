using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

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

    private static float _lastFetch = 0f;
    private static bool _isFetching = false;
    private static readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

    // Вызывается из Update() в MainBehaivour
    public static void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
                _mainThreadQueue.Dequeue()?.Invoke();
        }

        if (!Enabled) return;

        if (Time.time - _lastFetch >= 2f && !_isFetching)
        {
            _lastFetch = Time.time;
            _isFetching = true;
            Task.Run(() => FetchMediaInfo());
        }
    }

    private static async Task FetchMediaInfo()
    {
        try
        {
            // Для работы нужно добавить пакет Microsoft.Windows.SDK.NET (или использовать WinRT)
            // В Unity Il2Cpp используется System.Runtime.InteropServices.WindowsRuntime
            /*
            var manager = await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetCurrentSession();
            if (session != null)
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var timeline = session.GetTimelineProperties();

                // Обновляем данные в главном потоке
                EnqueueMainThread(() =>
                {
                    TrackTitle = props.Title;
                    Artist = props.Artist;
                    Progress = (float)timeline.Position.TotalSeconds;
                    Duration = (float)timeline.EndTime.TotalSeconds;
                    IsPlaying = session.GetPlaybackInfo().PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    // Загрузка обложки
                    if (props.Thumbnail != null)
                    {
                        LoadThumbnail(props.Thumbnail);
                    }
                });
            }
            */
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogError($"[Media] Fetch error: {ex.Message}");
        }
        finally
        {
            _isFetching = false;
        }
    }

    private static void EnqueueMainThread(Action action)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    public static string GetTimeString(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m}:{s:D2}";
    }
}
