using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public static class WeaponCheats
{
    public static bool NoSpread = false;
    public static bool NoRecoil = false;
    public static bool RapidFire = false;
    public static bool MaxWallbang = false;
    public static bool MaxArmorPent = false;

    private const int OFF_PC_WEAPONRY = 0xD8;
    private const int OFF_WC_CURRENT = 0x98;
    private const int OFF_WC_OWNER = 0x20;
    private const int OFF_GUN_PARAMS = 0x1A8;

    private const int OFF_GUN_ACCURATE_RANGE = 0x118;
    private const int OFF_GUN_RECOIL_CONTROL = 0xD8;
    private const int OFF_GUN_FIRE_RATE = 0xD4;
    private const int OFF_GUN_PEN_POWER = 0x12C;
    private const int OFF_GUN_ARMOR_PEN = 0x128;

    private static float _logT;

    public static void Update()
    {
        if (!NoSpread && !NoRecoil && !RapidFire && !MaxWallbang && !MaxArmorPent) return;
        if (AimShared.LocalPlayer == null) { Log("pc null"); return; }

        try
        {
            IntPtr pcPtr = IntPtr.Zero;
            if (AimShared.LocalPlayer is Il2CppObjectBase pcObj) pcPtr = pcObj.Pointer;
            if (pcPtr == IntPtr.Zero) { Log("pcPtr null"); return; }

            IntPtr wcPtr = Marshal.ReadIntPtr(pcPtr, OFF_PC_WEAPONRY);
            if (wcPtr == IntPtr.Zero) { Log("wcPtr null @0xD8"); return; }

            IntPtr currentPtr = Marshal.ReadIntPtr(wcPtr, OFF_WC_CURRENT);
            if (currentPtr == IntPtr.Zero) { Log("currentPtr null @0x98"); return; }

            IntPtr gpPtr = Marshal.ReadIntPtr(currentPtr, OFF_GUN_PARAMS);
            if (gpPtr == IntPtr.Zero) { Log("gpPtr null @0x1A8 (not Gun?)"); return; }

            Log($"ok gp=0x{gpPtr.ToInt64():X}");

            if (NoSpread) Marshal.WriteInt32(gpPtr, OFF_GUN_ACCURATE_RANGE, 9999);
            if (NoRecoil) Marshal.WriteInt32(gpPtr, OFF_GUN_RECOIL_CONTROL, 0);
            if (RapidFire) Marshal.WriteInt32(gpPtr, OFF_GUN_FIRE_RATE, 9999);
            if (MaxWallbang) Marshal.WriteInt32(gpPtr, OFF_GUN_PEN_POWER, 9999);
            if (MaxArmorPent) WriteFloat(gpPtr, OFF_GUN_ARMOR_PEN, 100f);
        }
        catch (Exception e) { Log($"EX: {e.Message}"); }
    }

    private static void WriteFloat(IntPtr ptr, int offset, float val)
    {
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(val), 0);
        Marshal.WriteInt32(ptr, offset, bits);
    }

    private static void Log(string msg)
    {
        _logT += Time.deltaTime;
        if (_logT < 1f) return;
        _logT = 0f;
        Plugin.LogSource?.LogInfo($"[WCH] {msg}");
    }
}
