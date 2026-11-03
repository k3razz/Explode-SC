using UnityEngine;
using System.Collections.Generic;

namespace MyDynamicESP;

public class Tracked
{
    public Transform Transform;
    public bool IsBot;
    public int Team;
    public string Name;
    public object BipedMap;
}

public static class EspModule
{
    public static readonly List<Tracked> ToDraw = new List<Tracked>();
    public static readonly Dictionary<int, Tracked> TrackedDict = new Dictionary<int, Tracked>();
    public static readonly List<int> SeenList = new List<int>();

    public static bool BoxesEnabled = true;
    public static bool NamesEnabled = true;
    public static bool DistanceEnabled = true;

    public const float HeadOffset = 1.8f;
    public const float FootOffset = 0.1f;
    public const float BoxWidthRatio = 0.4f;
    public const float NameMaxDist = 100f;

    public static void Draw(Texture2D whiteTex)
    {
        var cam = Camera.main;
        if (cam == null) return;

        foreach (var t in ToDraw)
        {
            try
            {
                if (t.Transform == null) continue;
                Vector3 hw = t.Transform.position + Vector3.up * HeadOffset;
                Vector3 fw = t.Transform.position - Vector3.up * FootOffset;
                Vector3 sh = cam.WorldToScreenPoint(hw);
                Vector3 sf = cam.WorldToScreenPoint(fw);
                if (sh.z < 0 || sf.z < 0) continue;

                float x = sh.x;
                float top = Screen.height - sh.y;
                float bottom = Screen.height - sf.y;
                float height = Mathf.Abs(bottom - top);
                float width = height * BoxWidthRatio;
                if (width < 4f) width = 4f;
                if (height < 8f) height = 8f;
                float left = x - width / 2f;
                float dist = Vector3.Distance(cam.transform.position, t.Transform.position);

                if (BoxesEnabled)
                {
                    float th = 2f;
                    Color fill = t.Team == 1 ? new Color(1f, 0f, 0f, 0.12f) : new Color(0f, 0.5f, 1f, 0.12f);
                    Color outl = t.Team == 1 ? Color.red : Color.cyan;
                    GUI.color = fill;
                    GUI.DrawTexture(new Rect(left, top, width, height), whiteTex);
                    GUI.color = outl;
                    GUI.DrawTexture(new Rect(left, top, width, th), whiteTex);
                    GUI.DrawTexture(new Rect(left, top + height - th, width, th), whiteTex);
                    GUI.DrawTexture(new Rect(left, top, th, height), whiteTex);
                    GUI.DrawTexture(new Rect(left + width - th, top, th, height), whiteTex);
                    GUI.color = Color.white;
                }

                if (NamesEnabled && dist < NameMaxDist)
                {
                    string lbl = t.Name + (DistanceEnabled ? $" [{dist:F0}m]" : "");
                    float lw = Mathf.Min(lbl.Length * 7.5f + 10f, 280f);
                    float ll = x - lw / 2f;
                    GUI.color = new Color(0f, 0f, 0f, 0.65f);
                    GUI.DrawTexture(new Rect(ll - 2f, top - 22f, lw + 4f, 20f), whiteTex);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(ll, top - 21f, lw, 18f), lbl);
                }
            }
            catch { }
        }
    }

    public static bool IsTeamVisible(int team)
    {
        switch (team)
        {
            case 0: return false;
            case 1: return TeamFilter.ShowTr;
            case 2: return TeamFilter.ShowCt;
            case 3: return TeamFilter.ShowSpectator;
            default: return true;
        }
    }
}
