using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

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

    private static float _lastFetch = 0f;
    private static bool _isFetching = false;
    private static readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private static GlobalSystemMediaTransportControlsSessionManager _manager = null;

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
            if (_manager == null)
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            var session = _manager.GetCurrentSession();
            if (session == null)
            {
                EnqueueMainThread(() =>
                {
                    TrackTitle = "";
                    Artist = "";
                    Progress = 0f;
                    Duration = 0f;
                    IsPlaying = false;
                    PlayerName = "";
                    if (AlbumArt != null) { UnityEngine.Object.Destroy(AlbumArt); AlbumArt = null; }
                });
                return;
            }

            var props = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            var playbackInfo = session.GetPlaybackInfo();

            string title = props.Title ?? "";
            string artist = props.Artist ?? "";
            float progress = (float)timeline.Position.TotalSeconds;
            float duration = (float)timeline.EndTime.TotalSeconds;
            bool playing = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            string player = session.SourceAppUserModelId ?? "";

            Texture2D art = null;
            if (props.Thumbnail != null)
            {
                try
                {
                    using (var stream = await props.Thumbnail.OpenReadAsync())
                    {
                        byte[] buffer = new byte[stream.Size];
                        using (var reader = new DataReader(stream))
                        {
                            await reader.LoadAsync((uint)stream.Size);
                            reader.ReadBytes(buffer);
                        }
                        art = new Texture2D(2, 2);
                        art.LoadImage(buffer);
                    }
                }
                catch { }
            }

            EnqueueMainThread(() =>
            {
                TrackTitle = title;
                Artist = artist;
                Progress = progress;
                Duration = duration;
                IsPlaying = playing;
                PlayerName = player;
                if (art != null)
                {
                    if (AlbumArt != null) UnityEngine.Object.Destroy(AlbumArt);
                    AlbumArt = art;
                }
            });
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
