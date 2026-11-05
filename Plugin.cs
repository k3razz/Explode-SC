using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.IO;
using Il2CppInterop.Runtime.Injection;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
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
    public static ConfigEntry<float> CfgAimbotSensitivity;
    public static ConfigEntry<bool> CfgAimbotToggleMode;
    public static ConfigEntry<string> CfgAimbotKey;

    public static ConfigEntry<bool> CfgWatermarkEnabled;
    public static ConfigEntry<float> CfgWatermarkX;
    public static ConfigEntry<float> CfgWatermarkY;

    public static ConfigEntry<bool> CfgShowBindList;
    public static ConfigEntry<float> CfgBindListX;
    public static ConfigEntry<float> CfgBindListY;

    public static ConfigEntry<bool> CfgShowBombStatus;
    public static ConfigEntry<float> CfgBombStatusX;
    public static ConfigEntry<float> CfgBombStatusY;

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
            CfgAimbotFov = ConfigFile.Bind("AimLock", "Fov", 90f);
            CfgAimbotSensitivity = ConfigFile.Bind("AimLock", "Sensitivity", 0.15f);
            CfgAimbotToggleMode = ConfigFile.Bind("AimLock", "ToggleMode", false);
            CfgAimbotKey = ConfigFile.Bind("AimLock", "Key", "Mouse0");

            CfgWatermarkEnabled = ConfigFile.Bind("Render", "WatermarkEnabled", true);
            CfgWatermarkX = ConfigFile.Bind("Render", "WatermarkX", -1f);
            CfgWatermarkY = ConfigFile.Bind("Render", "WatermarkY", -1f);

            CfgShowBindList = ConfigFile.Bind("Misc", "ShowBindList", true);
            CfgBindListX = ConfigFile.Bind("Misc", "BindListX", -1f);
            CfgBindListY = ConfigFile.Bind("Misc", "BindListY", -1f);

            CfgShowBombStatus = ConfigFile.Bind("Misc", "ShowBombStatus", true);
            CfgBombStatusX = ConfigFile.Bind("Misc", "BombStatusX", -1f);
            CfgBombStatusY = ConfigFile.Bind("Misc", "BombStatusY", -1f);

            TeamFilter.ShowTr = CfgShowTr.Value;
            TeamFilter.ShowCt = CfgShowCt.Value;
            TeamFilter.ShowSpectator = CfgShowSpectator.Value;
            TeamFilter.OnlyEnemies = CfgOnlyEnemies.Value;

            BindListState.Enabled = CfgShowBindList.Value;
            BindListState.Position = new Vector2(CfgBindListX.Value, CfgBindListY.Value);
            BombStatusRenderer.Enabled = CfgShowBombStatus.Value;
            BombStatusRenderer.Position = new Vector2(CfgBombStatusX.Value, CfgBombStatusY.Value);

            ClassInjector.RegisterTypeInIl2Cpp<EspBehaviour>();
            ClassInjector.RegisterTypeInIl2Cpp<KeybindListener>();

            AddComponent<KeybindListener>();
            IL2CPPChainloader.AddUnityComponent<EspBehaviour>();

            WeaponLog.Apply();

            Log.LogInfo("[ESP] Components added");
        }
        catch (Exception e) { Log.LogError($"[ESP] Load EX: {e}"); }
    }
}
