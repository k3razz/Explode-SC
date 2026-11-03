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

                Vector3 hp = GetHeadPosition(t);
                Vector3 dir = (hp - camPos).normalized;
                float angle = Vector3.Angle(camForward, dir);
                if (angle < bestAngle) { bestAngle = angle; best = t; }
            }
            catch { }
        }

        if (best == null) return;

        Vector3 targetHead = GetHeadPosition(best);
        Quaternion targetRot = Quaternion.LookRotation((targetHead - camPos).normalized);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, Smooth * Time.deltaTime);
    }

    private static bool IsVisible(Tracked t, Vector3 camPos)
    {
        try
        {
            Vector3 hp = GetHeadPosition(t);
            Vector3 d = hp - camPos;
            float dist = d.magnitude;
            if (dist < 0.1f) return true;
            if (Physics.Raycast(camPos, d.normalized, out var hit, dist))
                return hit.transform == t.Transform || hit.transform.IsChildOf(t.Transform);
            return true;
        }
        catch { return true; }
    }

    private static Vector3 GetHeadPosition(Tracked t)
    {
        try
        {
            if (t.BipedMap != null)
            {
                var nwh = t.BipedMap.GetType().GetMethod("nwh", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nwh != null)
                {
                    var h = nwh.Invoke(t.BipedMap, new object[] { 0 });
                    if (h is Transform ht) return ht.position;
                }
            }
        }
        catch { }
        return t.Transform.position + Vector3.up * 1.6f;
    }
}
