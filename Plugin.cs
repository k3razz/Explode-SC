using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace MyDynamicESP;

public static class TeamFilter
{
    public static bool ShowTr = true;
    public static bool ShowCt = true;
    public static bool ShowSpectator = false;
}

public static class EspManager
{
    public static void EnsureBehaviour()
    {
        try
        {
            var existing = UnityEngine.Object.FindObjectOfType<EspBehaviour>();
            if (existing != null) return;

            var go = new GameObject("ESP_Behaviour");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var type = Il2CppType.From(typeof(EspBehaviour)).TryCast<Il2CppSystem.Type>();
            if (type != null)
            {
                go.AddComponent(type);
                Plugin.LogSource?.LogInfo("[ESP] EnsureBehaviour: created new instance");
            }
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[ESP] EnsureBehaviour EX: {e}");
        }
    }
}

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public static ManualLogSource LogSource;

    public override void Load()
    {
        LogSource = Log;
        Log.LogInfo("[ESP] ===== Plugin.Load() =====");
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<EspBehaviour>();
            SceneManager.sceneLoaded += (Action<Scene, LoadSceneMode>)OnSceneLoaded;
            EspManager.EnsureBehaviour();
        }
        catch (Exception e)
        {
            Log.LogError($"[ESP] Failed to add EspBehaviour: {e}");
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LogSource?.LogInfo($"[ESP] Scene loaded: '{scene.name}' ({mode}) -> recreating behaviour");
        EspManager.EnsureBehaviour();
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public int Team;
        public string Name;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<Tracked> _toDraw = new();
    private readonly List<int> _seenList = new();

    private Camera _cam;
    private Texture2D _whiteTex;
    private Texture2D _circleTex;
    private float _scanTimer;
    private const float ScanInterval = 0.5f;

    private float _watchdogTimer;
    private const float WatchdogInterval = 3.0f;
    private int _watchdogTicks;

    private const float HeadOffset = 1.8f;
    private const float FootOffset = 0.1f;
    private const float BoxWidthRatio = 0.4f;

    private bool _espBoxesEnabled = true;
    private bool _espNamesEnabled = true;
    private bool _espDistanceEnabled = true;
    private bool _menuVisible = false;

    private const float MenuWidth = 340f;
    private const float MenuHeight = 460f;
    private const float MenuOffset = 30f;

    private const float ButtonRadius = 22f;
    private Vector2 _buttonCenter = new Vector2(-1f, -1f);
    private bool _buttonInitialized = false;
    private bool _isDragging = false;
    private bool _dragMoved = false;
    private Vector2 _dragOffset;
    private Vector2 _dragStartMouse;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private static MemberInfo _teamMember; 
    private int _localTeam = -1;

    private readonly Dictionary<string, float> _lastLogTime = new();
    private const float LogCooldown = 1.5f;

    private void LogThrottled(string key, string message, bool isError = false)
    {
        float now = Time.realtimeSinceStartup;
        if (_lastLogTime.TryGetValue(key, out float last) && now - last < LogCooldown) return;
        _lastLogTime[key] = now;
        
        if (isError)
        {
            Debug.LogError(message);
            Plugin.LogSource?.LogError(message);
        }
        else
        {
            Debug.Log(message);
            Plugin.LogSource?.LogInfo(message);
        }
    }

    void Awake()
    {
        try
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
            _circleTex = CreateCircleTexture(64);
            enabled = true;
            gameObject.SetActive(true);
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] Awake EX: {e}"); }
    }

    private Texture2D CreateCircleTexture(int size)
    {
        var tex = new Texture2D(size, size);
        float center = size / 2f;
        float radius = size / 2f - 1f;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                {
                    float alpha = dist <= radius - 1.5f ? 1f : Mathf.Clamp01(radius - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void OnDestroy()
    {
        try
        {
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_circleTex != null) Destroy(_circleTex);
        }
        catch { }
    }

    void RunWatchdog()
    {
        _watchdogTicks++;
        try
        {
            bool cameraOk = _cam != null;
            bool typesOk = _botType != null || _playerType != null;

            if (!typesOk)
            {
                _resolved = false;
                ResolveTypes();
            }
            if (!cameraOk) _cam = Camera.main;
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[ESP] Watchdog EX: {e}");
        }
    }

    int ReadTeam(Component component)
    {
        if (component == null || _teamMember == null) return -1;
        try
        {
            object val = null;
            if (_teamMember is PropertyInfo prop)
                val = prop.GetValue(component);
            else if (_teamMember is MethodInfo method)
                val = method.Invoke(component, null);
            else if (_teamMember is FieldInfo field)
                val = field.GetValue(component);

            if (val != null)
            {
                if (val is Enum en) return Convert.ToInt32(en);
                return Convert.ToInt32(val);
            }
        }
        catch { }
        return -1;
    }

    void Update()
    {
        try
        {
            if (_menuVisible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            _watchdogTimer += Time.deltaTime;
            if (_watchdogTimer >= WatchdogInterval)
            {
                _watchdogTimer = 0f;
                RunWatchdog();
            }

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval)
            {
                _scanTimer = 0f;
                DoScan();
            }

            _toDraw.Clear();
            foreach (var t in _tracked.Values)
            {
                if (t.Transform == null) continue;
                if (!IsTeamVisible(t.Team)) continue;
                _toDraw.Add(t);
            }
        }
        catch (Exception e)
        {
            LogThrottled("update_ex", $"[ESP] Update EX: {e}", true);
            _scanTimer = 0f;
        }
    }

    private bool IsTeamVisible(int team)
    {
        if (team < 0) return true; 

        switch (team)
        {
            case 0: return true;
            case 1: return TeamFilter.ShowTr;      
            case 2: return TeamFilter.ShowCt;      
            case 3: return TeamFilter.ShowSpectator; 
            default: return true;
        }
    }

    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            Type bt = null, pt = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    bt ??= asm.GetType("BotPlayerController");
                    pt ??= asm.GetType("PlayerController");
                }
                catch { }
                if (bt != null && pt != null) break;
            }

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);

            if (pt != null)
            {
                foreach (var m in pt.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (m.Name.StartsWith("bcbi") || m.Name.StartsWith("bcbl") || (m.ReturnType.Name.Contains("Team") && m.GetParameters().Length == 0))
                    {
                        _teamMember = m;
                        break;
                    }
                }

                if (_teamMember == null)
                {
                    foreach (var p in pt.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (p.Name.Contains("Team") || p.PropertyType.Name.Contains("Team"))
                        {
                            _teamMember = p;
                            break;
                        }
                    }
                }
            }

            Plugin.LogSource?.LogInfo($"[ESP] Types Resolved. Team Member found: {_teamMember != null}");
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[ESP] ResolveTypes FATAL EX: {e}");
        }
    }

    void DoScan()
    {
        try
        {
            ResolveTypes();
            if (_botType == null && _playerType == null) return;

            _seenList.Clear();
            ScanTypeInternal(_botType, true, "Bot");
            ScanTypeInternal(_playerType, false, "Player");

            var dead = new List<int>();
            foreach (var kv in _tracked)
                if (!_seenList.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) _tracked.Remove(id);
        }
        catch (Exception e)
        {
            LogThrottled("scan_ex", $"[ESP] DoScan EX: {e}", true);
        }
    }

    void ScanTypeInternal(Il2CppSystem.Type type, bool isBot, string label)
    {
        if (type == null) return;
        try
        {
            var arr = FindObjectsOfType(type, false);
            if (arr == null) return;

            for (int i = 0; i < arr.Length; i++)
            {
                try
                {
                    var o = arr[i];
                    if (o == null) continue;
                    var c = o.TryCast<Component>();
                    if (c == null) continue;
                    var tr = c.transform;
                    if (tr == null) continue;
                    var go = c.gameObject;
                    if (go == null) continue;

                    int id = go.GetInstanceID();
                    if (_seenList.Contains(id)) continue;
                    _seenList.Add(id);

                    int targetTeam = ReadTeam(c);

                    string name = TryGetName(c, isBot);
                    bool isNew = !_tracked.ContainsKey(id);
                    _tracked[id] = new Tracked
                    {
                        Transform = tr,
                        IsBot = isBot,
                        Team = targetTeam,
                        Name = name
                    };

                    if (isNew)
                        Plugin.LogSource?.LogInfo($"[ESP] Tracked {label}: name='{name}', team={targetTeam}");
                }
                catch { }
            }
        }
        catch { }
    }

    string TryGetName(Component c, bool isBot)
    {
        try
        {
            var props = c.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.PropertyType == typeof(string))
                {
                    var val = p.GetValue(c) as string;
                    if (!string.IsNullOrEmpty(val) && val.Length < 25 && !val.Contains("Clone") && !val.Contains("Player") && !val.Contains("Bot"))
                    {
                        return val;
                    }
                }
            }
        }
        catch { }

        try
        {
            string n = c.gameObject.name;
            if (!string.IsNullOrEmpty(n) && n != "Player(Clone)" && n != "Bot" && n != "Player")
            {
                return n;
            }
        }
        catch { }

        return isBot ? "Bot" : "Player";
    }

    void OnGUI()
    {
        try
        {
            if (_whiteTex == null) _whiteTex = new Texture2D(1, 1);
            if (_circleTex == null) _circleTex = CreateCircleTexture(64);
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            if (_espBoxesEnabled || _espNamesEnabled)
            {
                foreach (var t in _toDraw)
                {
                    try
                    {
                        if (t.Transform == null) continue;

                        Vector3 headWorld = t.Transform.position + Vector3.up * HeadOffset;
                        Vector3 footWorld = t.Transform.position - Vector3.up * FootOffset;

                        Vector3 screenHead = _cam.WorldToScreenPoint(headWorld);
                        Vector3 screenFoot = _cam.WorldToScreenPoint(footWorld);

                        if (screenHead.z < 0 || screenFoot.z < 0) continue;

                        float x = screenHead.x;
                        float top = Screen.height - screenHead.y;
                        float bottom = Screen.height - screenFoot.y;
                        float height = Mathf.Abs(bottom - top);
                        float width = height * BoxWidthRatio;
                        if (width < 4f) width = 4f;
                        if (height < 8f) height = 8f;

                        float left = x - width / 2f;
                        float dist = Vector3.Distance(_cam.transform.position, t.Transform.position);

                        if (_espBoxesEnabled)
                        {
                            float th = 2f;
                            Color fillColor = t.Team == 1 ? new Color(1f, 0f, 0f, 0.12f) : new Color(0f, 0.5f, 1f, 0.12f);
                            Color outlineColor = t.Team == 1 ? Color.red : Color.cyan;

                            GUI.color = fillColor;
                            GUI.DrawTexture(new Rect(left, top, width, height), _whiteTex);

                            GUI.color = outlineColor;
                            GUI.DrawTexture(new Rect(left, top, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top + height - th, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top, th, height), _whiteTex);
                            GUI.DrawTexture(new Rect(left + width - th, top, th, height), _whiteTex);
                        }

                        if (_espNamesEnabled)
                        {
                            string label = $"{t.Name}";
                            if (_espDistanceEnabled) label += $" [{dist:F0}m]";

                            float approxWidth = label.Length * 7f;
                            float labelLeft = x - approxWidth / 2f;

                            GUI.color = Color.black;
                            GUI.Label(new Rect(labelLeft - 1f, top - 19f, approxWidth, 20f), label);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(labelLeft, top - 18f, approxWidth, 20f), label);
                        }
                    }
                    catch { }
                }
            }

            if (_menuVisible) DrawMenu();
            else DrawFloatingButton();
        }
        catch { }
        finally
        {
            GUI.color = Color.white;
        }
    }

    private void DrawFloatingButton()
    {
        if (!_buttonInitialized)
        {
            _buttonCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            _buttonInitialized = true;
        }

        float r = ButtonRadius;
        Rect btnRect = new Rect(_buttonCenter.x - r, _buttonCenter.y - r, r * 2f, r * 2f);

        Event e = Event.current;
        if (e != null)
        {
            Vector2 mouse = e.mousePosition;
            if (e.type == EventType.MouseDown && e.button == 0 && btnRect.Contains(mouse))
            {
                _isDragging = true;
                _dragMoved = false;
                _dragOffset = mouse - _buttonCenter;
                _dragStartMouse = mouse;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                if ((mouse - _dragStartMouse).sqrMagnitude > 9f) _dragMoved = true;
                _buttonCenter = mouse - _dragOffset;
                _buttonCenter.x = Mathf.Clamp(_buttonCenter.x, r, Screen.width - r);
                _buttonCenter.y = Mathf.Clamp(_buttonCenter.y, r, Screen.height - r);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _isDragging)
            {
                _isDragging = false;
                if (!_dragMoved) _menuVisible = true;
                e.Use();
            }
        }

        GUI.color = _isDragging ? new Color(0.3f, 0.6f, 1f, 0.9f) : new Color(0.15f, 0.15f, 0.2f, 0.85f);
        GUI.DrawTexture(btnRect, _circleTex);
        GUI.color = Color.white;
        GUI.Label(new Rect(_buttonCenter.x - 7f, _buttonCenter.y - 10f, 14f, 20f), "E");
    }

    private bool CustomButton(Rect rect, string label, bool state)
    {
        Event e = Event.current;
        bool clicked = false;
        if (e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            clicked = true;
        }

        GUI.color = state ? new Color(0.2f, 0.55f, 0.3f, 0.95f) : new Color(0.55f, 0.2f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, _whiteTex);
        GUI.color = Color.white;
        
        float approxW = Math.Min(label.Length * 6.5f, rect.width - 8f);
        GUI.Label(new Rect(rect.x + (rect.width - approxW) / 2f, rect.y + (rect.height - 16f) / 2f, approxW + 4f, 16f), label);
        return clicked;
    }

    private Rect GetMenuRect()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        float mw = MenuWidth;
        float mh = MenuHeight;
        float r = ButtonRadius;

        float x = _buttonCenter.x + r + MenuOffset;
        if (x + mw > sw) x = _buttonCenter.x - r - MenuOffset - mw;
        if (x < 0) x = Mathf.Max(0, (sw - mw) / 2f);

        float y = _buttonCenter.y - mh / 2f;
        if (y < 0) y = 0;
        if (y + mh > sh) y = sh - mh;

        return new Rect(x, y, mw, mh);
    }

    private void DrawMenu()
    {
        Rect panelRect = GetMenuRect();
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        GUI.DrawTexture(panelRect, _whiteTex);
        GUI.color = Color.white;

        GUI.Label(new Rect(panelRect.x + 10, panelRect.y + 8, panelRect.width - 20, 25), "<b>DynamicESP</b>");

        float y = panelRect.y + 40;
        float bx = panelRect.x + 10;
        float bw = panelRect.width - 20;
        float bh = 30;

        if (CustomButton(new Rect(bx, y, bw, bh), $"ESP Boxes: {(_espBoxesEnabled ? "ON" : "OFF")}", _espBoxesEnabled)) _espBoxesEnabled = !_espBoxesEnabled;
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Names: {(_espNamesEnabled ? "ON" : "OFF")}", _espNamesEnabled)) _espNamesEnabled = !_espNamesEnabled;
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Distance: {(_espDistanceEnabled ? "ON" : "OFF")}", _espDistanceEnabled)) _espDistanceEnabled = !_espDistanceEnabled;
        y += 46;

        GUI.Label(new Rect(bx, y, bw, 22), "<b>Team filter:</b>");
        y += 26;

        if (CustomButton(new Rect(bx, y, bw, bh), $"TR (Terror): {(TeamFilter.ShowTr ? "ON" : "OFF")}", TeamFilter.ShowTr)) TeamFilter.ShowTr = !TeamFilter.ShowTr;
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"CT (Counter): {(TeamFilter.ShowCt ? "ON" : "OFF")}", TeamFilter.ShowCt)) TeamFilter.ShowCt = !TeamFilter.ShowCt;
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Spectator: {(TeamFilter.ShowSpectator ? "ON" : "OFF")}", TeamFilter.ShowSpectator)) TeamFilter.ShowSpectator = !TeamFilter.ShowSpectator;
        y += 46;

        if (CustomButton(new Rect(bx, y, bw, 34), "Hide Menu", false)) _menuVisible = false;
    }
}
