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

    private const int OFF_AIM_CTRL = 0xD0;
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
            var aimCtrlType = FindType("Chillow.StandChillow.Player.Aim.AimController");
            if (aimCtrlType == null) aimCtrlType = FindTypeByShortName("AimController");
            if (aimCtrlType == null) { Plugin.LogSource?.LogError("[AIM] AimController not found"); return; }

            var nxw = aimCtrlType.GetMethod("nxw", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (nxw == null) { Plugin.LogSource?.LogError("[AIM] nxw not found"); return; }

            var harmony = new Harmony("com.you.dynamicesp.aimlock");
            harmony.Patch(nxw, prefix: new HarmonyMethod(typeof(AimControllerPatches), nameof(PrefixNxw)));
            Plugin.LogSource?.LogInfo($"[AIM] Patched {aimCtrlType.FullName}.nxw (Prefix)");
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

    public static void PrefixNxw()
    {
        try
        {
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            int fc = Time.frameCount;
            if (fc == _lastFrame) return;

            if (AimShared.LocalPlayer == null) return;

            IntPtr pcPtr = IntPtr.Zero;
            if (AimShared.LocalPlayer is Il2CppObjectBase pcObj) pcPtr = pcObj.Pointer;
            if (pcPtr == IntPtr.Zero) return;

            IntPtr aimCtrlPtr = Marshal.ReadIntPtr(pcPtr, OFF_AIM_CTRL);
            if (aimCtrlPtr == IntPtr.Zero) { Plugin.LogSource?.LogInfo("[AIM] PrefixNxw: aimCtrlPtr null"); return; }

            IntPtr snapPtr = Marshal.ReadIntPtr(aimCtrlPtr, OFF_AIM_SNAPSHOT);
            if (snapPtr == IntPtr.Zero) { Plugin.LogSource?.LogInfo("[AIM] PrefixNxw: snapPtr null"); return; }

            Vector3 dir = AimbotModule.TargetDirection;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;

            float minX = -89f, maxX = 89f;
            try
            {
                float mn = ReadFloat(aimCtrlPtr, OFF_MIN_X);
                float mx = ReadFloat(aimCtrlPtr, OFF_MAX_X);
                if (mx > mn) { minX = mn; maxX = mx; }
            }
            catch { }
            pitch = Mathf.Clamp(pitch, minX, maxX);

            WriteFloat(snapPtr, OFF_AIM_X, yaw);
            WriteFloat(snapPtr, OFF_AIM_Y, pitch);

            _lastFrame = fc;

            Plugin.LogSource?.LogInfo($"[AIM] PrefixNxw yaw={yaw:F1} pitch={pitch:F1} tgt={dir}");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[AIM] PrefixNxw EX: {e.Message}"); }
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
