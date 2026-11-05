using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace MyDynamicESP;

public static class AimControllerPatches
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        try
        {
            Type aimCtrlType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Assembly-CSharp") continue;
                aimCtrlType = asm.GetType("Il2CppChillow.StandChillow.Player.AimController");
                if (aimCtrlType != null) break;
            }

            if (aimCtrlType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "AimController" && t.Namespace != null && t.Namespace.Contains("Chillow"))
                            {
                                aimCtrlType = t;
                                break;
                            }
                        }
                    }
                    catch { }
                    if (aimCtrlType != null) break;
                }
            }

            if (aimCtrlType == null)
            {
                Plugin.LogSource?.LogError("[AIM] AimController type not found");
                return;
            }

            var tickMethod = aimCtrlType.GetMethod("Tick", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (tickMethod == null)
            {
                Plugin.LogSource?.LogError("[AIM] AimController.Tick not found");
                return;
            }

            var harmony = new Harmony("com.you.dynamicesp.aimctrl");
            harmony.Patch(tickMethod, postfix: new HarmonyMethod(typeof(AimControllerPatches), nameof(Tick_Postfix)));
            Plugin.LogSource?.LogInfo($"[AIM] Patched {aimCtrlType.FullName}.Tick");
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[AIM] Apply EX: {e.Message}");
        }
    }

    public static void Tick_Postfix()
    {
        try
        {
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            var holder = AimShared.MainCameraHolder;
            if (holder == null) return;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            holder.rotation = Quaternion.Euler(e.x, e.y, 0f);
        }
        catch { }
    }
}