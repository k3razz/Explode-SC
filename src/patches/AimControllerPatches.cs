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
            Type aimType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Assembly-CSharp") continue;
                aimType = asm.GetType("Chillow.StandChillow.Player.Aim.AimController");
                if (aimType != null) break;
            }
            if (aimType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == "AimController") { aimType = t; break; }
                        }
                    }
                    catch { }
                    if (aimType != null) break;
                }
            }

            if (aimType != null)
            {
                Plugin.LogSource?.LogInfo($"[AIM] AimController methods:");
                var all = aimType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in all)
                {
                    if (m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                        Plugin.LogSource?.LogInfo($"[AIM]   {m.Name}");
                }
            }

            var pcType = FindType("Chillow.StandChillow.Player.PlayerController");
            if (pcType != null)
            {
                Plugin.LogSource?.LogInfo($"[AIM] PlayerController methods (void, 0 params):");
                var all = pcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in all)
                {
                    if (m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                        Plugin.LogSource?.LogInfo($"[AIM]   {m.Name}");
                }
            }

            var ctrlType = FindType("Chillow.StandChillow.Player.Controller");
            if (ctrlType != null)
            {
                Plugin.LogSource?.LogInfo($"[AIM] Controller methods (void, 0 params):");
                var all = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in all)
                {
                    if (m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                        Plugin.LogSource?.LogInfo($"[AIM]   {m.Name}");
                }
            }

            MethodInfo targetMethod = null;
            string targetName = null;

            foreach (var pair in new[] {
                Tuple.Create("Chillow.StandChillow.Player.PlayerController", "LateUpdate"),
                Tuple.Create("Chillow.StandChillow.Player.PlayerController", "Update"),
                Tuple.Create("Chillow.StandChillow.Player.Controller", "LateUpdate"),
                Tuple.Create("Chillow.StandChillow.Player.Controller", "Update")
            })
            {
                var t = FindType(pair.Item1);
                if (t == null) continue;
                var mi = FindMethodInHierarchy(t, pair.Item2);
                if (mi != null) { targetMethod = mi; targetName = $"{t.FullName}.{pair.Item2}"; break; }
            }

            if (targetMethod == null)
            {
                Plugin.LogSource?.LogError("[AIM] No patchable method found");
                return;
            }

            var harmony = new Harmony("com.you.dynamicesp.aimctrl");
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(AimControllerPatches), nameof(Postfix)));
            Plugin.LogSource?.LogInfo($"[AIM] Patched {targetName}");
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[AIM] Apply EX: {e.Message}");
        }
    }

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var t = asm.GetType(fullName); if (t != null) return t; } catch { }
        }
        return null;
    }

    private static MethodInfo FindMethodInHierarchy(Type t, string name)
    {
        var cur = t;
        int depth = 0;
        while (cur != null && depth < 10)
        {
            try
            {
                var mi = cur.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (mi != null) return mi;
            }
            catch { }
            try { cur = cur.BaseType; } catch { cur = null; }
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
