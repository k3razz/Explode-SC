using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace MyDynamicESP;

public static class WeaponLog
{
    public static bool Active = false;
    public static bool Enabled = true;

    private static Harmony _harmony;
    private static bool _applied;
    private static int _lastFrame;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        try
        {
            _harmony = new Harmony("com.you.dynamicesp.weaponlog");

            var gcType = FindType("Chillow.StandChillow.Inventory.Gun.GunController");
            if (gcType == null) gcType = FindTypeByShortName("GunController");

            var wcType = FindType("Chillow.StandChillow.Inventory.WeaponController");
            if (wcType == null) wcType = FindTypeByShortName("WeaponController");

            int patched = 0;

            if (gcType != null)
            {
                patched += Patch(gcType, "Update", nameof(Post_NoArgs));
                patched += Patch(gcType, "LateUpdate", nameof(Post_NoArgs));
                patched += Patch(gcType, "xaq", nameof(Post_NoArgs));
                patched += Patch(gcType, "xbb", nameof(Post_NoArgs));
                patched += Patch(gcType, "xax", nameof(Post_NoArgs));
                patched += Patch(gcType, "wxs", nameof(Post_NoArgs));
                patched += Patch(gcType, "wxt", nameof(Post_NoArgs));
                patched += Patch(gcType, "xbm", nameof(Post_NoArgs));
                patched += Patch(gcType, "xbh", nameof(Post_Xbh));
                patched += Patch(gcType, "wxm", nameof(Post_Wxm));
                patched += Patch(gcType, "wxj", nameof(Post_Wxj));
                patched += Patch(gcType, "xap", nameof(Post_Xap));
                patched += Patch(gcType, "wxl", nameof(Post_Wxl));
                patched += Patch(gcType, "xap", nameof(Post_Xap));
            }

            if (wcType != null)
            {
                patched += Patch(wcType, "Update", nameof(Post_NoArgs));
                patched += Patch(wcType, "LateUpdate", nameof(Post_NoArgs));
            }

            Plugin.LogSource?.LogInfo($"[WLOG] Patched {patched} methods");
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[WLOG] Apply EX: {e.Message}"); }
    }

    private static int Patch(Type t, string name, string postfixName)
    {
        try
        {
            var mi = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (mi == null) return 0;
            _harmony.Patch(mi, postfix: new HarmonyMethod(typeof(WeaponLog), postfixName));
            Plugin.LogSource?.LogInfo($"[WLOG] Patched {t.Name}.{name}");
            return 1;
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[WLOG] Patch {t.Name}.{name} EX: {e.Message}");
            return 0;
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

    private static void Write(string typeName, string method, string extra)
    {
        if (!Enabled || !Active) return;
        try { Plugin.LogSource?.LogInfo($"[WLOG] {typeName}.{method} {extra}"); } catch { }
    }

    private static string TypeName(object o) => o?.GetType().Name ?? "null";

    public static void Post_NoArgs(object __instance)
    {
        if (!Active) return;
        Write(TypeName(__instance), "?", "");
    }

    public static void Post_Xbh(object __instance, bool a, bool b)
    {
        if (!Active) return;
        Write(TypeName(__instance), "xbh", $"a={a} b={b}");
    }

    public static void Post_Wxm(object __instance, object a, float b, float c)
    {
        if (!Active) return;
        Write(TypeName(__instance), "wxm", $"cgs={TypeName(a)} b={b} c={c}");
    }

    public static void Post_Wxj(object __instance, object a)
    {
        if (!Active) return;
        Write(TypeName(__instance), "wxj", $"cgt={TypeName(a)}");
    }

    public static void Post_Xap(object __instance, byte a, int b, object c, bool d)
    {
        if (!Active) return;
        Write(TypeName(__instance), "xap", $"byte={a} int={b} hui={TypeName(c)} bool={d}");
    }

    public static void Post_Wxl(object __instance, object a)
    {
        if (!Active) return;
        Write(TypeName(__instance), "wxl", $"chf={TypeName(a)}");
    }
}
