using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("Bot+Player ESP loaded (boxes only)");
        AddComponent<EspBehaviour>();
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public float Height;
        public float Width;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<(Vector3 pos, bool isBot, float h, float w)> _toDraw = new();
    private readonly List<int> _seenList = new(); // НЕ HashSet — IL2CPP не любит HashSet в параметрах
    private Camera _cam;
    private Texture2D _boxTexture;

    private const float ScanInterval = 0.5f;
    private float _scanTimer;

    private static Il2CppSystem.Type _botControllerType;
    private static Il2CppSystem.Type _playerControllerType;
    private static bool _typesResolved;

    void Awake()
    {
        _boxTexture = new Texture2D(1, 1);
        _boxTexture.SetPixel(0, 0, Color.white);
        _boxTexture.Apply();
        Debug.Log("[ESP] Awake done");
    }

    private static void ResolveTypes()
    {
        if (_typesResolved) return;
        _typesResolved = true;

        try
        {
            Type botType = null, playerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                botType ??= asm.GetType("Chillow.StandChillow.Bots.Core.BotPlayerController");
                playerType ??= asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                if (botType != null && playerType != null) break;
            }

            if (botType != null) _botControllerType = Il2CppType.From(botType);
            if (playerType != null) _playerControllerType = Il2CppType.From(playerType);

            Debug.Log($"[ESP] BotPlayerController: {(botType != null ? "OK" : "NULL")}, PlayerController: {(playerType != null ? "OK" : "NULL")}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolveTypes failed: {e.Message}");
        }
    }

    void Update()
    {
        ResolveTypes();

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanScene();
        }

        _toDraw.Clear();
        foreach (var kvp in _tracked)
        {
            var t = kvp.Value;
            if (t.Transform == null) continue;

            Vector3 pos;
            try { pos = t.Transform.position; }
            catch { continue; }

            _toDraw.Add((pos, t.IsBot, t.Height, t.Width));
        }
    }

    void ScanScene()
    {
        _seenList.Clear();

        ScanType(_botControllerType, true);
        ScanType(_playerControllerType, false);

        // Удаляем мёртвых
        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!_seenList.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);
    }

    void ScanType(Il2CppSystem.Type type, bool isBot)
    {
        if (type == null) return;

        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<UnityEngine.Object> objects;
        try { objects = FindObjectsOfType(type, false); }
        catch (Exception e) { Debug.LogWarning($"[ESP] FindObjectsOfType failed: {e.Message}"); return; }
        if (objects == null) return;

        for (int i = 0; i < objects.Length; i++)
        {
            var obj = objects[i];
            if (obj == null) continue;

            Component component;
            try { component = obj.TryCast<Component>(); }
            catch { continue; }
            if (component == null) continue;

            Transform transform;
            try { transform = component.transform; }
            catch { continue; }
            if (transform == null) continue;

            GameObject go;
            try { go = component.gameObject; }
            catch { continue; }
            if (go == null) continue;

            int id = go.GetInstanceID();
            if (_seenList.Contains(id)) continue;
            _seenList.Add(id);

            float h = 1.8f, w = 0.6f;
            try
            {
                var cc = go.GetComponent<CharacterController>();
                if (cc != null)
                {
                    h = cc.height;
                    w = cc.radius * 2f;
                }
                else
                {
                    var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        var b = smr.bounds;
                        h = b.size.y;
                        w = b.size.x;
                    }
                }
            }
            catch { }

            _tracked[id] = new Tracked
            {
                Transform = transform,
                IsBot = isBot,
                Height = h,
                Width = w
            };
        }

        if (objects.Length > 0)
            Debug.Log($"[ESP] {(isBot ? "Bot" : "Player")} count: {objects.Length}");
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var (worldPos, isBot, h, w) in _toDraw)
        {
            Vector3 screen = _cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            float distance = screen.z;
            float pixelsPerUnit = Screen.height / (2f * distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

            float boxHeight = h * pixelsPerUnit;
            float boxWidth = w * pixelsPerUnit;

            // CharacterController pivot — в ногах. Поднимаем рамку так, чтобы она охватывала модель:
            // низ рамки = позиция ног, верх = ноги + h.
            float bottomY = y;
            float topY = y - boxHeight;
            float leftX = x - boxWidth * 0.5f;

            GUI.color = isBot ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(leftX, topY, boxWidth, boxHeight), _boxTexture);
        }

        GUI.color = Color.white;
    }
}
