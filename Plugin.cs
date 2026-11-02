using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using Il2CppInterop.Runtime;
using System.Collections.Generic;
using System;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    private static bool _created;

    public override void Load()
    {
        Log.LogInfo("ESP loading...");
        SceneManager.sceneLoaded = (UnityAction<Scene, LoadSceneMode>)OnSceneLoaded;
        Log.LogInfo("ESP subscribed to sceneLoaded");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_created) return;
        _created = true;

        var go = new GameObject("ESP_Behaviour_Object");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<EspBehaviour>();
        Debug.Log("[ESP] EspBehaviour created on scene load");
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public string Name;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<(Vector3 pos, bool isBot, string name)> _toDraw = new();
    private Camera _cam;
    private Texture2D _boxTexture;
    private float _scanTimer;
    private const float ScanInterval = 1.0f;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    void Awake()
    {
        Debug.Log("[ESP] EspBehaviour Awake");
        _boxTexture = new Texture2D(1, 1);
        _boxTexture.SetPixel(0, 0, Color.white);
        _boxTexture.Apply();
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
            try { _toDraw.Add((t.Transform.position, t.IsBot, t.Name)); } catch { }
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

        int botN = 0, playerN = 0;
        foreach (var t in _tracked.Values) { if (t.IsBot) botN++; else playerN++; }
        Debug.Log($"[ESP] Scan: bots={botN}, players={playerN}");
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

                string name = TryGetName(c, isBot);
                _tracked[id] = new Tracked { Transform = tr, IsBot = isBot, Name = name };
            }
        }
        catch (Exception e) { Debug.LogError($"[ESP] ScanType EX: {e}"); }
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

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { GUI.Label(new Rect(10, 10, 300, 20), "ESP: no camera"); return; }

        GUI.Label(new Rect(10, 10, 300, 20), $"ESP: {_toDraw.Count}");

        foreach (var (worldPos, isBot, name) in _toDraw)
        {
            Vector3 screen = _cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;
            float bw = 60f, bh = 100f;
            float left = x - bw / 2;
            float top = y - bh;

            GUI.color = isBot ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(left, top, bw, bh), _boxTexture);

            GUI.color = Color.white;
            string label = string.IsNullOrEmpty(name) ? (isBot ? "BOT" : "PLAYER") : name;
            GUI.Label(new Rect(left, top - 18, 200, 20), label);
        }
        GUI.color = Color.white;
    }
}
