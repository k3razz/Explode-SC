using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class AimbotModule
{
    public static bool Enabled = false;
    public static float Fov = 15f;
    public static float Smooth = 15f;   // 1 = мгновенный лок, 50 = очень плавно
    public static bool TargetTeammates = true;
    public static bool VisibilityCheck = false;
    public static bool DrawFovCircle = false;

    public static bool HasTarget = false;
    public static Vector3 TargetDirection = Vector3.zero;
    public static Vector3 TargetPoint = Vector3.zero;

    private static Tracked _currentTarget;
    private static float _targetTimer = 0f;

    // Та же геометрия, что в EspModule — центр ESP-бокса
    private const float HeadOffset = 1.8f;
    private const float FootOffset = 0.1f;

    public static Tracked CurrentTarget => _currentTarget;

    public static void Update(Camera cam, int localTeam, Dictionary<int, Tracked> tracked)
    {
        HasTarget = false;

        if (!Enabled) return;
        if (cam == null) return;

        Vector3 camPos, camForward;
        try { camPos = cam.transform.position; camForward = cam.transform.forward; } catch { return; }

        // 1. Пересчёт выбора цели раз в 0.25с (или если цель пропала)
        _targetTimer -= Time.deltaTime;
        if (_currentTarget == null || _currentTarget.Transform == null || _targetTimer <= 0f)
        {
            _targetTimer = 0.25f;
            _currentTarget = FindBestTarget(camPos, camForward, tracked);
        }

        if (_currentTarget == null || _currentTarget.Transform == null) return;

        // 2. Каждый кадр пересчитываем направление на АКТУАЛЬНЫЙ центр бокса цели.
        //    Это и есть "лок": камера следует за движущейся целью.
        Vector3 center = GetEspCenter(_currentTarget.Transform);
        Vector3 dir = center - camPos;
        if (dir.sqrMagnitude < 0.0001f) return;

        TargetPoint = center;
        TargetDirection = dir.normalized;
        HasTarget = true;
    }

    /// <summary>
    /// Центр ESP-бокса: середина между головой и ногами, теми же оффсетами, что рисует ESP.
    /// </summary>
    public static Vector3 GetEspCenter(Transform t)
    {
        if (t == null) return Vector3.zero;
        Vector3 head = t.position + Vector3.up * HeadOffset;
        Vector3 foot = t.position - Vector3.up * FootOffset;
        return (head + foot) * 0.5f;
    }

    private static Tracked FindBestTarget(Vector3 camPos, Vector3 camForward, Dictionary<int, Tracked> tracked)
    {
        Tracked best = null;
        float bestAngle = Fov;

        foreach (var t in tracked.Values)
        {
            if (t.Transform == null) continue;

            Vector3 center = GetEspCenter(t.Transform);
            Vector3 dir = center - camPos;
            float distSqr = dir.sqrMagnitude;
            if (distSqr < 0.01f) continue;

            float dot = Vector3.Dot(camForward, dir) / Mathf.Sqrt(distSqr);
            if (dot < 0.85f) continue; // ~32° пре-фильтр

            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            if (angle < bestAngle)
            {
                if (VisibilityCheck)
                {
                    float dlen = dir.magnitude;
                    if (Physics.Raycast(camPos, dir / dlen, out var hit, dlen))
                    {
                        if (hit.transform != t.Transform && !hit.transform.IsChildOf(t.Transform))
                            continue;
                    }
                }
                bestAngle = angle;
                best = t;
            }
        }
        return best;
    }
}
