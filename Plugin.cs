using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("ESP loading");
        IL2CPPChainloader.AddUnityComponent<EspBehaviour>();
        Log.LogInfo("ESP component added");
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public int Team;
        public string Name;
        public Renderer[] Renderers;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<Tracked> _toDraw = new();
    private Camera _cam;
    private Texture2D _whiteTex;
    private float _scanTimer;
    private const float ScanInterval = 1.0f

    private const float BoxWidth = 60f;
    private const float BoxHeight = 120f;
    private const float OutlineThickness = 2f;
    private const float HeadOffset = 1.8f;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private static PropertyInfo _localPlayerProp;
    private static PropertyInfo _teamProp;
    private int _localTeam = -1;

    void Awake()
    {
        Debug.Log("[ESP] Awake");
        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
    }

    void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            DoScan();
        }

        _toDraw.Clear();
        foreach (var t in _tracked.Values)
        {
            if (t.Transform == null) continue;
            
            
            ApplyChams(t);

            
            if (_localTeam != -1 && t.Team == _localTeam) continue;
            _toDraw.Add(t);
        }
    }

    void ApplyChams(Tracked t)
    {
        if (t.Renderers == null || t.Renderers.Length == 0) return;

        
        Color chamColor = t.Team == 1 ? Color.red : new Color(0f, 0.5f, 1f);

        foreach (var r in t.Renderers)
        {
            if (r == null) continue;
            try
            {
                
                var mat = r.material;
                if (mat != null)
                {
                    
                    if (mat.HasProperty("_Color")) mat.color = chamColor;
                   
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", chamColor);
                }
            }
            catch { }
        }
    }

    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            Type bt = null, pt = null, pmType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                bt ??= asm.GetType("Chillow.StandChillow.Bots.Core.BotPlayerController");
                pt ??= asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                pmType ??= asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                if (bt != null && pt != null && pmType != null) break;
            }

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);

            if (pmType != null && pt != null)
            {
                _localPlayerProp = pmType.GetProperty("DEEBDAFCBHCGFBA", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                _teamProp = pt.GetProperty("CGGHFHCAFFBBCBB", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            Debug.Log($"[ESP] Types: Bot={(_botType != null)}, Player={(_playerType != null)}, LocalProp={(_localPlayerProp != null)}");
        }
        catch (Exception e) { Debug.LogError($"[ESP] ResolveTypes EX: {e.Message}"); }
    }

    void DoScan()
    {
        ResolveTypes();
        if (_botType == null && _playerType == null) return;

        UpdateLocalTeam();

        var seen = new HashSet<int>();
        ScanType(_botType, true, seen);
        ScanType(_playerType, false, seen);

        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!seen.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);

        int botN = 0, playerN = 0;
        foreach (var t in _tracked.Values) { if (t.IsBot) botN++; else playerN++; }
        Debug.Log($"[ESP] Scan: bots={botN}, players={playerN}, localTeam={_localTeam}");
    }

    void UpdateLocalTeam()
    {
        try
        {
            if (_localPlayerProp == null || _teamProp == null) return;

            var pmType = _localPlayerProp.DeclaringType;
            object pmInstance = null;
            var instProp = pmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instProp != null) pmInstance = instProp.GetValue(null);

            var localPlayer = _localPlayerProp.GetValue(pmInstance);
            if (localPlayer != null)
            {
                var teamObj = _teamProp.GetValue(localPlayer);
                if (teamObj != null) _localTeam = Convert.ToInt32(teamObj);
            }
        }
        catch { }
    }

    void ScanType(Il2CppSystem.Type type, bool isBot, HashSet<int> seen)
    {
        if (type == null) return;
        try
        {
            var arr = FindObjectsOfType(type, false);
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                var o = arr[i];
                if (o == null) continue;
                var c = o.TryCast<Component>();
                if (c == null) continue;
                var tr = c.transform;
                if (tr == null) continue;
                var go = c.gameObject;
                if (go == null) continue;

                int id = go.GetInstanceID();
                if (seen.Contains(id)) continue;
                seen.Add(id);

                int targetTeam = -1;
                try
                {
                    if (_teamProp != null)
                    {
                        var teamObj = _teamProp.GetValue(c);
                        if (teamObj != null) targetTeam = Convert.ToInt32(teamObj);
                    }
                }
                catch { }

                
                var allRenderers = go.GetComponentsInChildren<Renderer>();

                _tracked[id] = new Tracked
                {
                    Transform = tr,
                    IsBot = isBot,
                    Team = targetTeam,
                    Name = TryGetName(c, isBot),
                    Renderers = allRenderers
                };
            }
        }
        catch (Exception e) { Debug.LogError($"[ESP] ScanType EX: {e.Message}"); }
    }

    string TryGetName(Component c, bool isBot)
    {
        try
        {
            var prop = c.GetType().GetProperty("HBBBCEFBCFEFBBA", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                var pp = prop.GetValue(c);
                if (pp != null)
                {
                    var nickProp = pp.GetType().GetProperty("NickName", BindingFlags.Public | BindingFlags.Instance);
                    if (nickProp != null)
                    {
                        var nick = nickProp.GetValue(pp) as string;
                        if (!string.IsNullOrEmpty(nick)) return nick;
                    }
                }
            }
        }
        catch { }
        try { var n = c.gameObject.name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
        return isBot ? "BOT" : "PLAYER";
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var t in _toDraw)
        {
            Vector3 headWorld;
            try { headWorld = t.Transform.position + Vector3.up * HeadOffset; }
            catch { continue; }

            Vector3 screen = _cam.WorldToScreenPoint(headWorld);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            float bw = BoxWidth;
            float bh = BoxHeight;
            float th = OutlineThickness;

            float left = x - bw / 2f;
            float top = y;
            float bottom = y + bh;

            Color fillColor = t.Team == 1 ? new Color(1f, 0f, 0f, 0.15f) : new Color(0f, 0.5f, 1f, 0.15f);
            Color outlineColor = t.Team == 1 ? Color.red : Color.cyan;

            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(left, top, bw, bh), _whiteTex);

           
            GUI.color = outlineColor;
            GUI.DrawTexture(new Rect(left, top, bw, th), _whiteTex);
            GUI.DrawTexture(new Rect(left, bottom - th, bw, th), _whiteTex);
            GUI.DrawTexture(new Rect(left, top, th, bh), _whiteTex);
            GUI.DrawTexture(new Rect(left + bw - th, top, th, bh), _whiteTex);

            
            GUI.color = Color.white;
            string label = string.IsNullOrEmpty(t.Name) ? (t.IsBot ? "BOT" : "PLAYER") : t.Name;
            GUI.Label(new Rect(left, top - 18f, bw + 60f, 20f), label);
        }
        GUI.color = Color.white;
    }
}