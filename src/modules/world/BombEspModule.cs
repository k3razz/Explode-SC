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
                        if (comp != null) _cachedBombManager = comp;
                        return objBase.Pointer;
                    }
                }
            }

            foreach (var n in new[] { "_instance", "instance", "m_instance", "<Instance>k__BackingField" })
            {
                var f = _bombManagerType.GetField(n, flags);
                if (f != null)
                {
                    var v = f.GetValue(null);
                    if (v != null)
                    {
                        var objBase = v.TryCast<Il2CppObjectBase>();
                        if (objBase != null && objBase.Pointer != IntPtr.Zero)
                        {
                            var comp = v.TryCast<Component>();
                            if (comp != null) _cachedBombManager = comp;
                            return objBase.Pointer;
                        }
                    }
                }
            }

            var baseType = _bombManagerType.BaseType;
            int depth = 0;
            while (baseType != null && depth < 8)
            {
                var instProp2 = baseType.GetProperty("Instance", flags);
                if (instProp2 != null)
                {
                    var v = instProp2.GetValue(null);
                    if (v != null)
                    {
                        var objBase = v.TryCast<Il2CppObjectBase>();
                        if (objBase != null && objBase.Pointer != IntPtr.Zero) return objBase.Pointer;
                    }
                }
                baseType = baseType.BaseType;
                depth++;
            }
        }
        catch { }

        return IntPtr.Zero;
    }

    [HideFromIl2Cpp]
    static Transform TryGetTransform(IntPtr ptr)
    {
        try
        {
            var comp = new Component(ptr);
            if (comp != null && comp.Pointer != IntPtr.Zero) return comp.transform;
        }
        catch { }
        try
        {
            var mb = new MonoBehaviour(ptr);
            if (mb != null && mb.Pointer != IntPtr.Zero) return mb.transform;
        }
        catch { }
        return null;
    }

    [HideFromIl2Cpp]
    static string DetectSite(Vector3 bombPos, IntPtr bmPtr)
    {
        try
        {
            var flags = (Il2CppSystem.Reflection.BindingFlags)(int)(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var sitesProp = _bombManagerType.GetProperty("cgqn", flags);
            if (sitesProp == null) return "-";

            var bmObj = new Component(bmPtr);
            if (bmObj == null) return "-";

            var sitesArr = sitesProp.GetValue(bmObj.Cast<Il2CppSystem.Object>());
            if (sitesArr == null) return "-";

            var arr = sitesArr.TryCast<Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Component>>();
            if (arr == null) return "-";

            float best = float.MaxValue;
            string bestName = "-";
            for (int i = 0; i < arr.Length; i++)
            {
                try
                {
                    var s = arr[i];
                    if (s == null) continue;
                    float d = Vector3.Distance(bombPos, s.transform.position);
                    if (d < best) { best = d; bestName = s.gameObject.name; }
                }
                catch { }
            }
            if (bestName.Contains("A") || bestName.Contains("0")) return "A";
            if (bestName.Contains("B") || bestName.Contains("1")) return "B";
            return bestName;
        }
        catch { return "-"; }
    }

    [HideFromIl2Cpp]
    static Type FindTypeInAllAssemblies(string shortName, string[] fullNames)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var full in fullNames)
            {
                try { var t = asm.GetType(full); if (t != null) return t; } catch { }
            }
        }
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

    [HideFromIl2Cpp]
    static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            if (_bombManagerType == null)
            {
                var t = FindTypeInAllAssemblies("BombManager", new[] {
                    "Il2CppChillow.StandChillow.Inventory.Bomb.BombManager",
                    "Chillow.StandChillow.Inventory.Bomb.BombManager",
                    "Il2CppChillow.StandChillow.Bomb.BombManager",
                    "BombManager"
                });
                if (t != null) _bombManagerType = Il2CppType.From(t);
            }
            if (_plantedBombType == null)
            {
                var t = FindTypeInAllAssemblies("PlantedBombController", new[] {
                    "Il2CppChillow.StandChillow.Inventory.Bomb.PlantedBombController",
                    "Chillow.StandChillow.Inventory.Bomb.PlantedBombController",
                    "PlantedBombController"
                });
                if (t != null) _plantedBombType = Il2CppType.From(t);
            }
            if (_bombSiteType == null)
            {
                var t = FindTypeInAllAssemblies("BombSite", new[] {
                    "Il2CppChillow.StandChillow.Inventory.Bomb.BombSite",
                    "Chillow.StandChillow.Inventory.Bomb.BombSite",
                    "BombSite"
                });
                if (t != null) _bombSiteType = Il2CppType.From(t);
            }

            Plugin.LogSource?.LogInfo($"[BOMB] Resolve: BombManager={_bombManagerType != null}, PlantedBomb={_plantedBombType != null}, BombSite={_bombSiteType != null}");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[BOMB] ResolveTypes EX: {e.Message}"); }
    }
}
