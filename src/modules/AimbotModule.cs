using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class AimbotModule
{
    public static bool Enabled = false;
    public static float Fov = 15f;
    public static float Smooth = 15f;
    public static bool TargetTeammates = true;  // всегда true — аим на всех (поле оставлено для совместимости)
    public static bool VisibilityCheck = false;
    public static bool DrawFovCircle = false;

    public static bool HasTarget = false;
    public static Vector3 TargetDirection = Vector3.zero;

    private static Tracked _currentTarget;
    private static float _targetTimer = 0f;

    public static void Update(Camera cam, int localTeam, Dictionary<int, Tracked> tracked)
    {
        HasTarget = false;

        if (!Enabled || !KeybindState.AimbotActive) return;
        if (cam == null) return;

        Vector3 camPos, camForward;
        try { camPos = cam.transform.position; camForward = cam.transform.forward; } catch { return; }

        _targetTimer -= Time.deltaTime;
        if (_currentTarget == null || _currentTarget.Transform == null || _targetTimer <= 0f)
        {
            _targetTimer = 0.25f;
            _currentTarget = FindBestTarget(camPos, camForward, tracked);
        }

        if (_currentTarget == null || _currentTarget.Transform == null) return;

        Vector3 head = _currentTarget.Transform.position + Vector3.up * 1.8f;
        Vector3 foot = _currentTarget.Transform.position;
        Vector3 center = (head + foot) * 0.5f + Vector3.up * 0.1f;

        Vector3 dir = center - camPos;
        if (dir.sqrMagnitude < 0.01f) return;

        TargetDirection = dir.normalized;
        HasTarget = true;
    }

    /// <summary>
    /// Выбираем ближайшего по углу в пределах FOV. Без фильтра по команде —
    /// целься в любого бота/игрока, пока аимбот активен.
    /// </summary>
    private static Tracked FindBestTarget(Vector3 camPos, Vector3 camForward, Dictionary<int, Tracked> tracked)
    {
        Tracked best = null;
        float bestAngle = Fov;

        foreach (var t in tracked.Values)
        {
            if (t.Transform == null) continue;

            Vector3 head = t.Transform.position + Vector3.up * 1.8f;
            Vector3 foot = t.Transform.position;
            Vector3 center = (head + foot) * 0.5f + Vector3.up * 0.1f;

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
