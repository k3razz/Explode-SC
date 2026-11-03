using BepInEx;
using BepInEx.Unity.IL2CPP;
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
                Debug.Log("[ESP] EnsureBehaviour: created new instance");
            }
            else
            {
                Debug.LogError("[ESP] EnsureBehaviour: failed to get Il2CppType");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] EnsureBehaviour EX: {e}");
        }
    }
}

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("[ESP] ===== Plugin.Load() =====");
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<EspBehaviour>();
            Log.LogInfo("[ESP] EspBehaviour registered in IL2CPP");

            SceneManager.sceneLoaded += (Action<Scene, LoadSceneMode>)OnSceneLoaded;
            Log.LogInfo("[ESP] Subscribed to sceneLoaded");

            EspManager.EnsureBehaviour();
        }
        catch (Exception e)
        {
            Log.LogError($"[ESP] Failed to add EspBehaviour: {e}");
        }
        Log.LogInfo("[ESP] ===== Plugin.Load() done =====");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[ESP] Scene loaded: '{scene.name}' ({mode}) -> recreating behaviour");
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
    private const float ScanInterval = 1.0f;

    private float _watchdogTimer;
    private const float WatchdogInterval = 5.0f;
    private int _watchdogTicks;

    private const float HeadOffset = 1.8f;
    private const float FootOffset = 0.1f;
    private const float BoxWidthRatio = 0.4f;

    private bool _espBoxesEnabled = true;
    private bool _espNamesEnabled = true;
    private bool _espDistanceEnabled = true;
    private bool _menuVisible = false;

    // ---- Круглая кнопка ----
    private const float ButtonRadius = 22f;
    private Vector2 _buttonCenter = new Vector2(-1f, -1f);
    private bool _buttonInitialized = false;
    private bool _isDragging = false;
    private Vector2 _dragOffset;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private static PropertyInfo _localPlayerProp;
    private static PropertyInfo _teamProp;
    private int _localTeam = -1;
    private int _lastLoggedTeam = -2;

    private readonly Dictionary<string, float> _lastLogTime = new();
    private const float LogCooldown = 2.0f;

    private int _updateCount;
    private float _lastUpdateLog;
    private int _scanCount;
    private int _guiFrameCount;

    private void LogThrottled(string key, string message, bool isError = false)
    {
        float now = Time.realtimeSinceStartup;
        if (_lastLogTime.TryGetValue(key, out float last) && now - last < LogCooldown)
            return;
        _lastLogTime[key] = now;
        if (isError) Debug.LogError(message);
        else Debug.Log(message);
    }

    void Awake()
    {
        Debug.Log("[ESP] ===== Awake() =====");
        try
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            _circleTex = CreateCircleTexture(64);
        }
        catch (Exception e) { Debug.LogError($"[ESP] Awake texture EX: {e}"); }

        try
        {
            enabled = true;
            gameObject.SetActive(true);
            Debug.Log($"[ESP] Awake state: enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}");
        }
        catch (Exception e) { Debug.LogError($"[ESP] Awake activate EX: {e}"); }

        Debug.Log("[ESP] ===== Awake() done =====");
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
        Debug.Log($"[ESP] ===== Start() ===== enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}");
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Debug.Log("[ESP] Cursor unlocked");
    }

    void OnDestroy()
    {
        Debug.Log("[ESP] OnDestroy() - behaviour destroyed");
        try
        {
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_circleTex != null) Destroy(_circleTex);
        }
        catch (Exception e) { Debug.LogError($"[ESP] OnDestroy EX: {e}"); }
    }

    // =========================================================
    //                       WATCHDOG
    // =========================================================
    void RunWatchdog()
    {
        _watchdogTicks++;
        try
        {
            bool localPlayerOk = CheckLocalPlayer();
            bool cameraOk = _cam != null;
            bool typesOk = _botType != null || _playerType != null;

            Debug.Log($"[ESP] Watchdog #{_watchdogTicks}: localPlayer={localPlayerOk}, " +
                      $"camera={cameraOk}, types={typesOk}, tracked={_tracked.Count}, localTeam={_localTeam}");

            if (!typesOk)
            {
                Debug.LogWarning("[ESP] Watchdog: types not resolved, forcing re-resolve");
                _resolved = false;
                ResolveTypes();
            }

            if (!cameraOk)
            {
                _cam = Camera.main;
                Debug.Log($"[ESP] Watchdog: camera re-fetched, result={(_cam != null ? _cam.name : "null")}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] Watchdog EX: {e}");
        }
    }

    bool CheckLocalPlayer()
    {
        try
        {
            if (_localPlayerProp == null || _teamProp == null) return false;

            var pmType = _localPlayerProp.DeclaringType;
            object pmInstance = null;
            var instProp = pmType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instProp != null) pmInstance = instProp.GetValue(null);

            if (pmInstance == null) return false;

            var localPlayer = _localPlayerProp.GetValue(pmInstance);
            if (localPlayer == null) return false;

            var teamObj = _teamProp.GetValue(localPlayer);
            if (teamObj != null)
            {
                int newTeam = Convert.ToInt32(teamObj);
                if (newTeam != _localTeam)
                {
                    Debug.Log($"[ESP] Watchdog: Local team changed: {_localTeam} -> {newTeam}");
                    _localTeam = newTeam;
                    _lastLoggedTeam = newTeam;
                }
            }
            return true;
        }
        catch (Exception e)
        {
            LogThrottled("checklocal_ex", $"[ESP] CheckLocalPlayer EX: {e.Message}", true);
            return false;
        }
    }

    // =========================================================
    //                       UPDATE
    // =========================================================
    void Update()
    {
        try
        {
            _updateCount++;

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
            int skippedOwnTeam = 0, skippedNull = 0, skippedFiltered = 0;
            foreach (var t in _tracked.Values)
            {
                if (t.Transform == null) { skippedNull++; continue; }
                if (_localTeam != -1 && t.Team == _localTeam) { skippedOwnTeam++; continue; }
                if (!IsTeamVisible(t.Team)) { skippedFiltered++; continue; }
                _toDraw.Add(t);
            }

            if (Time.realtimeSinceStartup - _lastUpdateLog > 5f)
            {
                _lastUpdateLog = Time.realtimeSinceStartup;
                Debug.Log($"[ESP] Update alive: tracked={_tracked.Count}, draw={_toDraw.Count}, " +
                          $"skippedOwnTeam={skippedOwnTeam}, skippedNull={skippedNull}, skippedFiltered={skippedFiltered}, " +
                          $"localTeam={_localTeam}, menu={_menuVisible}, updates={_updateCount}");
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
        switch (team)
        {
            case 1: return TeamFilter.ShowTr;
            case 2: return TeamFilter.ShowCt;
            case 3: return TeamFilter.ShowSpectator;
            default: return true;
        }
    }

    // =========================================================
    //                       SCAN
    // =========================================================
    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        Debug.Log("[ESP] ResolveTypes() start");
        try
        {
            Type bt = null, pt = null, pmType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    bt ??= asm.GetType("Chillow.StandChillow.Bots.Core.BotPlayerController");
                    pt ??= asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                    pmType ??= asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ESP] ResolveTypes asm '{asm.GetName().Name}' EX: {e.Message}");
                }
                if (bt != null && pt != null && pmType != null) break;
            }

            Debug.Log($"[ESP] Found types: Bot={bt?.FullName ?? "null"}, Player={pt?.FullName ?? "null"}, PM={pmType?.FullName ?? "null"}");

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);

            if (pmType != null && pt != null)
            {
                _localPlayerProp = pmType.GetProperty("DEEBDAFCBHCGFBA",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                _teamProp = pt.GetProperty("CGGHFHCAFFBBCBB",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                Debug.Log($"[ESP] Properties: LocalPlayer={(_localPlayerProp != null)}, Team={(_teamProp != null)}");
            }

            if (_botType == null && _playerType == null)
                Debug.LogError("[ESP] ResolveTypes: NO types resolved! ESP will not work.");
            else
                Debug.Log($"[ESP] ResolveTypes OK: Bot={_botType != null}, Player={_playerType != null}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolveTypes FATAL EX: {e}");
        }
    }

    void DoScan()
    {
        _scanCount++;
        try
        {
            ResolveTypes();
            if (_botType == null && _playerType == null) return;

            CheckLocalPlayer();

            _seenList.Clear();
            int beforeCount = _tracked.Count;
            ScanTypeInternal(_botType, true, "Bot");
            ScanTypeInternal(_playerType, false, "Player");

            var dead = new List<int>();
            foreach (var kv in _tracked)
                if (!_seenList.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) _tracked.Remove(id);

            int added = _tracked.Count - beforeCount + dead.Count;
            int removed = dead.Count;

            if (removed > 0 || added > 0 || _scanCount % 5 == 0)
            {
                Debug.Log($"[ESP] Scan #{_scanCount}: tracked={_tracked.Count} " +
                          $"(added={added}, removed={removed}), localTeam={_localTeam}");
            }
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

                    int targetTeam = -1;
                    if (_teamProp != null)
                    {
                        try
                        {
                            var teamObj = _teamProp.GetValue(c);
                            if (teamObj != null) targetTeam = Convert.ToInt32(teamObj);
                        }
                        catch (Exception e)
                        {
                            LogThrottled($"team_{label}_{id}", $"[ESP] Get team EX ({label} id={id}): {e.Message}", true);
                        }
                    }

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
                        Debug.Log($"[ESP] +Tracked {label}: name='{name}', team={targetTeam}, id={id}");
                }
                catch (Exception e)
                {
                    LogThrottled($"scan_item_{label}", $"[ESP] Scan item EX ({label}): {e.Message}", true);
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled($"scan_type_{label}", $"[ESP] ScanTypeInternal({label}) EX: {e.Message}", true);
        }
    }

    string TryGetName(Component c, bool isBot)
    {
        try
        {
            var prop = c.GetType().GetProperty("HBBBCEFBCFEFBBA",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                var pp = prop.GetValue(c);
                if (pp != null)
                {
                    var nickProp = pp.GetType().GetProperty("NickName",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (nickProp != null)
                    {
                        var nick = nickProp.GetValue(pp) as string;
                        if (!string.IsNullOrEmpty(nick)) return nick;
                    }
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled($"name_ex_{isBot}", $"[ESP] TryGetName EX (isBot={isBot}): {e.Message}", true);
        }
        try
        {
            var n = c.gameObject.name;
            if (!string.IsNullOrEmpty(n)) return n;
        }
        catch { }
        return isBot ? "BOT" : "PLAYER";
    }

    // =========================================================
    //                       OnGUI
    // =========================================================
    void OnGUI()
    {
        _guiFrameCount++;
        try
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            if (_circleTex == null)
            {
                _circleTex = CreateCircleTexture(64);
            }

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null)
                {
                    LogThrottled("no_cam", "[ESP] OnGUI: Camera.main is null!", true);
                    return;
                }
                Debug.Log($"[ESP] Camera found: {_cam.name}");
            }

            // ---- ESP ----
            if (_espBoxesEnabled || _espNamesEnabled)
            {
                int drawn = 0, skippedBehind = 0, skippedNull = 0;
                foreach (var t in _toDraw)
                {
                    try
                    {
                        if (t.Transform == null) { skippedNull++; continue; }

                        Vector3 headWorld = t.Transform.position + Vector3.up * HeadOffset;
                        Vector3 footWorld = t.Transform.position - Vector3.up * FootOffset;

                        Vector3 screenHead = _cam.WorldToScreenPoint(headWorld);
                        Vector3 screenFoot = _cam.WorldToScreenPoint(footWorld);

                        if (screenHead.z < 0 || screenFoot.z < 0) { skippedBehind++; continue; }

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
                            Color fillColor = t.Team == 1
                                ? new Color(1f, 0f, 0f, 0.12f)
                                : new Color(0f, 0.5f, 1f, 0.12f);
                            Color outlineColor = t.Team == 1 ? Color.red : Color.cyan;

                            GUI.color = fillColor;
                            GUI.DrawTexture(new Rect(left, top, width, height), _whiteTex);

                            GUI.color = outlineColor;
                            GUI.DrawTexture(new Rect(left, top, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top + height - th, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top, th, height), _whiteTex);
                            GUI.DrawTexture(new Rect(left + width - th, top, th, height), _whiteTex);
                            GUI.color = Color.white;
                        }

                        if (_espNamesEnabled)
                        {
                            string label = string.IsNullOrEmpty(t.Name)
                                ? (t.IsBot ? "BOT" : "PLAYER")
                                : t.Name;
                            if (_espDistanceEnabled)
                                label += $" [{dist:F0}m]";

                            float approxWidth = label.Length * 7f;
                            float labelLeft = x - approxWidth / 2f;

                            GUI.color = Color.black;
                            GUI.Label(new Rect(labelLeft - 1f, top - 19f, approxWidth, 20f), label);
                            GUI.Label(new Rect(labelLeft + 1f, top - 17f, approxWidth, 20f), label);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(labelLeft, top - 18f, approxWidth, 20f), label);
                        }

                        drawn++;
                    }
                    catch (Exception e)
                    {
                        LogThrottled("gui_item", $"[ESP] OnGUI item EX: {e.Message}", true);
                    }
                }

                if (_guiFrameCount % 600 == 0 && _toDraw.Count > 0)
                {
                    Debug.Log($"[ESP] OnGUI: drawn={drawn}, total={_toDraw.Count}, " +
                              $"skippedBehind={skippedBehind}, skippedNull={skippedNull}");
                }
            }

            // ---- Меню или круглая кнопка ----
            if (_menuVisible)
            {
                DrawMenu();
            }
            else
            {
                DrawFloatingButton();
            }
        }
        catch (Exception e)
        {
            LogThrottled("ongui_ex", $"[ESP] OnGUI EX: {e}", true);
        }
        finally
        {
            GUI.color = Color.white;
        }
    }

    // ---- Плавающая круглая кнопка (без GUIStyle) ----
    private void DrawFloatingButton()
    {
        if (!_buttonInitialized)
        {
            _buttonCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            _buttonInitialized = true;
            Debug.Log($"[ESP] Floating button initialized at center: {_buttonCenter}");
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
                _dragOffset = mouse - _buttonCenter;
                e.Use();
                Debug.Log("[ESP] Floating button drag started");
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                _buttonCenter = mouse - _dragOffset;
                _buttonCenter.x = Mathf.Clamp(_buttonCenter.x, r, Screen.width - r);
                _buttonCenter.y = Mathf.Clamp(_buttonCenter.y, r, Screen.height - r);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _isDragging)
            {
                _isDragging = false;
                if ((mouse - (_buttonCenter + _dragOffset)).sqrMagnitude < 25f)
                {
                    _menuVisible = true;
                    Debug.Log("[ESP] Floating button clicked -> menu opened");
                }
                else
                {
                    Debug.Log($"[ESP] Floating button moved to {_buttonCenter}");
                }
                e.Use();
            }
        }

        // Круг
        GUI.color = _isDragging
            ? new Color(0.3f, 0.6f, 1f, 0.9f)
            : new Color(0.15f, 0.15f, 0.2f, 0.85f);
        GUI.DrawTexture(btnRect, _circleTex);

        // Буква E по центру (без GUIStyle — interop не поддерживает)
        GUI.color = Color.white;
        float letterW = 14f;
        float letterH = 20f;
        GUI.Label(new Rect(
            _buttonCenter.x - letterW / 2f,
            _buttonCenter.y - letterH / 2f,
            letterW,
            letterH), "E");
    }

    // ---- Меню ----
    private void DrawMenu()
    {
        Rect panelRect = new Rect(20, 20, 340, 460);
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        GUI.DrawTexture(panelRect, _whiteTex);
        GUI.color = Color.white;

        GUI.Label(new Rect(panelRect.x + 10, panelRect.y + 8, 320, 25), "<b>DynamicESP</b>");

        float y = panelRect.y + 40;
        float bx = panelRect.x + 10;
        float bw = 320;

        if (GUI.Button(new Rect(bx, y, bw, 30),
            $"ESP Boxes: {(_espBoxesEnabled ? "ON" : "OFF")}"))
        {
            _espBoxesEnabled = !_espBoxesEnabled;
            Debug.Log($"[ESP] ESP Boxes = {_espBoxesEnabled}");
        }
        y += 36;

        if (GUI.Button(new Rect(bx, y, bw, 30),
            $"Names: {(_espNamesEnabled ? "ON" : "OFF")}"))
        {
            _espNamesEnabled = !_espNamesEnabled;
            Debug.Log($"[ESP] Names = {_espNamesEnabled}");
        }
        y += 36;

        if (GUI.Button(new Rect(bx, y, bw, 30),
            $"Distance: {(_espDistanceEnabled ? "ON" : "OFF")}"))
        {
            _espDistanceEnabled = !_espDistanceEnabled;
            Debug.Log($"[ESP] Distance = {_espDistanceEnabled}");
        }
        y += 46;

        GUI.Label(new Rect(bx, y, bw, 22), "<b>Team filter:</b>");
        y += 26;

        if (GUI.Button(new Rect(bx, y, bw, 30),
            $"TR (Terror): {(TeamFilter.ShowTr ? "ON" : "OFF")}"))
        {
            TeamFilter.ShowTr = !TeamFilter.ShowTr;
            Debug.Log($"[ESP] Show TR = {TeamFilter.ShowTr}");
        }
        y += 36;

        if (GUI.Button(new Rect(bx, y, bw, 30),
            $"CT (Counter): {(TeamFilter.ShowCt ? "ON" : "OFF")}"))
        {
            TeamFilter.ShowCt = !TeamFilter.ShowCt;
            Debug.Log($"[ESP] Show CT = {TeamFilter.ShowCt}");
        }
        y += 36;

        if (GUI.Button(new Rect(bx, y, bw, 30),
            $"Spectator: {(TeamFilter.ShowSpectator ? "ON" : "OFF")}"))
        {
            TeamFilter.ShowSpectator = !TeamFilter.ShowSpectator;
            Debug.Log($"[ESP] Show Spectator = {TeamFilter.ShowSpectator}");
        }
        y += 46;

        if (GUI.Button(new Rect(bx, y, bw, 34), "Hide Menu"))
        {
            _menuVisible = false;
            Debug.Log("[ESP] Menu hidden via button");
        }
    }
}
