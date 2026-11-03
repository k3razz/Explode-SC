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
using System.Runtime.InteropServices;

namespace MyDynamicESP;

public static class TeamFilter
{
    public static bool ShowTr = true;
    public static bool ShowCt = true;
    public static bool ShowSpectator = false;
    public static bool OnlyEnemies = false;
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
        public object BipedMap;
        public int LastSeenFrame;
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

    // ---- ESP ----
    private bool _espBoxesEnabled = true;
    private bool _espNamesEnabled = true;
    private bool _espDistanceEnabled = true;

    // ---- Menu ----
    private bool _menuVisible = false;
    private int _menuTab = 0;

    private const float MenuWidth = 480f;
    private const float MenuHeight = 520f;
    private const float MenuOffset = 30f;
    private const float TabWidth = 110f;

    // ---- Floating button ----
    private const float ButtonRadius = 22f;
    private Vector2 _buttonCenter = new Vector2(-1f, -1f);
    private bool _buttonInitialized = false;
    private bool _isDragging = false;
    private bool _dragMoved = false;
    private Vector2 _dragOffset;
    private Vector2 _dragStartMouse;

    // ---- Aimbot ----
    private bool _aimbotEnabled = false;
    private float _aimbotFov = 15f;
    private float _aimbotSmooth = 15f;
    private bool _aimbotTargetTeammates = false;
    private bool _aimbotVisibilityCheck = false;
    private bool _aimbotToggleMode = false;
    private KeyCode _aimbotKey = KeyCode.Mouse1;
    private bool _aimbotKeyCapturing = false;
    private bool _aimbotActive = false;
    private bool _aimbotPrevKeyState = false;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private static MemberInfo _playerTeamMember;
    private static MemberInfo _localPlayerMember;
    private static Il2CppSystem.Type _playerManagerType;
    private int _localTeam = -1;

    private readonly Dictionary<string, float> _lastLogTime = new();
    private const float LogCooldown = 1.5f;

    private void LogThrottled(string key, string message, bool isError = false)
    {
        float now = Time.realtimeSinceStartup;
        if (_lastLogTime.TryGetValue(key, out float last) && now - last < LogCooldown) return;
        _lastLogTime[key] = now;

        if (isError) { Debug.LogError(message); Plugin.LogSource?.LogError(message); }
        else { Debug.Log(message); Plugin.LogSource?.LogInfo(message); }
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

            if (!typesOk) { _resolved = false; ResolveTypes(); }
            if (!cameraOk) _cam = Camera.main;

            if (_watchdogTicks % 6 == 0)
            {
                UpdateLocalTeam();
                Plugin.LogSource?.LogInfo($"[ESP] Watchdog #{_watchdogTicks}: tracked={_tracked.Count}, localTeam={_localTeam}, aimbot={_aimbotEnabled}, active={_aimbotActive}");
            }
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[ESP] Watchdog EX: {e}");
        }
    }

    // =========================================================
    //                    TEAM READING
    // =========================================================
    int ReadTeamFromMember(MemberInfo member, object target)
    {
        if (member == null || target == null) return -1;
        try
        {
            object val = null;
            if (member is MethodInfo m) val = m.Invoke(target, null);
            else if (member is PropertyInfo p) val = p.GetValue(target);
            else if (member is FieldInfo f) val = f.GetValue(target);

            if (val != null)
            {
                if (val is Enum en) return Convert.ToInt32(en);
                return Convert.ToInt32(val);
            }
        }
        catch { }
        return -1;
    }

    int ReadByteAtOffset(object obj, int offset)
    {
        try
        {
            IntPtr ptr = IntPtr.Zero;
            if (obj is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj)
                ptr = il2cppObj.Pointer;
            else if (obj is Component comp)
                ptr = comp.Pointer;

            if (ptr == IntPtr.Zero) return -1;
            return Marshal.ReadByte(ptr, offset);
        }
        catch { return -1; }
    }

    int ReadTeam(Component component, bool isBot)
    {
        if (component == null) return -1;

        if (isBot)
        {
            int team = ReadTeamFromMember(_playerTeamMember, component);
            if (team >= 0) return team;

            try
            {
                var t = component.GetType();
                object playerCtrl = null;
                var crvsProp = t.GetProperty("crvs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (crvsProp != null) playerCtrl = crvsProp.GetValue(component);
                if (playerCtrl == null)
                {
                    var crvsField = t.GetField("crvs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (crvsField != null) playerCtrl = crvsField.GetValue(component);
                }
                if (playerCtrl != null)
                {
                    team = ReadTeamFromMember(_playerTeamMember, playerCtrl);
                    if (team >= 0) return team;
                }
            }
            catch { }

            int offTeam = ReadByteAtOffset(component, 0x24);
            if (offTeam > 0 && offTeam <= 3) return offTeam;
            return -1;
        }
        else
        {
            return ReadTeamFromMember(_playerTeamMember, component);
        }
    }

    void UpdateLocalTeam()
    {
        try
        {
            if (_localPlayerMember == null || _playerManagerType == null) return;

            object pmInstance = null;
            var pmType = _localPlayerMember.DeclaringType;
            var instProp = pmType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instProp != null) pmInstance = instProp.GetValue(null);
            if (pmInstance == null) return;

            object localPlayer = null;
            if (_localPlayerMember is PropertyInfo lp) localPlayer = lp.GetValue(pmInstance);
            else if (_localPlayerMember is FieldInfo lf) localPlayer = lf.GetValue(pmInstance);
            if (localPlayer == null) return;

            int newTeam = ReadTeamFromMember(_playerTeamMember, localPlayer);
            if (newTeam != _localTeam && newTeam >= 0)
            {
                Plugin.LogSource?.LogInfo($"[ESP] Local team changed: {_localTeam} -> {newTeam}");
                _localTeam = newTeam;
            }
        }
        catch { }
    }

    // =========================================================
    //                    UPDATE
    // =========================================================
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
                if (TeamFilter.OnlyEnemies && _localTeam > 0 && t.Team == _localTeam) continue;
                if (!IsTeamVisible(t.Team)) continue;
                _toDraw.Add(t);
            }

            if (!_aimbotKeyCapturing)
            {
                bool keyDown = false;
                try { keyDown = Input.GetKey(_aimbotKey); } catch { }

                if (_aimbotToggleMode)
                {
                    if (keyDown && !_aimbotPrevKeyState) _aimbotActive = !_aimbotActive;
                    _aimbotPrevKeyState = keyDown;
                }
                else
                {
                    _aimbotActive = keyDown;
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled("update_ex", $"[ESP] Update EX: {e}", true);
            _scanTimer = 0f;
        }
    }

    // =========================================================
    //                    AIMBOT (LateUpdate)
    // =========================================================
    void LateUpdate()
    {
        try
        {
            if (!_aimbotEnabled) return;
            if (!_aimbotActive) return;
            if (_aimbotKeyCapturing) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Tracked best = null;
            float bestAngle = _aimbotFov;
            Vector3 camPos;
            Vector3 camForward;
            try
            {
                camPos = _cam.transform.position;
                camForward = _cam.transform.forward;
            }
            catch { return; }

            foreach (var t in _tracked.Values)
            {
                try
                {
                    if (t.Transform == null) continue;
                    if (!_aimbotTargetTeammates && _localTeam > 0 && t.Team == _localTeam) continue;

                    if (_aimbotVisibilityCheck)
                    {
                        if (!IsVisible(t, camPos)) continue;
                    }

                    Vector3 headPos = GetHeadPosition(t);
                    Vector3 dir = (headPos - camPos).normalized;
                    float angle = Vector3.Angle(camForward, dir);
                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        best = t;
                    }
                }
                catch { }
            }

            if (best == null) return;

            Vector3 targetHead = GetHeadPosition(best);
            Vector3 targetDir = (targetHead - camPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            try
            {
                _cam.transform.rotation = Quaternion.Slerp(
                    _cam.transform.rotation, targetRot, _aimbotSmooth * Time.deltaTime);
            }
            catch { }
        }
        catch (Exception e)
        {
            LogThrottled("aim_ex", $"[ESP] LateUpdate EX: {e.Message}", true);
        }
    }

    bool IsVisible(Tracked t, Vector3 camPos)
    {
        try
        {
            Vector3 headPos = GetHeadPosition(t);
            Vector3 dir = (headPos - camPos);
            float dist = dir.magnitude;
            if (dist < 0.1f) return true;
            if (Physics.Raycast(camPos, dir.normalized, out var hit, dist))
            {
                return hit.transform == t.Transform || hit.transform.IsChildOf(t.Transform);
            }
            return true;
        }
        catch { return true; }
    }

    Vector3 GetHeadPosition(Tracked t)
    {
        try
        {
            if (t.BipedMap != null)
            {
                var nwh = t.BipedMap.GetType().GetMethod("nwh",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nwh != null)
                {
                    var headTr = nwh.Invoke(t.BipedMap, new object[] { 0 });
                    if (headTr is Transform ht) return ht.position;
                }
            }
        }
        catch { }
        return t.Transform.position + Vector3.up * 1.6f;
    }

    private bool IsTeamVisible(int team)
    {
        switch (team)
        {
            case 0: return false;
            case 1: return TeamFilter.ShowTr;
            case 2: return TeamFilter.ShowCt;
            case 3: return TeamFilter.ShowSpectator;
            default: return true;
        }
    }

    private static bool IsTeamEnum(Type t)
    {
        if (t == null) return false;
        return t.IsEnum && (t.Name == "Team" || t.FullName.EndsWith(".Team"));
    }

    private static Type FindTypeByName(string shortName, out string foundFullName)
    {
        foundFullName = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var direct = asm.GetType(shortName);
                if (direct != null) { foundFullName = direct.FullName; return direct; }
            }
            catch { }

            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == shortName)
                    {
                        foundFullName = t.FullName;
                        return t;
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private static MemberInfo FindTeamMember(Type startType, out string ownerName)
    {
        ownerName = null;
        if (startType == null) return null;
        Type search = startType;
        int depth = 0;
        while (search != null && search != typeof(object) && depth < 10)
        {
            foreach (var m in search.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.GetParameters().Length == 0 && IsTeamEnum(m.ReturnType))
                {
                    ownerName = $"{search.Name}.{m.Name}()";
                    return m;
                }
            }
            foreach (var p in search.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (IsTeamEnum(p.PropertyType) && p.GetGetMethod(true) != null)
                {
                    ownerName = $"{search.Name}.{p.Name}";
                    return p;
                }
            }
            foreach (var f in search.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (IsTeamEnum(f.FieldType))
                {
                    ownerName = $"{search.Name}.{f.Name}";
                    return f;
                }
            }
            search = search.BaseType;
            depth++;
        }
        return null;
    }

    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            Type bt = FindTypeByName("BotPlayerController", out var btName);
            Type pt = FindTypeByName("PlayerController", out var ptName);
            Type pmType = FindTypeByName("PlayerManager", out var pmName);

            Plugin.LogSource?.LogInfo($"[ESP] FindType: Bot='{btName ?? "null"}', Player='{ptName ?? "null"}', PM='{pmName ?? "null"}'");

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);
            if (pmType != null) _playerManagerType = Il2CppType.From(pmType);

            _playerTeamMember = null;
            _localPlayerMember = null;

            string playerOwner = null;
            _playerTeamMember = FindTeamMember(pt, out playerOwner);
            Plugin.LogSource?.LogInfo($"[ESP] Team member Player: {playerOwner ?? "null"}");

            if (pmType != null && pt != null)
            {
                foreach (var p in pmType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (p.PropertyType == pt) { _localPlayerMember = p; break; }
                }
                if (_localPlayerMember == null)
                {
                    foreach (var f in pmType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (f.FieldType == pt) { _localPlayerMember = f; break; }
                    }
                }
            }

            Plugin.LogSource?.LogInfo($"[ESP] Resolve: Bot={_botType != null}, Player={_playerType != null}, PM={_playerManagerType != null}, PlayerTeam={_playerTeamMember != null}, LocalPlayer={_localPlayerMember != null}");
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

            UpdateLocalTeam();

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

                    int targetTeam = ReadTeam(c, isBot);
                    string name = TryGetName(c, isBot);

                    object biped = null;
                    try
                    {
                        var bipedField = c.GetType().GetField("_characterBiped",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (bipedField != null) biped = bipedField.GetValue(c);
                    }
                    catch { }

                    bool isNew = !_tracked.ContainsKey(id);
                    _tracked[id] = new Tracked
                    {
                        Transform = tr,
                        IsBot = isBot,
                        Team = targetTeam,
                        Name = name,
                        BipedMap = biped,
                        LastSeenFrame = Time.frameCount
                    };

                    if (isNew)
                        Plugin.LogSource?.LogInfo($"[ESP] +Tracked {label}: name='{name}', team={targetTeam}, id={id}");
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
                        return val;
                }
            }
        }
        catch { }
        try
        {
            string n = c.gameObject.name;
            if (!string.IsNullOrEmpty(n) && n != "Player(Clone)" && n != "Bot" && n != "Player") return n;
        }
        catch { }
        return isBot ? "Bot" : "Player";
    }

    // =========================================================
    //                    OnGUI
    // =========================================================
    void OnGUI()
    {
        try
        {
            if (_whiteTex == null) { _whiteTex = new Texture2D(1, 1); _whiteTex.SetPixel(0, 0, Color.white); _whiteTex.Apply(); }
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
                            GUI.color = Color.white;
                        }

                        if (_espNamesEnabled)
                        {
                            string label = $"{t.Name}";
                            if (_espDistanceEnabled) label += $" [{dist:F0}m]";

                            float labelW = Math.Min(label.Length * 7.5f + 8f, 260f);
                            float labelLeft = x - labelW / 2f;

                            GUI.color = new Color(0f, 0f, 0f, 0.6f);
                            GUI.DrawTexture(new Rect(labelLeft - 2f, top - 21f, labelW + 4f, 20f), _whiteTex);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(labelLeft, top - 20f, labelW, 18f), label);
                        }
                    }
                    catch { }
                }
            }

            if (_aimbotEnabled)
            {
                float fovScreen = _aimbotFov * (Screen.height / 90f);
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(new Rect(Screen.width / 2f - fovScreen / 2f,
                    Screen.height / 2f - fovScreen / 2f, fovScreen, fovScreen), _circleTex);
                GUI.color = Color.white;
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
                if (!_dragMoved) { _menuVisible = true; }
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
            clicked = true;

        GUI.color = state ? new Color(0.2f, 0.55f, 0.3f, 0.95f) : new Color(0.55f, 0.2f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, _whiteTex);
        GUI.color = Color.white;

        float labelW = Math.Min(label.Length * 7.5f + 4f, rect.width - 6f);
        GUI.Label(new Rect(rect.x + (rect.width - labelW) / 2f,
            rect.y + (rect.height - 16f) / 2f, labelW, 16f), label);
        return clicked;
    }

    private bool TabButton(Rect rect, string label, bool active)
    {
        Event e = Event.current;
        bool clicked = false;
        if (e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            clicked = true;

        GUI.color = active ? new Color(0.2f, 0.4f, 0.7f, 0.95f) : new Color(0.15f, 0.15f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, _whiteTex);
        GUI.color = Color.white;

        float labelW = Math.Min(label.Length * 7.5f + 4f, rect.width - 6f);
        GUI.Label(new Rect(rect.x + (rect.width - labelW) / 2f,
            rect.y + (rect.height - 16f) / 2f, labelW, 16f), label);
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

        float bx = panelRect.x + 10;
        float by = panelRect.y + 10;
        float contentX = bx + TabWidth + 10f;
        float contentW = panelRect.width - (TabWidth + 30f);

        // Title
        GUI.Label(new Rect(bx, by, panelRect.width - 20f, 24f), "<b>DynamicESP</b>");
        by += 30;

        // Vertical tabs (left)
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "ESP", _menuTab == 0)) _menuTab = 0;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "AimBot", _menuTab == 1)) _menuTab = 1;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "Hide", false)) _menuVisible = false;

        // Content
        float cy = panelRect.y + 40;
        if (_menuTab == 0) DrawEspTab(contentX, cy, contentW);
        else DrawAimTab(contentX, cy, contentW);
    }

    private void DrawEspTab(float bx, float y, float bw)
    {
        float bh = 30;

        if (CustomButton(new Rect(bx, y, bw, bh), $"ESP Boxes: {(_espBoxesEnabled ? "ON" : "OFF")}", _espBoxesEnabled))
            _espBoxesEnabled = !_espBoxesEnabled;
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Names: {(_espNamesEnabled ? "ON" : "OFF")}", _espNamesEnabled))
            _espNamesEnabled = !_espNamesEnabled;
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Distance: {(_espDistanceEnabled ? "ON" : "OFF")}", _espDistanceEnabled))
            _espDistanceEnabled = !_espDistanceEnabled;
        y += 46;

        GUI.Label(new Rect(bx, y, bw, 22), "<b>Team filter:</b>");
        y += 26;

        if (CustomButton(new Rect(bx, y, bw, bh), $"TR (Terror): {(TeamFilter.ShowTr ? "ON" : "OFF")}", TeamFilter.ShowTr))
            TeamFilter.ShowTr = !TeamFilter.ShowTr;
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"CT (Counter): {(TeamFilter.ShowCt ? "ON" : "OFF")}", TeamFilter.ShowCt))
            TeamFilter.ShowCt = !TeamFilter.ShowCt;
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Spectator: {(TeamFilter.ShowSpectator ? "ON" : "OFF")}", TeamFilter.ShowSpectator))
            TeamFilter.ShowSpectator = !TeamFilter.ShowSpectator;
        y += 46;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Show only enemies: {(TeamFilter.OnlyEnemies ? "ON" : "OFF")}", TeamFilter.OnlyEnemies))
            TeamFilter.OnlyEnemies = !TeamFilter.OnlyEnemies;
    }

    private void DrawAimTab(float bx, float y, float bw)
    {
        float bh = 30;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Aimbot: {(_aimbotEnabled ? "ON" : "OFF")}", _aimbotEnabled))
            _aimbotEnabled = !_aimbotEnabled;
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>FOV:</b> {_aimbotFov:F0}°");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false))
            _aimbotFov = Mathf.Max(1f, _aimbotFov - 2f);
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true))
            _aimbotFov = Mathf.Min(90f, _aimbotFov + 2f);
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>Smooth:</b> {_aimbotSmooth:F0}");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false))
            _aimbotSmooth = Mathf.Max(1f, _aimbotSmooth - 5f);
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true))
            _aimbotSmooth = Mathf.Min(50f, _aimbotSmooth + 5f);
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Aim Teammates: {(_aimbotTargetTeammates ? "ON" : "OFF")}", _aimbotTargetTeammates))
            _aimbotTargetTeammates = !_aimbotTargetTeammates;
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Visibility Check: {(_aimbotVisibilityCheck ? "ON" : "OFF")}", _aimbotVisibilityCheck))
            _aimbotVisibilityCheck = !_aimbotVisibilityCheck;
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Mode: {(_aimbotToggleMode ? "TOGGLE" : "HOLD")}", _aimbotToggleMode))
            _aimbotToggleMode = !_aimbotToggleMode;
        y += 36;

        string keyName = _aimbotKeyCapturing ? "PRESS KEY..." : _aimbotKey.ToString();
        if (CustomButton(new Rect(bx, y, bw, bh), $"Bind: {keyName}", _aimbotKeyCapturing))
            _aimbotKeyCapturing = !_aimbotKeyCapturing;

        if (_aimbotKeyCapturing)
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            {
                _aimbotKey = e.keyCode;
                _aimbotKeyCapturing = false;
                e.Use();
            }
            else if (e != null && e.type == EventType.MouseDown)
            {
                if (e.button == 0) _aimbotKey = KeyCode.Mouse0;
                else if (e.button == 1) _aimbotKey = KeyCode.Mouse1;
                else if (e.button == 2) _aimbotKey = KeyCode.Mouse2;
                _aimbotKeyCapturing = false;
                e.Use();
            }
        }
    }
}
