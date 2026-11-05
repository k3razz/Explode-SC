using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public static class AimControllerPatches
{
    private static bool _applied;
    private static int _lastFrame = -1;

    private const int OFF_AIM_SNAPSHOT = 0x28;
    private const int OFF_AIM_X = 0x54;
    private const int OFF_AIM_Y = 0x58;
    private const int OFF_MIN_X = 0x94;
    private const int OFF_MAX_X = 0x98;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        try
        {
            MethodInfo targetMethod = null;
            string targetName = null;

            var aimCtrlType = FindType("Chillow.StandChillow.Player.Aim.AimController");
            if (aimCtrlType == null) aimCtrlType = FindTypeByShortName("AimController");

            if (aimCtrlType != null)
            {
                foreach (var name in new[] { "nwx", "Update", "nwy", "nwz" })
                {
                    try
                    {
                        var mi = aimCtrlType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (mi != null && mi.GetParameters().Length == 0)
                        {
                            targetMethod = mi;
                            targetName = $"{aimCtrlType.FullName}.{name}";
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (targetMethod == null)
            {
                Plugin.LogSource?.LogError("[AIM] AimController Update not found, fallback PlayerController.LateUpdate");
                var pcType = FindType("Chillow.StandChillow.Player.PlayerController");
                if (pcType != null)
                {
                    foreach (var name in new[] { "LateUpdate", "Update" })
                    {
                        var mi = FindMethodInHierarchy(pcType, name);
                        if (mi != null) { targetMethod = mi; targetName = $"{pcType.FullName}.{name}"; break; }
                    }
                }
            }

            if (targetMethod == null)
            {
                Plugin.LogSource?.LogError("[AIM] No patchable method found");
                return;
            }

            var harmony = new Harmony("com.you.dynamicesp.aimctrl");
            harmony.Patch(targetMethod,
                prefix: new HarmonyMethod(typeof(AimControllerPatches), nameof(Prefix)));
            Plugin.LogSource?.LogInfo($"[AIM] Patched {targetName} (prefix)");
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

    public static void Prefix(object __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;

            int fc = Time.frameCount;
            if (fc == _lastFrame) return;

            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            IntPtr instPtr = IntPtr.Zero;
            if (__instance is Il2CppObjectBase il2cppObj) instPtr = il2cppObj.Pointer;
            if (instPtr == IntPtr.Zero) return;

            IntPtr snapPtr = Marshal.ReadIntPtr(instPtr, OFF_AIM_SNAPSHOT);
            if (snapPtr == IntPtr.Zero) return;

            Vector3 dir = AimbotModule.TargetDirection;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;

            float minX = -89f, maxX = 89f;
            try
            {
                float mn = ReadFloat(instPtr, OFF_MIN_X);
                float mx = ReadFloat(instPtr, OFF_MAX_X);
                if (mx > mn) { minX = mn; maxX = mx; }
            }
            catch { }

            pitch = Mathf.Clamp(pitch, minX, maxX);

            WriteFloat(snapPtr, OFF_AIM_X, yaw);
            WriteFloat(snapPtr, OFF_AIM_Y, pitch);

            _lastFrame = fc;

            Plugin.LogSource?.LogInfo($"[AIM] Prefix: yaw={yaw:F1} pitch={pitch:F1} tgt={dir}");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[AIM] Prefix EX: {e.Message}"); }
    }

    private static float ReadFloat(IntPtr ptr, int offset)
    {
        int bits = Marshal.ReadInt32(ptr, offset);
        return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    }

    private static void WriteFloat(IntPtr ptr, int offset, float value)
    {
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        Marshal.WriteInt32(ptr, offset, bits);
    }
}
