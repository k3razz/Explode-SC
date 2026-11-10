using UnityEngine;
using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;

namespace MyDynamicESP;

public enum AntiAimYaw { Up = 0, Down = 1 }
public enum AntiAimDir { Left = 0, Right = 1, Forward = 2, Backward = 3 }

public static class AntiAimModule
{
    public static bool Enabled = false;
    public static AntiAimYaw Yaw = AntiAimYaw.Up;
    public static AntiAimDir Direction = AntiAimDir.Backward;
    public static float YawAngle = 89f;

    private static Transform _headTransform = null;
    private static Transform _spineTransform = null;
    private static Transform _neckTransform = null;
    private static Quaternion _originalHeadRot;
    private static Quaternion _originalSpineRot;
    private static Quaternion _originalNeckRot;
    private static bool _isActive = false;
    private static float _disableTimer = 0f;

    public static void Update(Component localPlayer)
    {
        if (!Enabled || localPlayer == null) return;

        if (Input.GetMouseButtonDown(0) || KeybindState.AimbotActive)
        {
            _disableTimer = 0.05f;
            Restore();
            return;
        }

        if (_disableTimer > 0f)
        {
            _disableTimer -= Time.deltaTime;
            if (_disableTimer <= 0f) _disableTimer = 0f;
            return;
        }

        if (_headTransform == null || _spineTransform == null)
        {
            FindBones(localPlayer);
            if (_headTransform == null || _spineTransform == null) return;
            _originalHeadRot = _headTransform.localRotation;
            _originalSpineRot = _spineTransform.localRotation;
            if (_neckTransform != null) _originalNeckRot = _neckTransform.localRotation;
        }

        ApplyAntiAim();
    }

    private static void FindBones(Component player)
    {
        try
        {
            var anim = player.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                _headTransform = anim.GetBoneTransform(HumanBodyBones.Head);
                _spineTransform = anim.GetBoneTransform(HumanBodyBones.Spine);
                _neckTransform = anim.GetBoneTransform(HumanBodyBones.Neck);
            }
        }
        catch { }
    }

    private static void ApplyAntiAim()
    {
        if (_headTransform == null) return;

        float yawAngle = (Yaw == AntiAimYaw.Up) ? YawAngle : -YawAngle;
        float dirAngle = 0f;
        switch (Direction)
        {
            case AntiAimDir.Left: dirAngle = -90f; break;
            case AntiAimDir.Right: dirAngle = 90f; break;
            case AntiAimDir.Forward: dirAngle = 0f; break;
            case AntiAimDir.Backward: dirAngle = 180f; break;
        }

        Quaternion yawRot = Quaternion.Euler(yawAngle, 0f, 0f);
        Quaternion dirRot = Quaternion.Euler(0f, dirAngle, 0f);

        _headTransform.localRotation = _originalHeadRot * dirRot * yawRot;

        if (_neckTransform != null)
            _neckTransform.localRotation = _originalNeckRot * dirRot * Quaternion.Euler(yawAngle * 0.6f, 0f, 0f);
        if (_spineTransform != null)
            _spineTransform.localRotation = _originalSpineRot * dirRot * Quaternion.Euler(yawAngle * 0.3f, 0f, 0f);
    }

    private static void Restore()
    {
        if (_headTransform != null) _headTransform.localRotation = _originalHeadRot;
        if (_neckTransform != null) _neckTransform.localRotation = _originalNeckRot;
        if (_spineTransform != null) _spineTransform.localRotation = _originalSpineRot;
    }

    public static void Clear()
    {
        Restore();
        _headTransform = null;
        _neckTransform = null;
        _spineTransform = null;
        _isActive = false;
        _disableTimer = 0f;
    }
}
