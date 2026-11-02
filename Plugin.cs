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
        Log.LogInfo("Bot+Player ESP loaded");
        AddComponent<EspBehaviour>();
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public string Label;
        public bool IsBot;
        public float Height; // для bounding box
        public float Width;  // для bounding box
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<(Vector3 pos, string label, bool isBot, float h, float w)> _toDraw = new();
    private Camera _cam;
    private Texture2D _boxTexture;
    private string _gameRoot;

    // Сканируем сцену редко (поиск новых), позиции читаем каждый кадр
    private const float ScanInterval = 0.5f;
    private float _scanTimer;

    private static Il2CppSystem.Type _botControllerType;
    private static Il2CppSystem.Type _playerControllerType;
    private static bool _typesResolved;

    void Awake()
    {
        _gameRoot = Path.GetDirectoryName(Application.dataPath);
        _boxTexture = new Texture2D(1, 1);
        _boxTexture.SetPixel(0, 0, Color.white);
        _boxTexture.Apply();
        Debug.Log($"[ESP] game root: {_gameRoot}");
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

        // 1. Редко сканируем сцену — ищем новых / удаляем мёртвых
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanScene();
        }

        // 2. Каждый кадр — читаем актуальные позиции и размеры
        _toDraw.Clear();
        foreach (var kvp in _tracked)
        {
            var t = kvp.Value;
            if (t.Transform == null) continue;

            Vector3 pos;
            try { pos = t.Transform.position; }
            catch { continue; }

            _toDraw.Add((pos, t.Label, t.IsBot, t.Height, t.Width));
        }
    }

    void ScanScene()
    {
        var seen = new HashSet<int>();

        ScanType(_botControllerType, true, seen);
        ScanType(_playerControllerType, false, seen);

        // Удаляем объекты, которых больше нет
        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!seen.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);
    }

    void ScanType(Il2CppSystem.Type type, bool isBot, HashSet<int> seen)
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
            if (seen.Contains(id)) continue;
            seen.Add(id);

            // Размеры: берём из CharacterController или из SkinnedMeshRenderer.bounds
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

            string goName = "unknown";
            try { goName = go.name; } catch { }

            _tracked[id] = new Tracked
            {
                Transform = transform,
                Label = $"{(isBot ? "BOT" : "PLR")}: {goName}",
                IsBot = isBot,
                Height = h,
                Width = w
            };
        }
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var (worldPos, label, isBot, h, w) in _toDraw)
        {
            Vector3 screen = _cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            // Высота рамки в пикселях: масштабируем по расстоянию до камеры
            float distance = screen.z;
            float pixelsPerUnit = Screen.height / (2f * distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

            float boxHeight = h * pixelsPerUnit;
            float boxWidth = w * pixelsPerUnit;

            // Центр экрана для верха головы: worldPos + height/2 (грубо)
            float topY = y - boxHeight * 0.5f;
            float bottomY = y + boxHeight * 0.5f;
            float leftX = x - boxWidth * 0.5f;

            // Рамка
            GUI.color = isBot ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(leftX, topY, boxWidth, boxHeight), _boxTexture);

            // Подпись чуть выше рамки и левее (ближе к центру)
            GUI.color = Color.white;
            GUI.Label(new Rect(leftX, topY - 18, 250, 20), label);
        }
    }
}
