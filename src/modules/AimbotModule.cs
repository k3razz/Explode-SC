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
    public static bool DrawFovCircle = false; // по умолчанию выключено для FPS

    public static void Update(Camera cam, int localTeam, Dictionary<int, Tracked> tracked)
    {
        if (!Enabled || !AimbotState.Active) return;
        if (cam == null) return;

        Tracked best = null;
        float bestAngle = Fov;
        Vector3 camPos, camForward;
        try { camPos = cam.transform.position; camForward = cam.transform.forward; } catch { return; }

        foreach (var t in tracked.Values)
        {
            try
            {
                if (t.Transform == null) continue;
                if (!TargetTeammates && localTeam > 0 && t.Team == localTeam) continue;
                if (VisibilityCheck && !IsVisible(t, camPos)) continue;

                Vector3 aimPoint = GetAimPoint(t);
                Vector3 dir = (aimPoint - camPos).normalized;
                float angle = Vector3.Angle(camForward, dir);
                if (angle < bestAngle) { bestAngle = angle; best = t; }
            }
            catch { }
        }

        if (best == null) return;

        Vector3 target = GetAimPoint(best);
        Quaternion targetRot = Quaternion.LookRotation((target - camPos).normalized);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Smooth * Time.deltaTime);
    }

    // Центр ESP-бокса: середина между головой и ногами
    private static Vector3 GetAimPoint(Tracked t)
    {
        Vector3 head = t.Transform.position + Vector3.up * 1.8f;
        Vector3 foot = t.Transform.position;
        return (head + foot) * 0.5f + Vector3.up * 0.1f;
    }

    private static bool IsVisible(Tracked t, Vector3 camPos)
    {
        try
        {
            Vector3 target = GetAimPoint(t);
            Vector3 d = target - camPos;
            float dist = d.magnitude;
            if (dist < 0.1f) return true;
            if (Physics.Raycast(camPos, d.normalized, out var hit, dist))
                return hit.transform == t.Transform || hit.transform.IsChildOf(t.Transform);
            return true;
        }
        catch { return true; }
    }
}
