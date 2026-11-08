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

            UiState.MenuScale = Mathf.Clamp(Plugin.CfgMenuScale.Value, 0.25f, 2f);
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

    // ... (методы FindMainCameraHolder, OnGUI, RunWatchdog и т.д. остаются БЕЗ ИЗМЕНЕНИЙ)
    
    void DoScan()
    {
        try
        {
            ResolveTypes();
            if (_botType == null && _playerType == null) return;
            UpdateLocalTeam();
            
            AimbotModule.ClearCaches(); // Очищаем кэш костей при скане
            
            EspModule.SeenList.Clear();
            ScanTypeInternal(_botType, true, "Bot");
            ScanTypeInternal(_playerType, false, "Player");
            var dead = new List<int>();
            foreach (var kv in EspModule.TrackedDict) if (!EspModule.SeenList.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) EspModule.TrackedDict.Remove(id);
        }
        catch (Exception e) { LogThrottled("scan_ex", $"[ESP] DoScan EX: {e}", true); }
    }
}