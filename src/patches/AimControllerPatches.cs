using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

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
            Type aimCtrlType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Assembly-CSharp") continue;
                aimCtrlType = asm.GetType("Chillow.StandChillow.Player.Aim.AimController");
                if (aimCtrlType == null)
                    aimCtrlType = asm.GetType("Il2CppChillow.StandChillow.Player.Aim.AimController");
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
                            if (t.Name == "AimController") { aimCtrlType = t; break; }
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

            Plugin.LogSource?.LogInfo($"[AIM] AimController found: {aimCtrlType.FullName}");

            MethodInfo targetMethod = null;
            string methodName = null;
            foreach (var name in new[] { "Tick", "LateUpdate", "Update" })
            {
                var mi = FindMethodInHierarchy(aimCtrlType, name);
                if (mi != null) { targetMethod = mi; methodName = name; break; }
            }

            if (targetMethod == null)
            {
                var allMethods = aimCtrlType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in allMethods)
                {
                    if (m.GetParameters().Length == 0 &&
                        (m.Name.IndexOf("Tick", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         m.Name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Plugin.LogSource?.LogInfo($"[AIM] candidate: {m.Name}");
                    }
                }
                Plugin.LogSource?.LogError("[AIM] No Tick/LateUpdate/Update found");
                return;
            }

            var harmony = new Harmony("com.you.dynamicesp.aimctrl");
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(AimControllerPatches), nameof(Postfix)));
            Plugin.LogSource?.LogInfo($"[AIM] Patched {targetMethod.DeclaringType?.FullName}.{methodName}");
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[AIM] Apply EX: {e.Message}");
        }
    }

    private static MethodInfo FindMethodInHierarchy(Type t, string name)
    {
        var cur = t;
        int depth = 0;
        while (cur != null && cur != typeof(object) && depth < 8)
        {
            var mi = cur.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (mi != null) return mi;
            cur = cur.BaseType;
            depth++;
        }
        return null;
    }

    public static void Postfix()
    {
        try
        {
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;

            int fc = Time.frameCount;
            if (fc == _lastFrame) return;
            _lastFrame = fc;

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
