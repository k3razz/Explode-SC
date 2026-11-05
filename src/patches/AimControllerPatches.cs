using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public static class AimControllerPatches
{
    private static bool _applied;
    private static int _lastFrame = -1;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        try
        {
            var aimCtrlType = FindType("Chillow.StandChillow.Player.Aim.AimController");
            if (aimCtrlType == null) aimCtrlType = FindTypeByShortName("AimController");
            if (aimCtrlType == null) { Plugin.LogSource?.LogError("[AIM] AimController not found"); return; }

            var nxw = aimCtrlType.GetMethod("nxw", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (nxw == null) { Plugin.LogSource?.LogError("[AIM] nxw not found"); return; }

            var harmony = new Harmony("com.you.dynamicesp.aimctrl");
            harmony.Patch(nxw, postfix: new HarmonyMethod(typeof(AimControllerPatches), nameof(PostfixUpdate)));
            Plugin.LogSource?.LogInfo($"[AIM] Patched {aimCtrlType.FullName}.nxw (postfix)");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[AIM] Apply EX: {e.Message}"); }
    }

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
        }
        return null;
    }

    private static Type FindTypeByShortName(string shortName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == shortName) return t;
                }
            }
            catch { }
        }
        return null;
    }

    public static void PostfixUpdate(object __instance)
    {
        try
        {
            Plugin.LogSource?.LogInfo($"[AIM] PostfixUpdate called enabled={AimbotModule.Enabled} active={KeybindState.AimbotActive} hasTarget={AimbotModule.HasTarget}");

            if (__instance == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            int fc = Time.frameCount;
            if (fc == _lastFrame) return;

            var mainCam = Camera.main;
            if (mainCam == null) return;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            mainCam.transform.rotation = Quaternion.Euler(e.x, e.y, 0f);

            _lastFrame = fc;

            Plugin.LogSource?.LogInfo($"[AIM] WriteAim cam={mainCam.name} yaw={e.y:F1} pitch={e.x:F1} tgt={AimbotModule.TargetDirection}");
        }
        catch (Exception ex) { Plugin.LogSource?.LogError($"[AIM] PostfixUpdate EX: {ex.Message}"); }
    }
}
