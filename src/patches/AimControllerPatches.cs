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
            var candidates = new[]
            {
                new[] { "Chillow.StandChillow.Player.PlayerController", "PlayerController" },
                new[] { "Chillow.StandChillow.Player.Controller", "Controller" }
            };

            MethodInfo targetMethod = null;
            string targetName = null;

            foreach (var group in candidates)
            {
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var full in group)
                    {
                        try { t = asm.GetType(full); if (t != null) break; } catch { }
                    }
                    if (t != null) break;
                }
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var x in asm.GetTypes())
                            {
                                if (x.Name == group[group.Length - 1]) { t = x; break; }
                            }
                        }
                        catch { }
                        if (t != null) break;
                    }
                }
                if (t == null) continue;

                foreach (var name in new[] { "LateUpdate", "Update", "Tick" })
                {
                    var mi = FindMethodInHierarchy(t, name);
                    if (mi != null)
                    {
                        targetMethod = mi;
                        targetName = $"{t.FullName}.{name}";
                        break;
                    }
                }
                if (targetMethod != null) break;
            }

            if (targetMethod == null)
            {
                Plugin.LogSource?.LogError("[AIM] No patchable method found in PlayerController/Controller");
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
