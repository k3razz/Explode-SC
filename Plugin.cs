using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System.Collections.Generic;
using System;
using System.IO;
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

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public static ManualLogSource LogSource;
    public static ConfigFile ConfigFile;
    public static Plugin Instance;

    public static ConfigEntry<bool> CfgAimbotEnabled;
    public static ConfigEntry<float> CfgAimbotFov;
    public static ConfigEntry<float> CfgAimbotSmooth;
    public static ConfigEntry<bool> CfgAimbotTargetTeammates;
    public static ConfigEntry<bool> CfgAimbotVisibilityCheck;
    public static ConfigEntry<bool> CfgAimbotToggleMode;
    public static ConfigEntry<string> CfgAimbotKey;
    public static ConfigEntry<bool> CfgEspBoxes;
    public static ConfigEntry<bool> CfgEspNames;
    public static ConfigEntry<bool> CfgEspDistance;
    public static ConfigEntry<bool> CfgOnlyEnemies;
    public static ConfigEntry<bool> CfgShowTr;
    public static ConfigEntry<bool> CfgShowCt;
    public static ConfigEntry<bool> CfgShowSpectator;

    public static KeyCode AimbotToggleKey = KeyCode.Mouse1;
    public static bool AimbotToggleMode = false;
    public static bool CapturingKey = false;
    public static KeyCode CapturingBackup = KeyCode.None;

    public override void Load()
    {
        Instance = this;
        LogSource = Log;
        Log.LogInfo("[ESP] ===== Plugin.Load() =====");
        try
        {
            string cfgPath = Path.Combine(Paths.ConfigPath, "explode.cfg");
            ConfigFile = new ConfigFile(cfgPath, true);
            Log.LogInfo($"[ESP] Config: {cfgPath}");

            CfgAimbotEnabled = ConfigFile.Bind("Aimbot", "Enabled", false);
            CfgAimbotFov = ConfigFile.Bind("Aimbot", "Fov", 15f);
            CfgAimbotSmooth = ConfigFile.Bind("Aimbot", "Smooth", 15f);
            CfgAimbotTargetTeammates = ConfigFile.Bind("Aimbot", "TargetTeammates", false);
            CfgAimbotVisibilityCheck = ConfigFile.Bind("Aimbot", "VisibilityCheck", false);
            CfgAimbotToggleMode = ConfigFile.Bind("Aimbot", "ToggleMode", false);
            CfgAimbotKey = ConfigFile.Bind("Aimbot", "Key", "Mouse1");

            CfgEspBoxes = ConfigFile.Bind("ESP", "Boxes", true);
            CfgEspNames = ConfigFile.Bind("ESP", "Names", true);
            CfgEspDistance = ConfigFile.Bind("ESP", "Distance", true);
            CfgOnlyEnemies = ConfigFile.Bind("ESP", "OnlyEnemies", false);
            CfgShowTr = ConfigFile.Bind("ESP", "ShowTr", true);
            CfgShowCt = ConfigFile.Bind("ESP", "ShowCt", true);
            CfgShowSpectator = ConfigFile.Bind("ESP", "ShowSpectator", false);

            TeamFilter.ShowTr = CfgShowTr.Value;
            TeamFilter.ShowCt = CfgShowCt.Value;
            TeamFilter.ShowSpectator = CfgShowSpectator.Value;
            TeamFilter.OnlyEnemies = CfgOnlyEnemies.Value;

            AimbotToggleMode = CfgAimbotToggleMode.Value;
            if (Enum.TryParse(CfgAimbotKey.Value, out KeyCode kc))
                AimbotToggleKey = kc;

            Log.LogInfo($"[ESP] Aimbot key: {AimbotToggleKey}, mode: {AimbotToggleMode}");

            AddComponent<EspBehaviour>();
            AddComponent<KeybindListener>();

            Log.LogInfo("[ESP] Components added via AddComponent<T>()");
        }
        catch (Exception e) { Log.LogError($"[ESP] Load EX: {e}"); }
    }
}

// =========================================================
//                    KEYBIND LISTENER
// =========================================================
public class KeybindListener : MonoBehaviour
{
    private float _lastLog;
    private bool _waitingForRelease = false;

    void Awake() { Plugin.LogSource?.LogInfo("[KB] Awake"); }
    void OnDestroy() { Plugin.LogSource?.LogInfo("[KB] OnDestroy"); }

    void Update()
    {
        try
        {
            if (Time.realtimeSinceStartup - _lastLog > 3f)
            {
                _lastLog = Time.realtimeSinceStartup;
                Plugin.LogSource?.LogInfo($"[KB] Tick key={Plugin.AimbotToggleKey} capture={Plugin.CapturingKey}");
            }

            if (Plugin.CapturingKey) { HandleCapture(); return; }
            if (IsTypingInChat()) return;
            if (Plugin.AimbotToggleKey == KeyCode.None) return;

            bool kd = false, kdDown = false;
            try
            {
                if (Plugin.AimbotToggleKey == KeyCode.Mouse0) { kd = Input.GetMouseButton(0); kdDown = Input.GetMouseButtonDown(0); }
                else if (Plugin.AimbotToggleKey == KeyCode.Mouse1) { kd = Input.GetMouseButton(1); kdDown = Input.GetMouseButtonDown(1); }
                else if (Plugin.AimbotToggleKey == KeyCode.Mouse2) { kd = Input.GetMouseButton(2); kdDown = Input.GetMouseButtonDown(2); }
                else { kd = Input.GetKey(Plugin.AimbotToggleKey); kdDown = Input.GetKeyDown(Plugin.AimbotToggleKey); }
            }
            catch (Exception e) { Plugin.LogSource?.LogError($"[KB] Input EX: {e.Message}"); }

            if (Plugin.AimbotToggleMode)
            {
                if (kdDown)
                {
                    EspBehaviour.SetAimbotActive(!EspBehaviour.AimbotActive);
                    Plugin.LogSource?.LogInfo($"[ESP] Aimbot toggled: {EspBehaviour.AimbotActive}");
                }
            }
            else
            {
                EspBehaviour.SetAimbotActive(kd);
                if (kd) Plugin.LogSource?.LogInfo($"[KB] Hold active");
            }
        }
        catch (Exception e) { Debug.LogError($"[KB] EX: {e.Message}"); }
    }

    private void HandleCapture()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Plugin.AimbotToggleKey = Plugin.CapturingBackup;
            Plugin.CapturingKey = false;
            Plugin.LogSource?.LogInfo($"[KB] Cancelled, restored {Plugin.AimbotToggleKey}");
            return;
        }

        if (_waitingForRelease)
        {
            bool anyDown = false;
            for (int b = 0; b < 3; b++) if (Input.GetMouseButton(b)) { anyDown = true; break; }
            if (!anyDown) _waitingForRelease = false;
            return;
        }

        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None || kc == KeyCode.Escape) continue;
            if (Input.GetKeyDown(kc))
            {
                Plugin.AimbotToggleKey = kc;
                Plugin.CapturingKey = false;
                _waitingForRelease = true;
                if (Plugin.CfgAimbotKey != null) Plugin.CfgAimbotKey.Value = kc.ToString();
                Plugin.LogSource?.LogInfo($"[KB] Set {kc}");
                return;
            }
        }

        for (int b = 0; b < 3; b++)
        {
            if (Input.GetMouseButtonDown(b))
            {
                KeyCode kc = b == 0 ? KeyCode.Mouse0 : (b == 1 ? KeyCode.Mouse1 : KeyCode.Mouse2);
                Plugin.AimbotToggleKey = kc;
                Plugin.CapturingKey = false;
                _waitingForRelease = true;
                if (Plugin.CfgAimbotKey != null) Plugin.CfgAimbotKey.Value = kc.ToString();
                Plugin.LogSource?.LogInfo($"[KB] Set {kc}");
                return;
            }
        }
    }

    private bool IsTypingInChat()
    {
        try
        {
            Type hudType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { foreach (var t in asm.GetTypes()) if (t.Name == "HudManager") { hudType = t; break; } } catch { }
                if (hudType != null) break;
            }
            if (hudType == null) return false;
            var instProp = hudType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instProp == null) return false;
            var hud = instProp.GetValue(null);
            if (hud == null) return false;
            var chatProp = hudType.GetProperty("Chat", BindingFlags.Public | BindingFlags.Instance);
            if (chatProp == null) return false;
            var chat = chatProp.GetValue(hud);
            if (chat == null) return false;
            var openProp = chat.GetType().GetProperty("IsOpenOrOpening", BindingFlags.Public | BindingFlags.Instance);
            if (openProp == null) return false;
            var open = openProp.GetValue(chat);
            return open is bool o && o;
        }
        catch { return false; }
    }
}

// =========================================================
//                    ESP BEHAVIOUR
// =========================================================
public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public int Team;
        public string Name;
        public object BipedMap;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<Tracked> _toDraw = new();
    private readonly List<int> _seenList = new();

    private Camera _cam;
    private Texture2D _whiteTex;
    private static Texture2D _circleTex;
    private static Texture2D _ringTex;
    private float _scanTimer;
    private const float ScanInterval = 1.0f;

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
    private int _menuTab = 0;

    private const float MenuWidth = 500f;
    private const float MenuHeight = 540f;
    private const float MenuOffset = 30f;
    private const float TabWidth = 120f;

    private GameObject _btnCanvas;
    private RectTransform _btnRect;
    private Vector2 _btnPos = new Vector2(-1f, -1f);
    private bool _btnInitialized = false;
    private bool _btnMoved = false;
    private Vector2 _btnDragOffset;
    private Vector2 _btnDragStart;

    private bool _aimbotEnabled = false;
    private float _aimbotFov = 15f;
    private float _aimbotSmooth = 15f;
    private bool _aimbotTargetTeammates = false;
    private bool _aimbotVisibilityCheck = false;

    private static bool _aimbotActive = false;
    public static bool AimbotActive => _aimbotActive;
    public static void SetAimbotActive(bool active) { _aimbotActive = active; }

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private static MemberInfo _playerTeamMember;
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

            GetCircleTex();
            GetRingTex();

            enabled = true;
            gameObject.SetActive(true);

            BuildButtonCanvas();

            if (Plugin.CfgEspBoxes != null) _espBoxesEnabled = Plugin.CfgEspBoxes.Value;
            if (Plugin.CfgEspNames != null) _espNamesEnabled = Plugin.CfgEspNames.Value;
            if (Plugin.CfgEspDistance != null) _espDistanceEnabled = Plugin.CfgEspDistance.Value;
            if (Plugin.CfgAimbotEnabled != null) _aimbotEnabled = Plugin.CfgAimbotEnabled.Value;
            if (Plugin.CfgAimbotFov != null) _aimbotFov = Plugin.CfgAimbotFov.Value;
            if (Plugin.CfgAimbotSmooth != null) _aimbotSmooth = Plugin.CfgAimbotSmooth.Value;
            if (Plugin.CfgAimbotTargetTeammates != null) _aimbotTargetTeammates = Plugin.CfgAimbotTargetTeammates.Value;
            if (Plugin.CfgAimbotVisibilityCheck != null) _aimbotVisibilityCheck = Plugin.CfgAimbotVisibilityCheck.Value;
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] Awake EX: {e}"); }
    }

    private static Texture2D GetCircleTex()
    {
        if (_circleTex != null) return _circleTex;
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float center = size / 2f;
        float radius = size / 2f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f, dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01((radius - dist) / 1.5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        _circleTex = tex;
        return tex;
    }

    private static Texture2D GetRingTex()
    {
        if (_ringTex != null) return _ringTex;
        int size = 128;
        float thickness = 2f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float center = size / 2f;
        float outer = size / 2f - 1f;
        float inner = outer - thickness;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f, dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (dist <= outer && dist >= inner)
                    a = Mathf.Clamp01(Mathf.Min(outer - dist, dist - inner) / 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        _ringTex = tex;
        return tex;
    }

    private void BuildButtonCanvas()
    {
        try
        {
            _btnCanvas = new GameObject("ESP_BtnCanvas");
            DontDestroyOnLoad(_btnCanvas);
            var canvas = _btnCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9998;
            var scaler = _btnCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _btnCanvas.AddComponent<GraphicRaycaster>();

            var btnGo = new GameObject("ESP_E_Button");
            btnGo.transform.SetParent(_btnCanvas.transform, false);

            var img = btnGo.AddComponent<Image>();
            var btnTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            btnTex.filterMode = FilterMode.Point;
            btnTex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[32 * 32];
            float center = 16f;
            float radius = 15f;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - center + 0.5f, dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = dist <= radius ? (byte)255 : (byte)0;
                    pixels[y * 32 + x] = new Color32(40, 40, 60, a);
                }
            btnTex.SetPixels32(pixels);
            btnTex.Apply();
            img.sprite = Sprite.Create(btnTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            img.raycastTarget = true;

            var txtGo = new GameObject("E_Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 22;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = "E";
            var txtRect = txtGo.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            _btnRect = btnGo.GetComponent<RectTransform>();
            _btnRect.anchorMin = new Vector2(0, 0);
            _btnRect.anchorMax = new Vector2(0, 0);
            _btnRect.pivot = new Vector2(0.5f, 0.5f);
            _btnRect.sizeDelta = new Vector2(44, 44);

            if (!_btnInitialized)
            {
                _btnPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
                _btnInitialized = true;
            }
            _btnRect.anchoredPosition = _btnPos;

            var trigger = btnGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var downEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            downEntry.callback.AddListener((Action<UnityEngine.EventSystems.BaseEventData>)(data =>
            {
                try
                {
                    var ped = data.TryCast<UnityEngine.EventSystems.PointerEventData>();
                    Vector2 mouse = ped.position;
                    _btnMoved = false;
                    _btnDragOffset = mouse - _btnPos;
                    _btnDragStart = mouse;
                }
                catch { }
            }));
            trigger.triggers.Add(downEntry);

            var dragEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
            dragEntry.callback.AddListener((Action<UnityEngine.EventSystems.BaseEventData>)(data =>
            {
                try
                {
                    var ped = data.TryCast<UnityEngine.EventSystems.PointerEventData>();
                    Vector2 mouse = ped.position;
                    if ((mouse - _btnDragStart).sqrMagnitude > 9f) _btnMoved = true;
                    _btnPos = mouse - _btnDragOffset;
                    _btnPos.x = Mathf.Clamp(_btnPos.x, 22f, Screen.width - 22f);
                    _btnPos.y = Mathf.Clamp(_btnPos.y, 22f, Screen.height - 22f);
                    _btnRect.anchoredPosition = _btnPos;
                }
                catch { }
            }));
            trigger.triggers.Add(dragEntry);

            var upEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            upEntry.callback.AddListener((Action<UnityEngine.EventSystems.BaseEventData>)(data =>
            {
                try
                {
                    if (!_btnMoved) _menuVisible = true;
                }
                catch { }
            }));
            trigger.triggers.Add(upEntry);

            _btnCanvas.SetActive(true);
            Plugin.LogSource?.LogInfo("[ESP] Button canvas created");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] BuildButtonCanvas EX: {e}"); }
    }

    void Start() { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }

    void OnDestroy()
    {
        try { if (_whiteTex != null) Destroy(_whiteTex); } catch { }
    }

    void RunWatchdog()
    {
        _watchdogTicks++;
        try
        {
            if (_botType == null && _playerType == null) { _resolved = false; ResolveTypes(); }
            if (_cam == null) _cam = Camera.main;
            if (_watchdogTicks % 6 == 0)
            {
                UpdateLocalTeam();
                Plugin.LogSource?.LogInfo($"[ESP] Watchdog #{_watchdogTicks}: tracked={_tracked.Count}, localTeam={_localTeam}, aimbot={_aimbotEnabled}, active={AimbotActive}");
            }
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] Watchdog EX: {e}"); }
    }

    int ReadTeamFromMember(MemberInfo member, object target)
    {
        if (member == null || target == null) return -1;
        try
        {
            object val = null;
            if (member is MethodInfo m) val = m.Invoke(target, null);
            else if (member is PropertyInfo p) val = p.GetValue(target);
            else if (member is FieldInfo f) val = f.GetValue(target);
            if (val != null) return Convert.ToInt32(val is Enum en ? (object)en : val);
        }
        catch { }
        return -1;
    }

    int ReadByteAtOffset(object obj, int offset)
    {
        try
        {
            IntPtr ptr = IntPtr.Zero;
            if (obj is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj) ptr = il2cppObj.Pointer;
            else if (obj is Component comp) ptr = comp.Pointer;
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
                var p = t.GetProperty("crvs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null) playerCtrl = p.GetValue(component);
                if (playerCtrl == null)
                {
                    var f = t.GetField("crvs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) playerCtrl = f.GetValue(component);
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
        return ReadTeamFromMember(_playerTeamMember, component);
    }

    void UpdateLocalTeam()
    {
        try
        {
            if (_playerManagerType == null) return;
            object pmInstance = null;
            var instProp = _playerManagerType.GetProperty("Instance",
                (Il2CppSystem.Reflection.BindingFlags)(int)(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            if (instProp != null) pmInstance = instProp.GetValue(null);

            if (pmInstance == null)
            {
                foreach (var n in new[] { "_instance", "s_Instance", "m_Instance", "instance" })
                {
                    var f = _playerManagerType.GetField(n,
                        (Il2CppSystem.Reflection.BindingFlags)(int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
                    if (f != null) { pmInstance = f.GetValue(null); break; }
                    var p = _playerManagerType.GetProperty(n,
                        (Il2CppSystem.Reflection.BindingFlags)(int)(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
                    if (p != null) { pmInstance = p.GetValue(null); break; }
                }
            }

            if (pmInstance == null)
            {
                var pmc = FindObjectOfType(_playerManagerType);
                if (pmc != null) pmInstance = pmc;
            }

            if (pmInstance == null) { LogThrottled("pm_null", "[ESP] PlayerManager not found", true); return; }

            object localPlayer = null;
            var pmManagedType = pmInstance.GetType();
            var cfogProp = pmManagedType.GetProperty("cfog", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (cfogProp != null) localPlayer = cfogProp.GetValue(pmInstance);
            if (localPlayer == null)
            {
                var cfogField = pmManagedType.GetField("_cfog_k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cfogField != null) localPlayer = cfogField.GetValue(pmInstance);
            }
            if (localPlayer == null) { LogThrottled("cfog_null", "[ESP] localPlayer is null", true); return; }

            int newTeam = ReadTeamFromMember(_playerTeamMember, localPlayer);
            if (newTeam != _localTeam && newTeam >= 0)
            {
                Plugin.LogSource?.LogInfo($"[ESP] Local team: {_localTeam} -> {newTeam}");
                _localTeam = newTeam;
            }
        }
        catch (Exception e) { LogThrottled("localt_ex", $"[ESP] LocalTeam EX: {e.Message}", true); }
    }

    void Update()
    {
        try
        {
            if (_btnCanvas != null)
            {
                bool showBtn = !_menuVisible;
                if (_btnCanvas.activeSelf != showBtn) _btnCanvas.SetActive(showBtn);
            }

            if (_menuVisible) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }

            _watchdogTimer += Time.deltaTime;
            if (_watchdogTimer >= WatchdogInterval) { _watchdogTimer = 0f; RunWatchdog(); }

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval) { _scanTimer = 0f; DoScan(); }

            _toDraw.Clear();
            foreach (var t in _tracked.Values)
            {
                if (t.Transform == null) continue;
                if (TeamFilter.OnlyEnemies && _localTeam > 0 && t.Team == _localTeam) continue;
                if (!IsTeamVisible(t.Team)) continue;
                _toDraw.Add(t);
            }
        }
        catch (Exception e) { LogThrottled("update_ex", $"[ESP] Update EX: {e}", true); _scanTimer = 0f; }
    }

    void LateUpdate()
    {
        try
        {
            if (!_aimbotEnabled || !AimbotActive) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Tracked best = null;
            float bestAngle = _aimbotFov;
            Vector3 camPos, camForward;
            try { camPos = _cam.transform.position; camForward = _cam.transform.forward; } catch { return; }

            foreach (var t in _tracked.Values)
            {
                try
                {
                    if (t.Transform == null) continue;
                    if (!_aimbotTargetTeammates && _localTeam > 0 && t.Team == _localTeam) continue;
                    if (_aimbotVisibilityCheck && !IsVisible(t, camPos)) continue;

                    Vector3 hp = GetHeadPosition(t);
                    Vector3 dir = (hp - camPos).normalized;
                    float angle = Vector3.Angle(camForward, dir);
                    if (angle < bestAngle) { bestAngle = angle; best = t; }
                }
                catch { }
            }

            if (best == null) return;

            Vector3 targetHead = GetHeadPosition(best);
            Quaternion targetRot = Quaternion.LookRotation((targetHead - camPos).normalized);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, targetRot, _aimbotSmooth * Time.deltaTime);
        }
        catch (Exception e) { LogThrottled("aim_ex", $"[ESP] LateUpdate EX: {e.Message}", true); }
    }

    bool IsVisible(Tracked t, Vector3 camPos)
    {
        try
        {
            Vector3 hp = GetHeadPosition(t);
            Vector3 d = hp - camPos;
            float dist = d.magnitude;
            if (dist < 0.1f) return true;
            if (Physics.Raycast(camPos, d.normalized, out var hit, dist))
                return hit.transform == t.Transform || hit.transform.IsChildOf(t.Transform);
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
                var nwh = t.BipedMap.GetType().GetMethod("nwh", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nwh != null)
                {
                    var h = nwh.Invoke(t.BipedMap, new object[] { 0 });
                    if (h is Transform ht) return ht.position;
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

    private static bool IsTeamEnum(Type t) => t != null && t.IsEnum && (t.Name == "Team" || t.FullName.EndsWith(".Team"));

    private static Type FindTypeByName(string shortName, out string full)
    {
        full = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var d = asm.GetType(shortName); if (d != null) { full = d.FullName; return d; } } catch { }
            try { foreach (var t in asm.GetTypes()) if (t.Name == shortName) { full = t.FullName; return t; } } catch { }
        }
        return null;
    }

    private static MemberInfo FindTeamMember(Type start, out string owner)
    {
        owner = null;
        if (start == null) return null;
        Type s = start; int depth = 0;
        while (s != null && s != typeof(object) && depth < 10)
        {
            foreach (var m in s.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (m.GetParameters().Length == 0 && IsTeamEnum(m.ReturnType)) { owner = $"{s.Name}.{m.Name}()"; return m; }
            foreach (var p in s.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (IsTeamEnum(p.PropertyType) && p.GetGetMethod(true) != null) { owner = $"{s.Name}.{p.Name}"; return p; }
            foreach (var f in s.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (IsTeamEnum(f.FieldType)) { owner = $"{s.Name}.{f.Name}"; return f; }
            s = s.BaseType; depth++;
        }
        return null;
    }

    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            var bt = FindTypeByName("BotPlayerController", out _);
            var pt = FindTypeByName("PlayerController", out _);
            var pmType = FindTypeByName("PlayerManager", out _);
            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);
            if (pmType != null) _playerManagerType = Il2CppType.From(pmType);

            _playerTeamMember = null;
            string owner = null;
            _playerTeamMember = FindTeamMember(pt, out owner);
            Plugin.LogSource?.LogInfo($"[ESP] Team member: {owner ?? "null"}");
            Plugin.LogSource?.LogInfo($"[ESP] Resolve: Bot={_botType != null}, Player={_playerType != null}, PM={_playerManagerType != null}, Team={_playerTeamMember != null}");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] ResolveTypes EX: {e}"); }
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
            foreach (var kv in _tracked) if (!_seenList.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) _tracked.Remove(id);
        }
        catch (Exception e) { LogThrottled("scan_ex", $"[ESP] DoScan EX: {e}", true); }
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

                    int team = ReadTeam(c, isBot);
                    string name = TryGetName(c, isBot);
                    object biped = null;
                    try { var bf = c.GetType().GetField("_characterBiped", BindingFlags.NonPublic | BindingFlags.Instance); if (bf != null) biped = bf.GetValue(c); } catch { }

                    bool isNew = !_tracked.ContainsKey(id);
                    _tracked[id] = new Tracked { Transform = tr, IsBot = isBot, Team = team, Name = name, BipedMap = biped };
                    if (isNew) Plugin.LogSource?.LogInfo($"[ESP] +{label}: '{name}' team={team}");
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
            foreach (var p in c.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(c) as string;
                    if (!string.IsNullOrEmpty(v) && v.Length < 25 && !v.Contains("Clone") && !v.Contains("Player") && !v.Contains("Bot")) return v;
                }
        }
        catch { }
        try { var n = c.gameObject.name; if (!string.IsNullOrEmpty(n) && n != "Player(Clone)" && n != "Bot" && n != "Player") return n; } catch { }
        return isBot ? "Bot" : "Player";
    }

    void OnGUI()
    {
        try
        {
            if (_whiteTex == null) { _whiteTex = new Texture2D(1, 1); _whiteTex.SetPixel(0, 0, Color.white); _whiteTex.Apply(); }
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            if (_espBoxesEnabled || _espNamesEnabled)
                foreach (var t in _toDraw)
                {
                    try
                    {
                        if (t.Transform == null) continue;
                        Vector3 hw = t.Transform.position + Vector3.up * HeadOffset;
                        Vector3 fw = t.Transform.position - Vector3.up * FootOffset;
                        Vector3 sh = _cam.WorldToScreenPoint(hw);
                        Vector3 sf = _cam.WorldToScreenPoint(fw);
                        if (sh.z < 0 || sf.z < 0) continue;

                        float x = sh.x;
                        float top = Screen.height - sh.y;
                        float bottom = Screen.height - sf.y;
                        float height = Mathf.Abs(bottom - top);
                        float width = height * BoxWidthRatio;
                        if (width < 4f) width = 4f;
                        if (height < 8f) height = 8f;
                        float left = x - width / 2f;
                        float dist = Vector3.Distance(_cam.transform.position, t.Transform.position);

                        if (_espBoxesEnabled)
                        {
                            float th = 2f;
                            Color fill = t.Team == 1 ? new Color(1f, 0f, 0f, 0.12f) : new Color(0f, 0.5f, 1f, 0.12f);
                            Color outl = t.Team == 1 ? Color.red : Color.cyan;
                            GUI.color = fill;
                            GUI.DrawTexture(new Rect(left, top, width, height), _whiteTex);
                            GUI.color = outl;
                            GUI.DrawTexture(new Rect(left, top, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top + height - th, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top, th, height), _whiteTex);
                            GUI.DrawTexture(new Rect(left + width - th, top, th, height), _whiteTex);
                            GUI.color = Color.white;
                        }

                        if (_espNamesEnabled)
                        {
                            string lbl = t.Name + (_espDistanceEnabled ? $" [{dist:F0}m]" : "");
                            float lw = Math.Min(lbl.Length * 7.5f + 10f, 280f);
                            float ll = x - lw / 2f;
                            GUI.color = new Color(0f, 0f, 0f, 0.65f);
                            GUI.DrawTexture(new Rect(ll - 2f, top - 22f, lw + 4f, 20f), _whiteTex);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(ll, top - 21f, lw, 18f), lbl);
                        }
                    }
                    catch { }
                }

            if (_aimbotEnabled)
            {
                var circ = GetCircleTex();
                var ring = GetRingTex();
                float fovScreen = _aimbotFov * (Screen.height / 90f);
                if (fovScreen > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.04f);
                    GUI.DrawTexture(new Rect(Screen.width / 2f - fovScreen / 2f, Screen.height / 2f - fovScreen / 2f, fovScreen, fovScreen), circ);
                    GUI.color = new Color(1f, 1f, 1f, 0.7f);
                    GUI.DrawTexture(new Rect(Screen.width / 2f - fovScreen / 2f, Screen.height / 2f - fovScreen / 2f, fovScreen, fovScreen), ring);
                    GUI.color = Color.white;
                }
            }

            if (_menuVisible) DrawMenu();
        }
        catch { }
        finally { GUI.color = Color.white; }
    }

    private bool CustomButton(Rect rect, string label, bool state)
    {
        Event e = Event.current;
        bool clicked = e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        GUI.color = state ? new Color(0.2f, 0.55f, 0.3f, 0.95f) : new Color(0.55f, 0.2f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, _whiteTex);
        GUI.color = Color.white;
        float lw = Math.Min(label.Length * 7.5f + 4f, rect.width - 6f);
        GUI.Label(new Rect(rect.x + (rect.width - lw) / 2f, rect.y + (rect.height - 16f) / 2f, lw, 16f), label);
        return clicked;
    }

    private bool TabButton(Rect rect, string label, bool active)
    {
        Event e = Event.current;
        bool clicked = e != null && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        GUI.color = active ? new Color(0.2f, 0.4f, 0.7f, 0.95f) : new Color(0.15f, 0.15f, 0.2f, 0.95f);
        GUI.DrawTexture(rect, _whiteTex);
        GUI.color = Color.white;
        float lw = Math.Min(label.Length * 7.5f + 4f, rect.width - 6f);
        GUI.Label(new Rect(rect.x + (rect.width - lw) / 2f, rect.y + (rect.height - 16f) / 2f, lw, 16f), label);
        return clicked;
    }

    private Rect GetMenuRect()
    {
        float sw = Screen.width, sh = Screen.height, mw = MenuWidth, mh = MenuHeight;
        float x = _btnPos.x + 22f + MenuOffset;
        if (x + mw > sw) x = _btnPos.x - 22f - MenuOffset - mw;
        if (x < 0) x = Mathf.Max(0, (sw - mw) / 2f);
        float y = _btnPos.y - mh / 2f;
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

        float bx = panelRect.x + 10, by = panelRect.y + 10;
        float contentX = bx + TabWidth + 10f;
        float contentW = panelRect.width - (TabWidth + 30f);

        GUI.Label(new Rect(bx, by, panelRect.width - 20f, 24f), "<b>DynamicESP</b>");
        by += 30;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "ESP", _menuTab == 0)) _menuTab = 0;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "AimBot", _menuTab == 1)) _menuTab = 1;
        by += 34;
        if (TabButton(new Rect(bx, by, TabWidth, 30f), "Hide", false)) _menuVisible = false;

        float cy = panelRect.y + 40;
        if (_menuTab == 0) DrawEspTab(contentX, cy, contentW);
        else DrawAimTab(contentX, cy, contentW);
    }

    private void DrawEspTab(float bx, float y, float bw)
    {
        float bh = 30;
        if (CustomButton(new Rect(bx, y, bw, bh), $"ESP Boxes: {(_espBoxesEnabled ? "ON" : "OFF")}", _espBoxesEnabled))
        { _espBoxesEnabled = !_espBoxesEnabled; if (Plugin.CfgEspBoxes != null) Plugin.CfgEspBoxes.Value = _espBoxesEnabled; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Names: {(_espNamesEnabled ? "ON" : "OFF")}", _espNamesEnabled))
        { _espNamesEnabled = !_espNamesEnabled; if (Plugin.CfgEspNames != null) Plugin.CfgEspNames.Value = _espNamesEnabled; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Distance: {(_espDistanceEnabled ? "ON" : "OFF")}", _espDistanceEnabled))
        { _espDistanceEnabled = !_espDistanceEnabled; if (Plugin.CfgEspDistance != null) Plugin.CfgEspDistance.Value = _espDistanceEnabled; }
        y += 46;

        GUI.Label(new Rect(bx, y, bw, 22), "<b>Team filter:</b>");
        y += 26;
        if (CustomButton(new Rect(bx, y, bw, bh), $"TR: {(TeamFilter.ShowTr ? "ON" : "OFF")}", TeamFilter.ShowTr))
        { TeamFilter.ShowTr = !TeamFilter.ShowTr; if (Plugin.CfgShowTr != null) Plugin.CfgShowTr.Value = TeamFilter.ShowTr; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"CT: {(TeamFilter.ShowCt ? "ON" : "OFF")}", TeamFilter.ShowCt))
        { TeamFilter.ShowCt = !TeamFilter.ShowCt; if (Plugin.CfgShowCt != null) Plugin.CfgShowCt.Value = TeamFilter.ShowCt; }
        y += 36;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Spec: {(TeamFilter.ShowSpectator ? "ON" : "OFF")}", TeamFilter.ShowSpectator))
        { TeamFilter.ShowSpectator = !TeamFilter.ShowSpectator; if (Plugin.CfgShowSpectator != null) Plugin.CfgShowSpectator.Value = TeamFilter.ShowSpectator; }
        y += 46;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Only enemies: {(TeamFilter.OnlyEnemies ? "ON" : "OFF")}", TeamFilter.OnlyEnemies))
        { TeamFilter.OnlyEnemies = !TeamFilter.OnlyEnemies; if (Plugin.CfgOnlyEnemies != null) Plugin.CfgOnlyEnemies.Value = TeamFilter.OnlyEnemies; }
    }

    private void DrawAimTab(float bx, float y, float bw)
    {
        float bh = 30;
        if (CustomButton(new Rect(bx, y, bw, bh), $"Aimbot: {(_aimbotEnabled ? "ON" : "OFF")}", _aimbotEnabled))
        { _aimbotEnabled = !_aimbotEnabled; if (Plugin.CfgAimbotEnabled != null) Plugin.CfgAimbotEnabled.Value = _aimbotEnabled; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>FOV:</b> {_aimbotFov:F0}°");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false))
        { _aimbotFov = Mathf.Max(1f, _aimbotFov - 2f); if (Plugin.CfgAimbotFov != null) Plugin.CfgAimbotFov.Value = _aimbotFov; }
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true))
        { _aimbotFov = Mathf.Min(90f, _aimbotFov + 2f); if (Plugin.CfgAimbotFov != null) Plugin.CfgAimbotFov.Value = _aimbotFov; }
        y += 36;

        GUI.Label(new Rect(bx, y, bw, 22), $"<b>Smooth:</b> {_aimbotSmooth:F0}");
        y += 24;
        if (CustomButton(new Rect(bx, y, bw / 2f - 2f, bh), "-", false))
        { _aimbotSmooth = Mathf.Max(1f, _aimbotSmooth - 5f); if (Plugin.CfgAimbotSmooth != null) Plugin.CfgAimbotSmooth.Value = _aimbotSmooth; }
        if (CustomButton(new Rect(bx + bw / 2f + 2f, y, bw / 2f - 2f, bh), "+", true))
        { _aimbotSmooth = Mathf.Min(50f, _aimbotSmooth + 5f); if (Plugin.CfgAimbotSmooth != null) Plugin.CfgAimbotSmooth.Value = _aimbotSmooth; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Aim Teammates: {(_aimbotTargetTeammates ? "ON" : "OFF")}", _aimbotTargetTeammates))
        { _aimbotTargetTeammates = !_aimbotTargetTeammates; if (Plugin.CfgAimbotTargetTeammates != null) Plugin.CfgAimbotTargetTeammates.Value = _aimbotTargetTeammates; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Visibility: {(_aimbotVisibilityCheck ? "ON" : "OFF")}", _aimbotVisibilityCheck))
        { _aimbotVisibilityCheck = !_aimbotVisibilityCheck; if (Plugin.CfgAimbotVisibilityCheck != null) Plugin.CfgAimbotVisibilityCheck.Value = _aimbotVisibilityCheck; }
        y += 36;

        if (CustomButton(new Rect(bx, y, bw, bh), $"Mode: {(Plugin.AimbotToggleMode ? "TOGGLE" : "HOLD")}", Plugin.AimbotToggleMode))
        {
            Plugin.AimbotToggleMode = !Plugin.AimbotToggleMode;
            if (Plugin.CfgAimbotToggleMode != null) Plugin.CfgAimbotToggleMode.Value = Plugin.AimbotToggleMode;
        }
        y += 36;

        string keyName = Plugin.CapturingKey ? "PRESS KEY... (Esc)" : Plugin.AimbotToggleKey.ToString();
        if (CustomButton(new Rect(bx, y, bw, bh), $"Bind: {keyName}", Plugin.CapturingKey))
        {
            if (!Plugin.CapturingKey)
            {
                Plugin.CapturingBackup = Plugin.AimbotToggleKey;
                Plugin.CapturingKey = true;
            }
            else
            {
                Plugin.AimbotToggleKey = Plugin.CapturingBackup;
                Plugin.CapturingKey = false;
            }
        }
    }
}
