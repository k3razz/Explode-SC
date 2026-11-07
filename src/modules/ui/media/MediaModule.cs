using UnityEngine;
using System;
using System.Threading;
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
    public static string PlayerName = "";

    private static float _lastFetch = 0f;
    private static bool _isFetching = false;
    private static readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
    private static object _manager = null;
    private static bool _initFailed = false;
    private static bool _initStarted = false;
    private static int _tick = 0;

    private static void EnsureWinRT()
    {
        if (_manager != null || _initFailed || _initStarted) return;
        _initStarted = true;

        Plugin.LogSource?.LogInfo("[Media] Starting WinRT init on background thread...");

        Task.Run(async () =>
        {
            try
            {
                // Даём Unity время, чтобы COM был инициализирован
                await Task.Delay(500);

                var t = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Microsoft.Windows.SDK.NET");
                if (t == null)
                {
                    Plugin.LogSource?.LogWarning("[Media] Type not found: Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager");
                    _initFailed = true;
                    return;
                }

                Plugin.LogSource?.LogInfo($"[Media] Type found: {t.FullName}");

                var reqMethod = t.GetMethod("RequestAsync");
                if (reqMethod == null)
                {
                    Plugin.LogSource?.LogError("[Media] RequestAsync method not found");
                    _initFailed = true;
                    return;
                }

                Plugin.LogSource?.LogInfo("[Media] Calling RequestAsync...");
                var task = reqMethod.Invoke(null, null) as Task;
                if (task == null)
                {
                    Plugin.LogSource?.LogError("[Media] RequestAsync returned null task");
                    _initFailed = true;
                    return;
                }

                await task;
                Plugin.LogSource?.LogInfo($"[Media] RequestAsync completed, status={task.Status}");

                var resultProp = task.GetType().GetProperty("Result");
                var result = resultProp?.GetValue(task);
                if (result == null)
                {
                    Plugin.LogSource?.LogError("[Media] RequestAsync result is null");
                    _initFailed = true;
                    return;
                }

                _manager = result;
                Plugin.LogSource?.LogInfo($"[Media] Manager initialized: {_manager.GetType().FullName}");
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError($"[Media] EnsureWinRT EX: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                _initFailed = true;
            }
        });
    }

    public static void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
                _mainThreadQueue.Dequeue()?.Invoke();
        }

        if (!Enabled) return;
        if (_initFailed)
        {
            _tick++;
            if (_tick % 300 == 0) // раз в 5 секунд
                Plugin.LogSource?.LogWarning("[Media] Init failed, media disabled");
            return;
        }

        EnsureWinRT();

        _tick++;
        if (_tick % 120 == 0)
            Plugin.LogSource?.LogInfo($"[Media] Tick: enabled={Enabled}, manager={_manager != null}, initStarted={_initStarted}, title='{TrackTitle}'");

        if (Time.time - _lastFetch >= 2f && !_isFetching && _manager != null)
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
            if (_manager == null) return;

            object session = null;

            var getSession = _manager.GetType().GetMethod("GetCurrentSession");
            session = getSession?.Invoke(_manager, null);

            if (session == null)
            {
                var getSessions = _manager.GetType().GetMethod("GetSessions");
                var sessionsObj = getSessions?.Invoke(_manager, null);
                if (sessionsObj is System.Collections.IEnumerable enumerable)
                {
                    foreach (var s in enumerable)
                    {
                        session = s;
                        break;
                    }
                }
            }

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

            var tryGetProps = session.GetType().GetMethod("TryGetMediaPropertiesAsync");
            var propsTask = tryGetProps?.Invoke(session, null) as Task;
            if (propsTask == null) return;
            await propsTask;

            var props = propsTask.GetType().GetProperty("Result")?.GetValue(propsTask);
            if (props == null) return;

            string title = props.GetType().GetProperty("Title")?.GetValue(props) as string ?? "";
            string artist = props.GetType().GetProperty("Artist")?.GetValue(props) as string ?? "";
            var sourceProp = session.GetType().GetProperty("SourceAppUserModelId");
            string appId = sourceProp?.GetValue(session) as string ?? "";

            var getTimeline = session.GetType().GetMethod("GetTimelineProperties");
            var timeline = getTimeline?.Invoke(session, null);
            float progress = 0f, duration = 0f;
            if (timeline != null)
            {
                var pos = timeline.GetType().GetProperty("Position")?.GetValue(timeline);
                var end = timeline.GetType().GetProperty("EndTime")?.GetValue(timeline);
                if (pos != null) progress = (float)((TimeSpan)pos).TotalSeconds;
                if (end != null) duration = (float)((TimeSpan)end).TotalSeconds;
            }

            var getPlayback = session.GetType().GetMethod("GetPlaybackInfo");
            var playbackInfo = getPlayback?.Invoke(session, null);
            bool playing = false;
            if (playbackInfo != null)
            {
                var status = playbackInfo.GetType().GetProperty("PlaybackStatus")?.GetValue(playbackInfo);
                playing = status?.ToString() == "Playing";
            }

            byte[] artBytes = null;
            var thumbProp = props.GetType().GetProperty("Thumbnail");
            var thumb = thumbProp?.GetValue(props);
            if (thumb != null)
            {
                try
                {
                    var openRead = thumb.GetType().GetMethod("OpenReadAsync");
                    var streamTask = openRead?.Invoke(thumb, null) as Task;
                    if (streamTask != null)
                    {
                        await streamTask;
                        var stream = streamTask.GetType().GetProperty("Result")?.GetValue(streamTask);
                        if (stream != null)
                        {
                            var sizeProp = stream.GetType().GetProperty("Size");
                            uint size = sizeProp != null ? (uint)sizeProp.GetValue(stream) : 0;
                            var readMethod = stream.GetType().GetMethod("ReadAsync", new[] { typeof(byte[]), typeof(uint), typeof(uint) });
                            if (readMethod != null && size > 0)
                            {
                                byte[] buffer = new byte[size];
                                var readTask = readMethod.Invoke(stream, new object[] { buffer, (uint)0, size }) as Task;
                                if (readTask != null) await readTask;
                                artBytes = buffer;
                            }
                        }
                    }
                }
                catch { }
            }

            bool changed = title != TrackTitle;
            EnqueueMainThread(() =>
            {
                TrackTitle = title;
                Artist = artist;
                Progress = progress;
                Duration = duration;
                IsPlaying = playing;
                PlayerName = appId;

                if (artBytes != null && artBytes.Length > 0)
                {
                    try
                    {
                        if (AlbumArt != null) UnityEngine.Object.Destroy(AlbumArt);
                        AlbumArt = new Texture2D(2, 2);
                        UnityEngine.ImageConversion.LoadImage(AlbumArt, artBytes);
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogSource?.LogError($"[Media] Texture EX: {ex.Message}");
                    }
                }

                if (changed)
                    Plugin.LogSource?.LogInfo($"[Media] Now playing: '{title}' by '{artist}' ({appId})");
            });
        }
        catch (Exception ex)
        {
            Plugin.LogSource?.LogError($"[Media] Fetch error: {ex.GetType().Name}: {ex.Message}");
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
