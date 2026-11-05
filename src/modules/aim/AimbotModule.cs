using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace MyDynamicESP;

public enum AimBone
{
    Head = 0,
    Neck = 1,
    Body = 2
}

public static class AimbotModule
{
    public static bool Enabled = false;
    public static float Fov = 90f;
    public static bool DrawFovCircle = false;
    public static float Sensitivity = 0.15f;
    public static AimBone Bone = AimBone.Body;

    public static bool HasTarget = false;
    public static Vector3 TargetDirection = Vector3.zero;
    public static Vector3 TargetPoint = Vector3.zero;
    public static float TargetAngleX = 0f;
    public static float TargetAngleY = 0f;

    private static Tracked _currentTarget;
    private const float HeadOffset = 1.5f;
    private const float BodyOffset = 0.9f;
    private const float NeckOffset = 1.35f;

    private static bool _bipedResolved;
    private static Il2CppSystem.Type _bipedMapType;
    private static Il2CppSystem.Reflection.MethodInfo _nwhMethod;

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

        _currentTarget = FindBestTarget(camPos, camForward, tracked);

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

        if (Bone == AimBone.Body)
            return t.Transform.position + Vector3.up * BodyOffset;

        if (t.BipedMap != null)
        {
            int boneIdx = Bone == AimBone.Head ? 0 : 1;
            var pos = TryGetBonePosition(t.BipedMap, boneIdx);
            if (pos.HasValue) return pos.Value;
        }

        float fallback = Bone == AimBone.Head ? HeadOffset : NeckOffset;
        return t.Transform.position + Vector3.up * fallback;
    }

    private static Vector3? TryGetBonePosition(object bipedMap, int boneIndex)
    {
        ResolveBiped();
        if (_nwhMethod == null) return null;

        try
        {
            var bipedObj = bipedMap as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase;
            if (bipedObj == null) return null;

            var paramType = _nwhMethod.GetParameters()[0].ParameterType;
            object headVal;
            try
            {
                headVal = Il2CppSystem.Enum.ToObject(paramType, boneIndex);
            }
            catch
            {
                headVal = Il2CppSystem.Convert.ChangeType(boneIndex, paramType);
            }

            var args = new Il2CppSystem.Object[1];
            args[0] = (Il2CppSystem.Object)headVal;

            var result = _nwhMethod.Invoke(bipedObj, args);
            if (result == null) return null;

            var tr = result.TryCast<Transform>();
            if (tr == null) return null;
            return tr.position;
        }
        catch { return null; }
    }

    private static void ResolveBiped()
    {
        if (_bipedResolved) return;
        _bipedResolved = true;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType("Il2CppChillow.StandChillow.Player.BipedMap")
                     ?? asm.GetType("Chillow.StandChillow.Player.BipedMap");
                if (t == null) continue;
                _bipedMapType = Il2CppInterop.Runtime.Il2CppType.From(t);
                _nwhMethod = _bipedMapType.GetMethod("nwh",
                    (Il2CppSystem.Reflection.BindingFlags)(int)(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance));
                if (_nwhMethod != null) return;
            }
            catch { }
        }
    }

    private static Tracked FindBestTarget(Vector3 camPos, Vector3 camForward, Dictionary<int, Tracked> tracked)
    {
        Tracked best = null;
        float bestAngle = Fov;

        foreach (var t in tracked.Values)
        {
            if (t.Transform == null) continue;
            try { if (!t.Transform.gameObject.activeInHierarchy) continue; } catch { }

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