using UnityEngine;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;

namespace MyDynamicESP;

public static class BombModule
{
    public static bool Planted = false;
    public static Vector3 Position = Vector3.zero;
    public static string Site = "?";
    public static float TimeLeft = 0f;
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
        Site = "?";
        TimeLeft = 0f;
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

            var bm = FindBombManager();
            if (bm == null)
            {
                if (Planted) Reset();
                return;
            }

            IntPtr plantedPtr = Marshal.ReadIntPtr(bm.Pointer, OFF_BM_CGQT);
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
            Site = DetectSite(Position, bm);

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
    static Component FindBombManager()
    {
        if (_cachedBombManager != null)
        {
            try { if (_cachedBombManager.Pointer != IntPtr.Zero) return _cachedBombManager; } catch { }
            _cachedBombManager = null;
        }
        try
        {
            var obj = UnityEngine.Object.FindObjectOfType(_bombManagerType);
            if (obj != null) _cachedBombManager = obj.TryCast<Component>();
        }
        catch { }
        return _cachedBombManager;
    }

    [HideFromIl2Cpp]
    static string DetectSite(Vector3 bombPos, Component bm)
    {
        try
        {
            var sitesProp = _bombManagerType.GetProperty("cgqn",
                (Il2CppSystem.Reflection.BindingFlags)(int)(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
            if (sitesProp == null) return "?";
            var sitesArr = sitesProp.GetValue(bm.Cast<Il2CppSystem.Object>());
            if (sitesArr == null) return "?";
            var arr = sitesArr.TryCast<Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Component>>();
            if (arr == null) return "?";
            float best = float.MaxValue;
            string bestName = "?";
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
        catch { return "?"; }
    }

    [HideFromIl2Cpp]
    static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (_bombManagerType == null)
                    {
                        var t = asm.GetType("Il2CppChillow.StandChillow.Inventory.Bomb.BombManager")
                             ?? asm.GetType("Chillow.StandChillow.Inventory.Bomb.BombManager")
                             ?? asm.GetType("BombManager");
                        if (t != null) _bombManagerType = Il2CppType.From(t);
                    }
                    if (_plantedBombType == null)
                    {
                        var t = asm.GetType("Il2CppChillow.StandChillow.Inventory.Bomb.PlantedBombController")
                             ?? asm.GetType("Chillow.StandChillow.Inventory.Bomb.PlantedBombController")
                             ?? asm.GetType("PlantedBombController");
                        if (t != null) _plantedBombType = Il2CppType.From(t);
                    }
                    if (_bombSiteType == null)
                    {
                        var t = asm.GetType("Il2CppChillow.StandChillow.Inventory.Bomb.BombSite")
                             ?? asm.GetType("Chillow.StandChillow.Inventory.Bomb.BombSite")
                             ?? asm.GetType("BombSite");
                        if (t != null) _bombSiteType = Il2CppType.From(t);
                    }
                }
                catch { }
            }
            Plugin.LogSource?.LogInfo($"[BOMB] Resolve: BombManager={_bombManagerType != null}, PlantedBomb={_plantedBombType != null}, BombSite={_bombSiteType != null}");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[BOMB] ResolveTypes EX: {e.Message}"); }
    }
}
