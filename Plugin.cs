using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
using System.Collections.Generic;
using System;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("ESP loading...");
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
        public string Name;
        public Renderer[] BodyRenderers;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<Tracked> _toDraw = new();
    private Camera _cam;
    private Texture2D _whiteTexture;
    private float _scanTimer;
    private const float ScanInterval = 1.0f;
    private const float HeadOffset = 1.8f;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private int _guiCount;

    void Awake()
    {
        Debug.Log("[ESP] Awake");
        _whiteTexture = new Texture2D(1, 1);
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply();
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
            _toDraw.Add(t);
        }
    }

    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            Type bt = null, pt = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                bt ??= asm.GetType("Chillow.StandChillow.Bots.Core.BotPlayerController");
                pt ??= asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                if (bt != null && pt != null) break;
            }
            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);
            Debug.Log($"[ESP] Types: Bot={(_botType != null ? "OK" : "NULL")}, Player={(_playerType != null ? "OK" : "NULL")}");
        }
        catch (Exception e) { Debug.LogError($"[ESP] ResolveTypes EX: {e}"); }
    }

    void DoScan()
    {
        ResolveTypes();
        if (_botType == null && _playerType == null) return;

        var seen = new HashSet<int>();
        ScanType(_botType, true, seen);
        ScanType(_playerType, false, seen);

        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!seen.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);

        int botN = 0, playerN = 0, withRenderers = 0;
        foreach (var t in _tracked.Values)
        {
            if (t.IsBot) botN++; else playerN++;
            if (t.BodyRenderers != null && t.BodyRenderers.Length > 0) withRenderers++;
        }
        Debug.Log($"[ESP] Scan: bots={botN}, players={playerN}, withRenderers={withRenderers}");
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

                var bodyRenderers = GetBodyRenderers(go);
                string name = TryGetName(c, isBot);
                _tracked[id] = new Tracked
                {
                    Transform = tr,
                    IsBot = isBot,
                    Name = name,
                    BodyRenderers = bodyRenderers
                };
            }
        }
        catch (Exception e) { Debug.LogError($"[ESP] ScanType EX: {e}"); }
    }

    static Renderer[] GetBodyRenderers(GameObject go)
    {
        var list = new List<Renderer>();
        try
        {
            var comps = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (comps != null)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    var smr = comps[i];
                    if (smr == null) continue;
                    var n = smr.gameObject.name;
                    if (n == null) continue;

                    if (n.Contains("Arms")) continue;
                    if (n.Contains("Weapon")) continue;
                    if (n.Contains("Tool")) continue;
                    if (n.Contains("Knife")) continue;
                    if (n.Contains("Gun")) continue;
                    if (n.Contains("Ragdoll")) continue;

                    if (n.Contains("PlayerCharacter") || n.Contains("Character") || n.Contains("Body"))
                        list.Add(smr);
                }
            }
        }
        catch { }
        return list.ToArray();
    }

    string TryGetName(Component c, bool isBot)
    {
        try
        {
            var prop = c.GetType().GetProperty("HBBBCEFBCFEFBBA", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                var pp = prop.GetValue(c);
                if (pp != null)
                {
                    var nickProp = pp.GetType().GetProperty("NickName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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

    static bool GetBoundsScreenRect(Renderer[] renderers, Camera cam, out Rect screenRect)
    {
        screenRect = default;
        if (renderers == null || renderers.Length == 0 || cam == null) return false;

        Bounds b = default;
        bool has = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) return false;

        Vector3 min = b.min, max = b.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z),
        };

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        int valid = 0;

        for (int i = 0; i < 8; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(corners[i]);
            if (sp.z <= 0f) continue;
            if (sp.x < minX) minX = sp.x;
            if (sp.y < minY) minY = sp.y;
            if (sp.x > maxX) maxX = sp.x;
            if (sp.y > maxY) maxY = sp.y;
            valid++;
        }

        if (valid < 2) return false;
        float w = maxX - minX, h = maxY - minY;
        if (w <= 1f || h <= 1f) return false;

        screenRect = new Rect(minX, Screen.height - maxY, w, h);
        return true;
    }

    void OnGUI()
    {
        _guiCount++;
        if (_guiCount <= 3 || _guiCount % 300 == 0)
            Debug.Log($"[ESP] OnGUI tick #{_guiCount}, draw={_toDraw.Count}");

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var t in _toDraw)
        {
            // --- Бокс: только если реально получился ---
            bool boxDrawn = false;
            if (GetBoundsScreenRect(t.BodyRenderers, _cam, out var rect))
            {
                boxDrawn = true;

                // Заливка
                GUI.color = t.IsBot ? new Color(1f, 1f, 0f, 0.15f) : new Color(0f, 1f, 1f, 0.15f);
                GUI.DrawTexture(rect, _whiteTexture);

                // Границы
                GUI.color = t.IsBot ? Color.yellow : Color.cyan;
                const float th = 2f;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, th), _whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - th, rect.width, th), _whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, th, rect.height), _whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - th, rect.y, th, rect.height), _whiteTexture);
            }

            // --- Текст: рисуется ВСЕГДА, независимо от бокса ---
            Vector3 headWorld;
            try { headWorld = t.Transform.position + Vector3.up * HeadOffset; }
            catch { continue; }

            Vector3 screen = _cam.WorldToScreenPoint(headWorld);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            string label = string.IsNullOrEmpty(t.Name) ? (t.IsBot ? "BOT" : "PLAYER") : t.Name;

            // Если бокс был — текст над ним; иначе — на уровне головы
            float labelY = boxDrawn ? (y - 18f) : (y - 20f);
            float labelX = boxDrawn ? (x - 50f) : (x - 50f);

            GUI.color = t.IsBot ? Color.yellow : Color.cyan;
            GUI.Label(new Rect(labelX, labelY, 200, 20), label);
        }
        GUI.color = Color.white;
    }
}
