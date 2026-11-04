using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace MyDynamicESP;

[HarmonyPatch]
public static class AimControllerPatches
{
    // Патчим Controller.Tick (AimController : Controller) — Postfix.
    // В Postfix holder уже перезаписан AimController'ом из input, мы его перезаписываем на цель.
    [HarmonyPatch(typeof(Controller), "Tick")]
    [HarmonyPostfix]
    public static void Tick_Postfix(object __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            // Проверяем, что это AimController (по имени типа — надёжнее, чем cast)
            string typeName = __instance.GetType().Name;
            if (typeName != "AimController") return;

            var holder = AimShared.MainCameraHolder;
            if (holder == null) return;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            holder.rotation = Quaternion.Euler(e.x, e.y, 0f);
        }
        catch { }
    }

    // Fallback: если Controller.Tick не существует (не virtual), пробуем LateUpdate
    [HarmonyPatch(typeof(Controller), "LateUpdate")]
    [HarmonyPostfix]
    public static void LateUpdate_Postfix(object __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            string typeName = __instance.GetType().Name;
            if (typeName != "AimController") return;

            var holder = AimShared.MainCameraHolder;
            if (holder == null) return;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            holder.rotation = Quaternion.Euler(e.x, e.y, 0f);
        }
        catch { }
    }
}
