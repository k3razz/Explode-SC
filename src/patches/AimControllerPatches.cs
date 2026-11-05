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
                foreach (var name in new[] { "nxw", "Update", "nyv", "nxz" })
                {
                    try
                    {
                        var mi = aimCtrlType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (mi != null)
                        {
                            if (name == "nyv")
                            {
                                if (mi.GetParameters().Length != 1) continue;
                            }
                            else
                            {
                                if (mi.GetParameters().Length != 0) continue;
                            }
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
                Plugin.LogSource?.LogError("[AIM] No patchable method found");
                return;
            }

            var harmony = new Harmony("com.you.dynamicesp.aimctrl");
            if (targetMethod.Name == "nyv")
            {
                harmony.Patch(targetMethod,
                    prefix: new HarmonyMethod(typeof(AimControllerPatches), nameof(PrefixNyv)));
            }
            else
            {
                harmony.Patch(targetMethod,
                    prefix: new HarmonyMethod(typeof(AimControllerPatches), nameof(PrefixUpdate)));
            }
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

    public static void PrefixUpdate(object __instance)
    {
        try
        {
            Plugin.LogSource?.LogInfo($"[AIM] PrefixUpdate called enabled={AimbotModule.Enabled} active={KeybindState.AimbotActive} hasTarget={AimbotModule.HasTarget}");
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
            if (snapPtr == IntPtr.Zero)
            {
                Plugin.LogSource?.LogInfo("[AIM] PrefixUpdate: AimingSnapshot null");
                return;
            }

            WriteAim(snapPtr, instPtr);

            _lastFrame = fc;
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[AIM] PrefixUpdate EX: {e.Message}"); }
    }

    public static void PrefixNyv(object __instance, object __0)
    {
        try
        {
            if (__0 == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;

            int fc = Time.frameCount;
            if (fc == _lastFrame) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            IntPtr snapPtr = IntPtr.Zero;
            if (__0 is Il2CppObjectBase snapObj) snapPtr = snapObj.Pointer;
            if (snapPtr == IntPtr.Zero) return;

            IntPtr instPtr = IntPtr.Zero;
            if (__instance is Il2CppObjectBase instObj) instPtr = instObj.Pointer;

            WriteAim(snapPtr, instPtr);

            _lastFrame = fc;
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[AIM] PrefixNyv EX: {e.Message}"); }
    }

    private static void WriteAim(IntPtr snapPtr, IntPtr instPtr)
    {
        Vector3 dir = AimbotModule.TargetDirection;
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;

        float minX = -89f, maxX = 89f;
        if (instPtr != IntPtr.Zero)
        {
            try
            {
                float mn = ReadFloat(instPtr, OFF_MIN_X);
                float mx = ReadFloat(instPtr, OFF_MAX_X);
                if (mx > mn) { minX = mn; maxX = mx; }
            }
            catch { }
        }
        pitch = Mathf.Clamp(pitch, minX, maxX);

        WriteFloat(snapPtr, OFF_AIM_X, yaw);
        WriteFloat(snapPtr, OFF_AIM_Y, pitch);

        Plugin.LogSource?.LogInfo($"[AIM] WriteAim yaw={yaw:F1} pitch={pitch:F1}");
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
