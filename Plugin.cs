using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.IO;
using Il2CppInterop.Runtime.Injection;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "ExplodeDLC", "2.0.0.0")]
public class Plugin : BasePlugin
{
    public static ManualLogSource LogSource;
    public static ConfigFile ConfigFile;
    public static Plugin Instance;

    public static ConfigEntry<bool> CfgEspBoxes;
    public static ConfigEntry<bool> CfgEspNames;
    public static ConfigEntry<bool> CfgEspDistance;
    public static ConfigEntry<bool> CfgOnlyEnemies;
    public static ConfigEntry<bool> CfgShowTr;
    public static ConfigEntry<bool> CfgShowCt;
    public static ConfigEntry<bool> CfgShowSpectator;

    public static ConfigEntry<bool> CfgAimbotEnabled;
    public static ConfigEntry<float> CfgAimbotFov;
    public static ConfigEntry<bool> CfgAimbotDrawFov;
    public static ConfigEntry<float> CfgAimbotSensitivity;
    public static ConfigEntry<string> CfgAimbotBone;
    public static ConfigEntry<bool> CfgAimbotToggleMode;
    public static ConfigEntry<string> CfgAimbotKey;
    public static ConfigEntry<bool> CfgAimbotAimOnTeammates;
    
    public static ConfigEntry<bool> CfgWatermarkEnabled;
    public static ConfigEntry<float> CfgWatermarkX;
    public static ConfigEntry<float> CfgWatermarkY;

    public static ConfigEntry<bool> CfgShowBindList;
    public static ConfigEntry<float> CfgBindListX;
    public static ConfigEntry<float> CfgBindListY;

    public static ConfigEntry<bool> CfgShowBombStatus;
    public static ConfigEntry<float> CfgBombStatusX;
    public static ConfigEntry<float> CfgBombStatusY;

    public static ConfigEntry<float> CfgMenuScale;
    public static ConfigEntry<bool> CfgHudWatermark;

    // Возвращены для совместимости с MediaRenderer.cs
    public static ConfigEntry<bool> CfgMediaEnabled;
    public static ConfigEntry<float> CfgMediaX;
    public static ConfigEntry<float> CfgMediaY;

    public override void Load()
    {
        Instance = this;
        LogSource = Log;
        Log.LogInfo("[ESP] ===== Plugin.Load() =====");
        try
        {
            string cfgPath = Path.Combine(Paths.ConfigPath, "explode.cfg");
            ConfigFile = new ConfigFile(cfgPath, true);

            CfgEspBoxes = ConfigFile.Bind("ESP", "Boxes", true);
            CfgEspNames = ConfigFile.Bind("ESP", "Names", true);
            CfgEspDistance = ConfigFile.Bind("ESP", "Distance", true);
            CfgOnlyEnemies = ConfigFile.Bind("ESP", "OnlyEnemies", false);
            CfgShowTr = ConfigFile.Bind("ESP", "ShowTr", true);
            CfgShowCt = ConfigFile.Bind("ESP", "ShowCt", true);
            CfgShowSpectator = ConfigFile.Bind("ESP", "ShowSpectator", false);

            CfgAimbotEnabled = ConfigFile.Bind("AimLock", "Enabled", false);
            CfgAimbotFov = ConfigFile.Bind("AimLock", "Fov", 20f);
            CfgAimbotDrawFov = ConfigFile.Bind("AimLock", "DrawFov", false);
            CfgAimbotSensitivity = ConfigFile.Bind("AimLock", "Sensitivity", 0.15f);
            CfgAimbotBone = ConfigFile.Bind("AimLock", "Bone", "Neck");
            CfgAimbotToggleMode = ConfigFile.Bind("AimLock", "ToggleMode", false);
            CfgAimbotKey = ConfigFile.Bind("AimLock", "Key", "Mouse0");
            CfgAimbotAimOnTeammates = ConfigFile.Bind("AimLock", "AimOnTeammates", false,
                "If true, aimbot will also lock onto teammates.");
                
            CfgWatermarkEnabled = ConfigFile.Bind("Render", "WatermarkEnabled", true);
            CfgWatermarkX = ConfigFile.Bind("Render", "WatermarkX", -1f);
            CfgWatermarkY = ConfigFile.Bind("Render", "WatermarkY", -1f);

            CfgShowBindList = ConfigFile.Bind("Misc", "ShowBindList", true);
            CfgBindListX = ConfigFile.Bind("Misc", "BindListX", -1f);
            CfgBindListY = ConfigFile.Bind("Misc", "BindListY", -1f);

            CfgShowBombStatus = ConfigFile.Bind("Misc", "ShowBombStatus", true);
            CfgBombStatusX = ConfigFile.Bind("Misc", "BombStatusX", -1f);
            CfgBombStatusY = ConfigFile.Bind("Misc", "BombStatusY", -1f);

            CfgMenuScale = ConfigFile.Bind("Hud", "MenuScale", 1.0f,
                "Menu size as fraction of screen (0.25 = 25%, 2.0 = 200%).");
            CfgHudWatermark = ConfigFile.Bind("Hud", "Watermark", true);

            // Медиа-плеер (заглушка, но конфиги нужны для компиляции)
            CfgMediaEnabled = ConfigFile.Bind("Media", "Enabled", false);
            CfgMediaX = ConfigFile.Bind("Media", "X", -1f);
            CfgMediaY = ConfigFile.Bind("Media", "Y", -1f);

            TeamFilter.ShowTr = CfgShowTr.Value;
            TeamFilter.ShowCt = CfgShowCt.Value;
            TeamFilter.ShowSpectator = CfgShowSpectator.Value;
            TeamFilter.OnlyEnemies = CfgOnlyEnemies.Value;

            BindListState.Enabled = CfgShowBindList.Value;
            BindListState.Position = new Vector2(CfgBindListX.Value, CfgBindListY.Value);
            BombStatusRenderer.Enabled = CfgShowBombStatus.Value;
            BombStatusRenderer.Position = new Vector2(CfgBombStatusX.Value, CfgBombStatusY.Value);

            UiState.MenuSection = 0;

            ClassInjector.RegisterTypeInIl2Cpp<EspBehaviour>();
            ClassInjector.RegisterTypeInIl2Cpp<KeybindListener>();

            AddComponent<KeybindListener>();
            IL2CPPChainloader.AddUnityComponent<EspBehaviour>();

            Log.LogInfo("[ESP] Components added");
            Log.LogInfo("");
            Log.LogInfo("[Explode] Welcome to Explode DLC!");
            Log.LogInfo("");
            Log.LogInfo("   ,d88b.d88b,");
            Log.LogInfo("   88888888888");
            Log.LogInfo("   `Y8888888Y'      Made with love");
            Log.LogInfo("     `Y888Y'");
            Log.LogInfo("       `Y'");
            Log.LogInfo("");
        }
        catch (Exception e) { Log.LogError($"[ESP] Load EX: {e}"); }
    }
}
