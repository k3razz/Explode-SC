using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public enum AimBone
{
    Head = 0,
    Neck = 1,
    Chest = 2
}

public static class AimbotModule
{
    public static bool Enabled = false;
    public static float Fov = 20f;
    public static bool DrawFovCircle = false;
    public static float Sensitivity = 0.15f;
    public static AimBone Bone = AimBone.Head;

    public static bool HasTarget = false;
    public static Vector3 TargetDirection = Vector3.zero;
    public static Vector3 TargetPoint = Vector3.zero;
    public static float TargetAngleX = 0f;
    public static float TargetAngleY = 0f;

    private static Tracked _currentTarget;
    private const float HeadOffset = 1.6f;
    private const float ChestOffset = 1.1f;
    private const float NeckOffset = 1.45f;

    private const int OFF_BIPED_HEAD = 0x20;
    private const int OFF_BIPED_NECK = 0x28;
    private const int OFF_BIPED_SPINE = 0x30;

    public static Tracked CurrentTarget => _currentTarget;

    public static void Update(Camera cam, int localTeam, Dictionary<int, Tracked> tracked)
    {
        HasTarget = false;
        TargetAngleX = 0f;
        TargetAngleY = 0f;

        if (!Enabled) return;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 camForward = cam.transform.forward;

        _currentTarget = FindBestTarget(camPos, camForward, tracked, localTeam);

        if (_currentTarget == null || _currentTarget.Transform == null) return;

        Vector3 center = GetAimPoint(_currentTarget);
        Vector3 dir = center - camPos;
        if (dir.sqrMagnitude < 0.0001f) return;

        TargetPoint = center;
        TargetDirection = dir.normalized;
        HasTarget = true;

        Vector3 local = cam.transform.InverseTransformDirection(TargetDirection);
        if (local.z <= 0.001f) { HasTarget = false; return; }

        TargetAngleX = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        TargetAngleY = -Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
    }

    public static Vector3 GetAimPoint(Tracked t)
    {
        if (t == null || t.Transform == null) return Vector3.zero;

        if (t.BipedMap != null)
        {
            int offset = Bone == AimBone.Head ? OFF_BIPED_HEAD
                       : Bone == AimBone.Neck ? OFF_BIPED_NECK
                       : OFF_BIPED_SPINE;

            var tr = ReadTransform(t.BipedMap, offset);
            if (tr != null) return tr.position;
        }

        float fallback = Bone == AimBone.Head ? HeadOffset
                       : Bone == AimBone.Neck ? NeckOffset
                       : ChestOffset;
        return t.Transform.position + Vector3.up * fallback;
    }

    private static Transform ReadTransform(object obj, int offset)
    {
        try
        {
            if (!(obj is Il2CppObjectBase baseObj)) return null;
            IntPtr ptr = baseObj.Pointer;
            if (ptr == IntPtr.Zero) return null;

            IntPtr bonePtr = Marshal.ReadIntPtr(ptr, offset);
            if (bonePtr == IntPtr.Zero) return null;

            var tr = new Transform(bonePtr);
            if (tr == null || tr.Pointer == IntPtr.Zero) return null;
            return tr;
        }
        catch { return null; }
    }

    private static Tracked FindBestTarget(Vector3 camPos, Vector3 camForward,
                                           Dictionary<int, Tracked> tracked, int localTeam)
    {
        Tracked best = null;
        float bestAngle = Fov;

        foreach (var t in tracked.Values)
        {
            if (t.Transform == null) continue;
            try { if (!t.Transform.gameObject.activeInHierarchy) continue; } catch { }

            // === ПРОПУСКАЕМ ТИММЕЙТОВ ===
            // Если команда определена у обоих и совпадает — не целимся
            if (localTeam > 0 && t.Team > 0 && t.Team == localTeam) continue;

            Vector3 center = GetAimPoint(t);
            Vector3 dir = center - camPos;
            if (dir.sqrMagnitude < 0.01f) continue;

            float angle = Vector3.Angle(camForward, dir);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = t;
            }
        }
        return best;
    }
}
