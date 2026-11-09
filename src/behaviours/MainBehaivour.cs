using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public class EspBehaviour : MonoBehaviour
{
    public static EspBehaviour Instance { get; private set; }

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

    private Component _cachedPm;
    private float _pmRetryTimer;
    private const float PmRetryInterval = 2.0f;

    private Transform _mainCameraHolder;
    private float _holderCheckTimer;
    private const float HolderCheckInterval = 2.0f;

    private const int OFF_PM_CFOG = 0x40;
    private const int OFF_BOT_TEAM = 0x24;
    private const int OFF_PC_CFMP = 0x159;
    private const int OFF_PC_MAINCAMHOLDER = 0x20;

    private readonly Dictionary<string, float> _lastLogTime = new();
    private const float LogCooldown = 1.5f;

    private void LogThrottled(string key, string message, bool isError = false)
    {
        float now = Time.time;
        if (_lastLogTime.TryGetValue(key, out float last) && now - last < LogCooldown) return;
        _lastLogTime[key] = now;
        if (isError) { Debug.LogError(message); Plugin.LogSource?.LogError(message); }
        else { Debug.Log(message); Plugin.LogSource?.LogInfo(message); }
    }

    void Awake()
    {
        try
        {
            Instance = this;
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
            AimbotModule.DrawFovCircle = Plugin.CfgAimbotDrawFov.Value;
            AimbotModule.Sensitivity = Plugin.CfgAimbotSensitivity.Value;
            AimbotModule.AimOnTeammates = Plugin.CfgAimbotAimOnTeammates.Value;
            if (Enum.TryParse(Plugin.CfgAimbotBone.Value, out AimBone bone)) AimbotModule.Bone = bone;
            else AimbotModule.Bone = AimBone.Head;

            // ─── Weapon (раскомментируй, если конфиги есть в Plugin.cs) ───
            WeaponCheats.NoSpread = Plugin.CfgWeaponNoSpread.Value;
            WeaponCheats.NoRecoil = Plugin.CfgWeaponNoRecoil.Value;
            WeaponCheats.RapidFire = Plugin.CfgWeaponRapidFire.Value;
            WeaponCheats.MaxWallbang = Plugin.CfgWeaponMaxWallbang.Value;
            WeaponCheats.MaxArmorPent = Plugin.CfgWeaponMaxArmorPent.Value;

            KeybindState.ToggleMode = Plugin.CfgAimbotToggleMode.Value;
            if (Enum.TryParse(Plugin.CfgAimbotKey.Value, out KeyCode kc)) KeybindState.AimbotKey = kc;
            else KeybindState.AimbotKey = KeyCode.Mouse0;

            WatermarkModule.Enabled = Plugin.CfgWatermarkEnabled.Value;
            WatermarkModule.Position = new Vector2(Plugin.CfgWatermarkX.Value, Plugin.CfgWatermarkY.Value);

            BindListState.Enabled = Plugin.CfgShowBindList.Value;
            BindListState.Position = new Vector2(Plugin.CfgBindListX.Value, Plugin.CfgBindListY.Value);
            BombStatusRenderer.Enabled = Plugin.CfgShowBombStatus.Value;
            BombStatusRenderer.Position = new Vector2(Plugin.CfgBombStatusX.Value, Plugin.CfgBombStatusY.Value);

            UiState.MenuScale = Mathf.Clamp(Plugin.CfgMenuScale.Value, MenuRenderer.MinScale, MenuRenderer.MaxScale);
            UiState.HudWatermark = Plugin.CfgHudWatermark.Value;

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

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void OnDestroy()
    {
        try { if (_whiteTex != null) Destroy(_whiteTex); } catch { }
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        try
        {
            if (_cam == null) _cam = Camera.main;

            WatermarkModule.Update();
            BombEspModule.Update();
            WeaponCheats.Update();
            WeaponLog.Flush();

            if (KeybindState.IntroActive && !KeybindState.IntroDone)
            {
                KeybindState.IntroAnimTime += Time.deltaTime;
                if (KeybindState.IntroAnimTime >= MenuRenderer.IntroDuration)
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

            if (UiState.MenuPosition.x < 0 || UiState.MenuPosition.y < 0)
                UiState.MenuPosition = new Vector2(
                    (Screen.width - Screen.width * UiState.MenuScale) / 2f,
                    (Screen.height - Screen.height * UiState.MenuScale) / 2f);

            _holderCheckTimer += Time.deltaTime;
            if (_holderCheckTimer >= HolderCheckInterval)
            {
                _holderCheckTimer = 0f;
                if (_mainCameraHolder == null || AimShared.MainCameraHolder == null) FindMainCameraHolder();
            }

            _watchdogTimer += Time.deltaTime;
            if (_watchdogTimer >= WatchdogInterval) { _watchdogTimer = 0f; RunWatchdog(); }

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval) { _scanTimer = 0f; DoScan(); }

            EspModule.ToDraw.Clear();
            foreach (var t in EspModule.TrackedDict.Values)
            {
                if (t.Transform == null) continue;
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
            if (!AimbotModule.Enabled) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            AimbotModule.Update(_cam, _localTeam, EspModule.TrackedDict);

            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;

            var cur = AimbotModule.CurrentTarget;
            if (!AimbotModule.AimOnTeammates && cur != null && _localTeam > 0 && cur.Team > 0 && cur.Team == _localTeam)
                return;

            float angleX = AimbotModule.TargetAngleX;
            float angleY = AimbotModule.TargetAngleY;

            float angleMag = Mathf.Sqrt(angleX * angleX + angleY * angleY);
            if (angleMag > AimbotModule.Fov) return;

            float sens = Mathf.Clamp(AimbotModule.Sensitivity, 0.01f, 3f);
            float px = angleX * sens;
            float py = angleY * sens;

            const float MaxStep = 150f;
            if (px > MaxStep) px = MaxStep;
            if (px < -MaxStep) px = -MaxStep;
            if (py > MaxStep) py = MaxStep;
            if (py < -MaxStep) py = -MaxStep;

            int dx = Mathf.RoundToInt(px);
            int dy = Mathf.RoundToInt(py);

            if (dx == 0 && Mathf.Abs(px) > 0.01f) dx = px > 0 ? 1 : -1;
            if (dy == 0 && Mathf.Abs(py) > 0.01f) dy = py > 0 ? 1 : -1;

            if (dx != 0 || dy != 0)
            {
                MouseEmulator.Move(dx, dy);
            }
        }
        catch (Exception e) { LogThrottled("aim_ex", $"[ESP] LateUpdate EX: {e.Message}", true); }
        finally { WeaponLog.Active = false; }
    }

    [HideFromIl2Cpp]
    void FindMainCameraHolder()
    {
        try
        {
            var pm = FindPlayerManager();
            if (pm == null) return;

            var localPlayer = ReadLocalPlayerFromPm(pm);
            if (localPlayer == null) return;

            AimShared.LocalPlayer = localPlayer;

            IntPtr ptr = Marshal.ReadIntPtr(localPlayer.Pointer, OFF_PC_MAINCAMHOLDER);
            if (ptr == IntPtr.Zero)
            {
                Plugin.LogSource?.LogInfo("[ESP] _mainCameraHolder = null");
                return;
            }

            var tr = new Transform(ptr);
            if (tr != null && tr.Pointer != IntPtr.Zero)
            {
                _mainCameraHolder = tr;
                AimShared.MainCameraHolder = tr;
                Plugin.LogSource?.LogInfo($"[ESP] _mainCameraHolder found: {tr.name} (parent={tr.parent?.name})");
            }
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[ESP] FindMainCameraHolder EX: {e.Message}");
        }
    }

    void OnGUI()
    {
        try
        {
            if (_whiteTex == null) { _whiteTex = new Texture2D(1, 1); _whiteTex.SetPixel(0, 0, Color.white); _whiteTex.Apply(); }
            if (_cam == null) return;

            var e = Event.current;
            bool isRepaint = e == null || e.type == EventType.Repaint;
            bool isMouse = e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag || e.type == EventType.MouseUp);

            if (isRepaint)
            {
                if (EspModule.BoxesEnabled && !UiState.MenuVisible)
                {
                    foreach (var t in EspModule.ToDraw)
                    {
                        try
                        {
                            if (t.Transform == null) continue;
                            Vector3 hw = t.Transform.position + Vector3.up * EspModule.HeadOffset;
                            Vector3 fw = t.Transform.position - Vector3.up * EspModule.FootOffset;
                            Vector3 sh = _cam.WorldToScreenPoint(hw);
                            Vector3 sf = _cam.WorldToScreenPoint(fw);
                            if (sh.z < 0 || sf.z < 0) continue;

                            float x = sh.x;
                            float top = Screen.height - sh.y;
                            float bottom = Screen.height - sf.y;
                            float height = Mathf.Abs(bottom - top);
                            float width = height * EspModule.BoxWidthRatio;
                            if (width < 4f) width = 4f;
                            if (height < 8f) height = 8f;
                            float left = x - width / 2f;

                            Color fill = t.Team == 1 ? new Color(1f, 0f, 0f, 0.12f) : new Color(0f, 0.5f, 1f, 0.12f);
                            Color outl = t.Team == 1 ? Color.red : Color.cyan;

                            GUI.color = fill;
                            GUI.DrawTexture(new Rect(left, top, width, height), _whiteTex);
                            GUI.color = outl;
                            float th = 2f;
                            GUI.DrawTexture(new Rect(left, top, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top + height - th, width, th), _whiteTex);
                            GUI.DrawTexture(new Rect(left, top, th, height), _whiteTex);
                            GUI.DrawTexture(new Rect(left + width - th, top, th, height), _whiteTex);
                            GUI.color = Color.white;
                        }
                        catch { }
                    }
                }

                if (EspModule.NamesEnabled && !UiState.MenuVisible)
                {
                    foreach (var t in EspModule.ToDraw)
                    {
                        try
                        {
                            if (t.Transform == null) continue;
                            Vector3 hw = t.Transform.position + Vector3.up * EspModule.HeadOffset;
                            Vector3 sh = _cam.WorldToScreenPoint(hw);
                            if (sh.z < 0) continue;
                            float x = sh.x;
                            float top = Screen.height - sh.y;
                            float dist = Vector3.Distance(_cam.transform.position, t.Transform.position);
                            if (dist > EspModule.NameMaxDist) continue;

                            string lbl = t.Name + (EspModule.DistanceEnabled ? $" [{dist:F0}m]" : "");
                            Vector2 size = GUI.skin.label.CalcSize(new GUIContent(lbl));
                            float lw = size.x + 10f;
                            float ll = x - lw / 2f;

                            GUI.color = new Color(0f, 0f, 0f, 0.65f);
                            GUI.DrawTexture(new Rect(ll - 2f, top - 22f, lw + 4f, 20f), _whiteTex);
                            GUI.color = Color.white;
                            GUI.Label(new Rect(ll, top - 21f, lw, 18f), lbl);
                        }
                        catch { }
                    }
                }

                MenuRenderer.DrawFovCircle();
                Watermark.DrawBackground(_whiteTex);
                Watermark.DrawText();
                BindListRenderer.Draw();
                BombStatusRenderer.Draw();
            }

            if (isMouse)
            {
                Watermark.HandleDrag();
                BindListRenderer.HandleDrag();
                BombStatusRenderer.HandleDrag();
            }

            if (KeybindState.IntroActive && !KeybindState.IntroDone)
            {
                if (isRepaint) MenuRenderer.DrawIntroAnimation(_whiteTex, KeybindState.IntroAnimTime);
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
            if (_watchdogTicks % 6 == 0)
            {
                UpdateLocalTeam();
                string holderInfo = _mainCameraHolder == null ? "null" : _mainCameraHolder.name;
                Plugin.LogSource?.LogInfo($"[ESP] Watchdog #{_watchdogTicks}: tracked={EspModule.TrackedDict.Count}, localTeam={_localTeam}, aimlock={AimbotModule.Enabled}, active={KeybindState.AimbotActive}, hasTarget={AimbotModule.HasTarget}, holder={holderInfo}");
            }
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[ESP] Watchdog EX: {e}"); }
    }

    [HideFromIl2Cpp]
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

    [HideFromIl2Cpp]
    int ReadByteAtOffset(object obj, int offset)
    {
        try
        {
            IntPtr ptr = IntPtr.Zero;
            if (obj is Il2CppObjectBase il2cppObj) ptr = il2cppObj.Pointer;
            else if (obj is Component comp) ptr = comp.Pointer;
            if (ptr == IntPtr.Zero) return -1;
            return Marshal.ReadByte(ptr, offset);
        }
        catch { return -1; }
    }

    [HideFromIl2Cpp]
    int ReadTeam(Component component, bool isBot)
    {
        if (component == null) return -1;
        if (isBot)
        {
            int offTeam = ReadByteAtOffset(component, OFF_BOT_TEAM);
            if (offTeam > 0 && offTeam <= 3) return offTeam;
            int viaCfmp = ReadTeamViaCfmp(component);
            if (viaCfmp >= 0) return viaCfmp;
            return -1;
        }
        return ReadTeamViaCfmp(component);
    }

    [HideFromIl2Cpp]
    int ReadTeamViaCfmp(Component component)
    {
        if (component == null) return -1;
        int viaMember = ReadTeamFromMember(_playerTeamMember, component);
        if (viaMember > 0 && viaMember <= 3) return viaMember;
        try
        {
            int t = Marshal.ReadByte(component.Pointer, OFF_PC_CFMP);
            if (t > 0 && t <= 3) return t;
        }
        catch { }
        return -1;
    }

    [HideFromIl2Cpp]
    Component FindPlayerManager()
    {
        if (_playerManagerType == null) return null;
        if (_cachedPm != null)
        {
            try { if (_cachedPm.Pointer != IntPtr.Zero) return _cachedPm; } catch { }
            _cachedPm = null;
        }
        try
        {
            var obj = UnityEngine.Object.FindObjectOfType(_playerManagerType);
            if (obj != null)
            {
                _cachedPm = obj.TryCast<Component>();
                if (_cachedPm != null)
                {
                    Plugin.LogSource?.LogInfo("[ESP] PlayerManager found via FindObjectOfType");
                    return _cachedPm;
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled("pm_foft_ex", $"[ESP] FindObjectOfType(PM) EX: {e.Message}", true);
        }
        return null;
    }

    [HideFromIl2Cpp]
    Component ReadLocalPlayerFromPm(Component pm)
    {
        if (pm == null) return null;
        try
        {
            IntPtr lpPtr = Marshal.ReadIntPtr(pm.Pointer, OFF_PM_CFOG);
            if (lpPtr != IntPtr.Zero)
            {
                var lp = new Component(lpPtr);
                if (lp != null && lp.Pointer != IntPtr.Zero) return lp;
            }
        }
        catch { }
        try
        {
            var dpkyProp = _playerManagerType.GetProperty("dpky",
                (Il2CppSystem.Reflection.BindingFlags)(int)(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance));
            if (dpkyProp != null)
            {
                var val = dpkyProp.GetValue(pm.Cast<Il2CppSystem.Object>());
                if (val != null)
                {
                    var comp = val.TryCast<Component>();
                    if (comp != null) return comp;
                }
            }
        }
        catch { }
        try
        {
            var cfogField = _playerManagerType.GetField("<cfog>k__BackingField",
                (Il2CppSystem.Reflection.BindingFlags)(int)(
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance));
            if (cfogField != null)
            {
                var val = cfogField.GetValue(pm.Cast<Il2CppSystem.Object>());
                if (val != null)
                {
                    var comp = val.TryCast<Component>();
                    if (comp != null) return comp;
                }
            }
        }
        catch { }
        return null;
    }

    [HideFromIl2Cpp]
    void UpdateLocalTeam()
    {
        try
        {
            if (_playerManagerType == null) return;
            if (_cachedPm == null)
            {
                _pmRetryTimer += Time.deltaTime;
                if (_pmRetryTimer < PmRetryInterval) return;
                _pmRetryTimer = 0f;
            }
            var pm = FindPlayerManager();
            if (pm == null)
            {
                LogThrottled("pm_null", "[ESP] PlayerManager not found", true);
                return;
            }
            var localPlayer = ReadLocalPlayerFromPm(pm);
            if (localPlayer == null)
            {
                LogThrottled("cfog_null", "[ESP] localPlayer is null", true);
                return;
            }
            int newTeam = ReadTeamViaCfmp(localPlayer);
            if (newTeam >= 0 && newTeam != _localTeam)
            {
                Plugin.LogSource?.LogInfo($"[ESP] Local team: {_localTeam} -> {newTeam}");
                _localTeam = newTeam;
            }
        }
        catch (Exception e) { LogThrottled("localt_ex", $"[ESP] LocalTeam EX: {e.Message}", true); }
    }

    [HideFromIl2Cpp]
    private static bool IsTeamEnum(Type t) =>
        t != null && t.IsEnum && (t.Name == "Team" || (t.FullName != null && t.FullName.EndsWith(".Team")));

    [HideFromIl2Cpp]
    private static Type FindTypeByName(string shortName, out string full)
    {
        full = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var d = asm.GetType(shortName); if (d != null) { full = d.FullName; return d; } } catch { }
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == shortName) { full = t.FullName; return t; }
                }
            }
            catch { }
        }
        return null;
    }

    [HideFromIl2Cpp]
    private static Type FindTypeByFullName(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
        }
        return null;
    }

    [HideFromIl2Cpp]
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

    [HideFromIl2Cpp]
    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            var bt = FindTypeByName("BotPlayerController", out _);
            var pt = FindTypeByName("PlayerController", out _);

            var pmType = FindTypeByFullName("Chillow.StandChillow.Player.PlayerManager")
                         ?? FindTypeByName("PlayerManager", out _);

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);
            if (pmType != null) _playerManagerType = Il2CppType.From(pmType);

            _playerTeamMember = null;
            string owner = null;
            _playerTeamMember = FindTeamMember(pt, out owner);
            Plugin.LogSource?.LogInfo($"[ESP] Team member: {owner ?? "null"}");
            Plugin.LogSource?.LogInfo($"[ESP] Resolve: Bot={_botType != null}, Player={_playerType != null}, PM={_playerManagerType != null}, PMType={(pmType?.FullName ?? "null")}, Team={_playerTeamMember != null}");
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
            
            AimbotModule.ClearCaches(); // очистка кэша костей
            
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

    [HideFromIl2Cpp]
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
