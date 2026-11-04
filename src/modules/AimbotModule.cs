using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public static class AimbotModule
{
    public static bool Enabled = false;
    public static float Fov = 15f;
    public static float Smooth = 15f;
    public static bool TargetTeammates = false;
    public static bool VisibilityCheck = false;
    public static bool DrawFovCircle = false;

    public static void Update(Camera cam, int localTeam, Dictionary<int, Tracked> tracked)
    {
        // Если выключен или не зажат бинд — выходим сразу
        if (!Enabled || !KeybindState.AimbotActive) return;
        if (cam == null) return;

        Vector3 camPos, camForward;
        try { camPos = cam.transform.position; camForward = cam.transform.forward; } catch { return; }

        Tracked best = null;
        float bestAngle = Fov;

        // Единственный цикл по целям — без лишних вызовов
        foreach (var t in tracked.Values)
        {
            if (t.Transform == null) continue;
            if (!TargetTeammates && localTeam > 0 && t.Team == localTeam) continue;

            Vector3 hp = t.Transform.position + Vector3.up * 1.3f; // центр тела
            Vector3 dir = hp - camPos;
            float distSqr = dir.sqrMagnitude;
            if (distSqr < 0.01f) continue;

            // Угол без sqrt (через dot), это быстрее Vector3.Angle
            float dot = Vector3.Dot(camForward, dir) / Mathf.Sqrt(distSqr);
            if (dot < 0.9f) continue; // отсекаем всё, что >25° (быстрый пре-фильтр)

            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = t;
            }
        }

        if (best == null) return;

        // Проверка видимости — ТОЛЬКО для выбранной цели, один Raycast
        if (VisibilityCheck)
        {
            Vector3 hp = best.Transform.position + Vector3.up * 1.3f;
            Vector3 d = hp - camPos;
            float dlen = d.magnitude;
            if (Physics.Raycast(camPos, d / dlen, out var hit, dlen))
            {
                if (hit.transform != best.Transform && !hit.transform.IsChildOf(best.Transform))
                    return; // стена — не наводимся
            }
        }

        Vector3 target = best.Transform.position + Vector3.up * 1.3f;
        Quaternion targetRot = Quaternion.LookRotation((target - camPos).normalized);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Smooth * Time.deltaTime);
    }
}
