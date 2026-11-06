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
    private const int OFF_GUN_PARAMS = 0x1A8;

    private const int OFF_GUN_ACCURATE_RANGE = 0x118;
    private const int OFF_GUN_RECOIL_CONTROL = 0xD8;
    private const int OFF_GUN_FIRE_RATE = 0xD4;
    private const int OFF_GUN_PEN_POWER = 0x12C;
    private const int OFF_GUN_ARMOR_PEN = 0x128;

    public static void Update()
    {
        if (!NoSpread && !NoRecoil && !RapidFire && !MaxWallbang && !MaxArmorPent) return;
        if (AimShared.LocalPlayer == null) return;

        try
        {
            IntPtr pcPtr = IntPtr.Zero;
            if (AimShared.LocalPlayer is Il2CppObjectBase pcObj) pcPtr = pcObj.Pointer;
            if (pcPtr == IntPtr.Zero) return;

            IntPtr wcPtr = Marshal.ReadIntPtr(pcPtr, OFF_PC_WEAPONRY);
            if (wcPtr == IntPtr.Zero) return;

            IntPtr currentPtr = Marshal.ReadIntPtr(wcPtr, OFF_WC_CURRENT);
            if (currentPtr == IntPtr.Zero) return;

            IntPtr gpPtr = Marshal.ReadIntPtr(currentPtr, OFF_GUN_PARAMS);
            if (gpPtr == IntPtr.Zero) return;

            if (NoSpread) Marshal.WriteInt32(gpPtr, OFF_GUN_ACCURATE_RANGE, 9999);
            if (NoRecoil) Marshal.WriteInt32(gpPtr, OFF_GUN_RECOIL_CONTROL, 0);
            if (RapidFire) Marshal.WriteInt32(gpPtr, OFF_GUN_FIRE_RATE, 9999);
            if (MaxWallbang) Marshal.WriteInt32(gpPtr, OFF_GUN_PEN_POWER, 9999);
            if (MaxArmorPent) WriteFloat(gpPtr, OFF_GUN_ARMOR_PEN, 100f);
        }
        catch { }
    }

    private static void WriteFloat(IntPtr ptr, int offset, float val)
    {
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(val), 0);
        Marshal.WriteInt32(ptr, offset, bits);
    }
}
