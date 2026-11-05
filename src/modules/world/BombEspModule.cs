using UnityEngine;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public static class BombEspModule
{
    public static bool Planted = false;
    public static Vector3 Position = Vector3.zero;
    public static string Site = "-";
    public static float TimeLeft = 40f;
    public static float FuseTime = 40f;

    private static Il2CppSystem.Type _bombManagerType;
    private static Il2CppSystem.Type _plantedBombType;
    private static Il2CppSystem.Type _bombSiteType;
    private static bool _resolved;
    private static Component _cachedBombManager;
    private static float _plantedAt = -1f;
    private static float _lastCheck;
    private const float CheckInterval = 0.25f;

    private const int OFF_BM_CGQT = 0x78;

    public static void Reset()
    {
        Planted = false;
        Position = Vector3.zero;
        Site = "-";
        TimeLeft = FuseTime;
        _plantedAt = -1f;
    }

    public static void Update()
    {
        float now = Time.time;
        if (now - _lastCheck < CheckInterval) return;
        _lastCheck = now;

        try
        {
            ResolveTypes();
            if (_bombManagerType == null) return;

            IntPtr bmPtr = FindBombManagerPtr();
            if (bmPtr == IntPtr.Zero)
            {
                if (Planted) Reset();
                return;
            }

            IntPtr plantedPtr = Marshal.ReadIntPtr(bmPtr, OFF_BM_CGQT);
            if (plantedPtr == IntPtr.Zero)
            {
                if (Planted) Reset();
                return;
            }

            var plantedTr = TryGetTransform(plantedPtr);
            if (plantedTr == null)
            {
                if (Planted) Reset();
                return;
            }

            if (!Planted)
            {
                _plantedAt = now;
                Planted = true;
                Plugin.LogSource?.LogInfo("[BOMB] Planted detected");
            }

            Position = plantedTr.position;
            Site = DetectSite(Position, bmPtr);

            float elapsed = now - _plantedAt;
            float remain = Mathf.Max(0f, FuseTime - elapsed);
            TimeLeft = remain;

            if (remain <= 0f && Planted)
            {
                Planted = false;
                _plantedAt = -1f;
            }
        }
        catch { }
    }

    [HideFromIl2Cpp]
    static IntPtr FindBombManagerPtr()
    {
        try
        {
            if (_cachedBombManager != null && _cachedBombManager.Pointer != IntPtr.Zero)
                return _cachedBombManager.Pointer;
        }
        catch { }

        try
        {
            var obj = UnityEngine.Object.FindObjectOfType(_bombManagerType);
            if (obj != null)
            {
                _cachedBombManager = obj.TryCast<Component>();
                if (_cachedBombManager != null && _cachedBombManager.Pointer != IntPtr.Zero)
                    return _cachedBombManager.Pointer;
            }
        }
        catch { }

        try
        {
            var flags = (Il2CppSystem.Reflection.BindingFlags)(int)(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            var instProp = _bombManagerType.GetProperty("Instance", flags);
            if (instProp != null)
            {
                var v = instProp.GetValue(null);
                if (v != null)
                {
                    var objBase = v.TryCast<Il2CppObjectBase>();
                    if (objBase != null && objBase.Pointer != IntPtr.Zero)
                    {
                        var comp = v.TryCast<Component>();
                        if (
