using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace MyDynamicESP;

public class EspBehaviour : MonoBehaviour
{
    private Camera _cam;
    private Texture2D _whiteTex;
    private float _scanTimer;
    private const float ScanInterval = 1.0f;
    private float _watchdogTimer;
    private const float WatchdogInterval = 3.0f;
    private int _watchdogTicks;

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
        float now = Time.time; // Time.time кэшируется, работает быстрее
        if (_lastLogTime.TryGetValue(key, out float last) && now - last < LogCooldown) return;
        _lastLogTime[key] = now;
        if (isError) { Debug.LogError(message); Plugin.LogSource?.LogError(message); }
        else { Debug.Log(message); Plugin.LogSource?.LogInfo(message); }
    }

    void Awake()
    {
        try
        {
            Debug.Log("[ESP] ===== Awake() =====");
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
            MenuRenderer.EnsureTextures();

            enabled = true;
            gameObject.SetActive(true);

            EspModule.BoxesEnabled = Plugin.CfgEspBoxes.Value;
            EspModule.NamesEnabled = Plugin.CfgEspNames.Value;
            EspModule.DistanceEnabled = Plugin.CfgEspDistance.Value;
            AimbotModule.Enabled = Plugin.CfgAimbotEnabled.Value;
            AimbotModule.Fov = Plugin.CfgAimbotFov.Value;
            AimbotModule.Smooth = Plugin.CfgAimbotSmooth.Value;
            AimbotModule.TargetTeammates = Plugin.CfgAimbotTargetTeammates.Value;
            AimbotModule.VisibilityCheck = Plugin.CfgAimbotVisibilityCheck.Value;

            if (!KeybindState.IntroPlayed)
            {
                KeybindState.IntroPlayed = true;
                KeybindState.IntroActive = true;
                KeybindState.IntroDone = false;
                KeybindState.IntroAnimTime = 0f;
                KeybindState.MenuOpen = false;
                UiState.MenuVisible = false;
                Plugin.LogSource?.LogInfo("[ESP] Intro animation started");
            }

            Debug.Log("[ESP] ===== Awake() done =====");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] Awake EX: {e}"); }
    }

    void Start() { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }

    void OnDestroy()
    {
        try { if (_whiteTex != null) Destroy(_whiteTex); } catch { }
    }

    void Update()
    {
        try
        {
            // Кэшируем камеру один раз в Update
            if (_cam == null) _cam = Camera.main;

            // Интро-анимация
            if (KeybindState.IntroActive && !KeybindState.IntroDone)
            {
                KeybindState.IntroAnimTime += Time.deltaTime;
                if (KeybindState.IntroAnimTime >= 2.0f)
                {
                    KeybindState.IntroActive = false;
                    KeybindState.IntroDone = true;
                    KeybindState.MenuOpen = true;
                    UiState.MenuVisible = true;
                    Plugin.LogSource?.LogInfo("[ESP] Intro finished, menu opened");
                }
            }

            if (UiState.MenuVisible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            _watchdogTimer += Time.deltaTime;
            if (_watchdogTimer >= WatchdogInterval) { _watchdogTimer = 0f; RunWatchdog(); }

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval) { _scanTimer = 0f; DoScan(); }

            EspModule.ToDraw.Clear();
            foreach (var t in EspModule.TrackedDict.Values)
            {
                if (t.Transform == null) continue;
                
                // Проверка на смерть/отключение объекта (убирает трупы)
                var go = t.Transform.gameObject;
                if (go == null || !go.activeInHierarchy) continue;

                if (TeamFilter.OnlyEnemies && _localTeam > 0 && t.Team == _localTeam) continue;
                if (!EspModule.IsTeamVisible(t.Team)) continue;
                EspModule.ToDraw.Add(t);
            }
        }
        catch (Exception e) { LogThrottled("update_ex", $"[ESP] Update EX: {e}", true); _scanTimer = 0f; }
    }

    void LateUpdate()
    {
        try
        {
            // Если аимбот выключен в меню — вообще не тратим ресурсы
            if (!AimbotModule.Enabled) return;
            if (_cam == null) _cam = Camera.main;
            
            AimbotModule.Update(_cam, _localTeam, EspModule.TrackedDict);
        }
        catch (Exception e) { LogThrottled("aim_ex", $"[ESP] LateUpdate EX: {e.Message}", true); }
    }

    void OnGUI()
    {
        try
        {
            if (_whiteTex == null) { _whiteTex = new Texture2D(1, 1); _whiteTex.SetPixel(0, 0, Color.white); _whiteTex.Apply(); }
            // Убрали поиск Camera.main отсюда, чтобы не тормозило
            if (_cam == null) return; 

            EspModule.Draw(_whiteTex);
            if (AimbotModule.Enabled && AimbotModule.DrawFovCircle) MenuRenderer.DrawFovCircle();

            if (KeybindState.IntroActive && !KeybindState.IntroDone)
            {
                MenuRenderer.DrawIntroAnimation(_whiteTex, KeybindState.IntroAnimTime);
                return;
            }

            if (UiState.MenuVisible)
                MenuRenderer.DrawMenu(_whiteTex);
        }
        catch { }
        finally { GUI.color = Color.white; }
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
                Plugin.LogSource?.LogInfo($"[ESP] Watchdog #{_watchdogTicks}: tracked={EspModule.TrackedDict.Count}, localTeam={_localTeam}, aimbot={AimbotModule.Enabled}, active={AimbotState.Active}");
            }
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] Watchdog EX: {e}"); }
    }

    // --- Рефлексия ---
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
            EspModule.SeenList.Clear();
            ScanTypeInternal(_botType, true, "Bot");
            ScanTypeInternal(_playerType, false, "Player");
            var dead = new List<int>();
            foreach (var kv in EspModule.TrackedDict) if (!EspModule.SeenList.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) EspModule.TrackedDict.Remove(id);
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
                    if (EspModule.SeenList.Contains(id)) continue;
                    EspModule.SeenList.Add(id);

                    int team = ReadTeam(c, isBot);
                    string name = TryGetName(c, isBot);
                    object biped = null;
                    try { var bf = c.GetType().GetField("_characterBiped", BindingFlags.NonPublic | BindingFlags.Instance); if (bf != null) biped = bf.GetValue(c); } catch { }

                    bool isNew = !EspModule.TrackedDict.ContainsKey(id);
                    EspModule.TrackedDict[id] = new Tracked { Transform = tr, IsBot = isBot, Team = team, Name = name, BipedMap = biped };
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
}