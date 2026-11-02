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
        EspRenderer.Init();
        Log.LogInfo("ESP hooked");
    }
}

public static class EspRenderer
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public float Height;
    }

    private static readonly Dictionary<int, Tracked> _tracked = new();
    private static readonly List<(Vector3 pos, bool isBot, float h)> _toDraw = new();
    private static Texture2D _dotTexture;
    private static float _scanTimer;
    private const float ScanInterval = 1.0f;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;
    private static bool _hooked;

    public static void Init()
    {
        if (_hooked) return;
        _hooked = true;

        _dotTexture = new Texture2D(4, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                _dotTexture.SetPixel(x, y, Color.white);
        _dotTexture.Apply();

        Camera.onPostRender = (Camera.CameraCallback)OnPostRender;
        Debug.Log("[ESP] EspRenderer.Init");
    }

    private static void OnPostRender(Camera cam)
    {
        try
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval)
            {
                _scanTimer = 0f;
                DoScan();
            }

            UpdateDrawList();
            Draw(cam);
        }
        catch (Exception e) { Debug.LogError($"[ESP] EX: {e}"); }
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

    private static void DoScan()
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

        Debug.Log($"[ESP] Scan: {_tracked.Count} tracked");
    }

    private static void ScanType(Il2CppSystem.Type type, bool isBot, HashSet<int> seen)
    {
        if (type == null) return;
        try
        {
            var arr = UnityEngine.Object.FindObjectsOfType(type, false);
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

                float h = 1.8f;
                try
                {
                    var cc = go.GetComponent<CharacterController>();
                    if (cc != null) h = cc.height;
                }
                catch { }

                _tracked[id] = new Tracked { Transform = tr, IsBot = isBot, Height = h };
            }
        }
        catch (Exception e) { Debug.LogError($"[ESP] ScanType EX: {e}"); }
    }

    private static void UpdateDrawList()
    {
        _toDraw.Clear();
        foreach (var t in _tracked.Values)
        {
            if (t.Transform == null) continue;
            try
            {
                Vector3 feet = t.Transform.position;
                Vector3 head = feet + Vector3.up * t.Height;
                _toDraw.Add((head, t.IsBot, t.Height));
            }
            catch { }
        }
    }

    private static void Draw(Camera cam)
    {
        if (cam == null || _dotTexture == null) return;

        foreach (var (headPos, isBot, h) in _toDraw)
        {
            Vector3 screen = cam.WorldToScreenPoint(headPos);
            if (screen.z < 0) continue;

            // Простая эвристика: если точка слишком близко к камере — вероятно, локальный игрок
            if (screen.z < 1.0f) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            // Рисуем точку (квадрат 10x10)
            GUI.color = isBot ? Color.yellow : Color.cyan;
            Graphics.DrawTexture(new Rect(x - 5, y - 5, 10, 10), _dotTexture);

            // Текст под точкой (на экране, а не в мире)
            GUI.color = Color.white;
            GUI.Label(new Rect(x - 50, y + 8, 100, 20), isBot ? "BOT" : "PLAYER");
        }
        GUI.color = Color.white;
    }
}
