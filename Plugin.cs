using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
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

    // AimLock configs
    public static ConfigEntry<bool> CfgAimbotEnabled;
    public static ConfigEntry<float> CfgAimbotFov;
    public static ConfigEntry<float> CfgAimbotSmooth;
    public static ConfigEntry<bool> CfgAimbotToggleMode;
    public static ConfigEntry<string> CfgAimbotKey;

    public static ConfigEntry<bool> CfgWatermarkEnabled;
    public static ConfigEntry<float> CfgWatermarkX;
    public static ConfigEntry<float> CfgWatermarkY;

    public static ConfigEntry<bool> CfgShowBindList;

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
            CfgAimbotFov = ConfigFile.Bind("AimLock", "Fov", 15f);
            CfgAimbotSmooth = ConfigFile.Bind("AimLock", "Smooth", 15f);
            CfgAimbotToggleMode = ConfigFile.Bind("AimLock", "ToggleMode", false);
            CfgAimbotKey = ConfigFile.Bind("AimLock", "Key", "Mouse0");

            CfgWatermarkEnabled = ConfigFile.Bind("Render", "WatermarkEnabled", true);
            CfgWatermarkX = ConfigFile.Bind("Render", "WatermarkX", -1f);
            CfgWatermarkY = ConfigFile.Bind("Render", "WatermarkY", -1f);

            CfgShowBindList = ConfigFile.Bind("Misc", "ShowBindList", true);

            TeamFilter.ShowTr = CfgShowTr.Value;
            TeamFilter.ShowCt = CfgShowCt.Value;
            TeamFilter.ShowSpectator = CfgShowSpectator.Value;
            TeamFilter.OnlyEnemies = CfgOnlyEnemies.Value;

            BindListState.Enabled = CfgShowBindList.Value;

            ClassInjector.RegisterTypeInIl2Cpp<EspBehaviour>();
            ClassInjector.RegisterTypeInIl2Cpp<KeybindListener>();
            ClassInjector.RegisterTypeInIl2Cpp<AimCameraHook>();

            AddComponent<KeybindListener>();
            IL2CPPChainloader.AddUnityComponent<EspBehaviour>();

            // Harmony — патчим Controller.Tick / LateUpdate для AimController
            try
            {
                var harmony = new Harmony("com.you.dynamicesp.harmony");
                harmony.PatchAll(typeof(AimControllerPatches).Assembly);
                Log.LogInfo("[ESP] Harmony patches applied");
            }
            catch (Exception he) { Log.LogError($"[ESP] Harmony patch EX: {he}"); }

            Log.LogInfo("[ESP] Components added");
        }
        catch (Exception e) { Log.LogError($"[ESP] Load EX: {e}"); }
    }
}
