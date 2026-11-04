using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
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
    public static ConfigEntry<float> CfgAimbotSmooth;
    public static ConfigEntry<bool> CfgAimbotTargetTeammates;
    public static ConfigEntry<bool> CfgAimbotVisibilityCheck;
    public static ConfigEntry<bool> CfgAimbotToggleMode;
    public static ConfigEntry<string> CfgAimbotKey;
    public static ConfigEntry<bool> CfgWatermarkEnabled;
    public static ConfigEntry<float> CfgWatermarkX;
    public static ConfigEntry<float> CfgWatermarkY;

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

            CfgAimbotEnabled = ConfigFile.Bind("Aimbot", "Enabled", false);
            CfgAimbotFov = ConfigFile.Bind("Aimbot", "Fov", 15f);
            CfgAimbotSmooth = ConfigFile.Bind("Aimbot", "Smooth", 15f);
            CfgAimbotTargetTeammates = ConfigFile.Bind("Aimbot", "TargetTeammates", false);
            CfgAimbotVisibilityCheck = ConfigFile.Bind("Aimbot", "VisibilityCheck", false);
            CfgAimbotToggleMode = ConfigFile.Bind("Aimbot", "ToggleMode", false);
            CfgAimbotKey = ConfigFile.Bind("Aimbot", "Key", "Mouse1");

            CfgWatermarkEnabled = ConfigFile.Bind("Render", "WatermarkEnabled", true);
            CfgWatermarkX = ConfigFile.Bind("Render", "WatermarkX", -1f);
            CfgWatermarkY = ConfigFile.Bind("Render", "WatermarkY", -1f);

            TeamFilter.ShowTr = CfgShowTr.Value;
            TeamFilter.ShowCt = CfgShowCt.Value;
            TeamFilter.ShowSpectator = CfgShowSpectator.Value;
            TeamFilter.OnlyEnemies = CfgOnlyEnemies.Value;

            KeybindState.ToggleMode = CfgAimbotToggleMode.Value;
            if (Enum.TryParse(CfgAimbotKey.Value, out KeyCode kc)) KeybindState.AimbotKey = kc;

            ClassInjector.RegisterTypeInIl2Cpp<EspBehaviour>();
            ClassInjector.RegisterTypeInIl2Cpp<KeybindListener>();

            AddComponent<KeybindListener>();
            IL2CPPChainloader.AddUnityComponent<EspBehaviour>();

            try
            {
                var harmony = new Harmony("com.you.dynamicesp");
                harmony.PatchAll();
                Log.LogInfo("[ESP] Harmony patched");
            }
            catch (Exception e)
            {
                Log.LogError($"[ESP] Harmony patch EX: {e}");
            }

            Log.LogInfo("[ESP] Components added");
        }
        catch (Exception e) { Log.LogError($"[ESP] Load EX: {e}"); }
    }
}

// =========================================================
//                    HARMONY PATCH
// =========================================================
[HarmonyPatch]
public static class PlayerDestroyPatch
{
    static MethodBase TargetMethod()
    {
        Type pcType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { pcType = asm.GetType("PlayerController"); if (pcType != null) break; } catch { }
        }
        if (pcType == null) return null;

        var method = pcType.GetMethod("OnDestroy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            var baseType = pcType.BaseType;
            while (baseType != null && method == null)
            {
                method = baseType.GetMethod("OnDestroy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                baseType = baseType.BaseType;
            }
        }
        return method;
    }

    static void Prefix(object __instance)
    {
        if (__instance == null) return;
        try
        {
            var comp = __instance as Component;
            if (comp == null) return;
            int id = comp.gameObject.GetInstanceID();
            EspBehaviour.OnPlayerDestroyed(id);
        }
        catch { }
    }
}