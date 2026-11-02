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
    private readonly List<(Vector3 pos, string label, bool isBot)> _toDraw = new();
    private Camera _cam;
    private Texture2D _boxTexture;
    private string _gameRoot;

    private const float ScanInterval = 0.25f;
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
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanAll();
        }
    }

    void ScanAll()
    {
        ResolveTypes();
        _toDraw.Clear();

        ScanType(_botControllerType, true);
        ScanType(_playerControllerType, false);
    }

    void ScanType(Il2CppSystem.Type type, bool isBot)
    {
        if (type == null) return;

        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<UnityEngine.Object> objects;
        try
        {
            objects = FindObjectsOfType(type, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ESP] FindObjectsOfType({(isBot ? "bot" : "player")}) failed: {e.Message}");
            return;
        }

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

            Vector3 pos;
            try { pos = transform.position; }
            catch { continue; }

            string goName;
            try { goName = component.gameObject.name; }
            catch { goName = "unknown"; }

            string label = $"{(isBot ? "BOT" : "PLR")}: {goName}";
            _toDraw.Add((pos, label, isBot));
        }

        if (objects.Length > 0)
            Debug.Log($"[ESP] {(isBot ? "Bot" : "Player")} count: {objects.Length}");
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var (worldPos, label, isBot) in _toDraw)
        {
            Vector3 screen = _cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            GUI.color = isBot ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(x - 25, y - 25, 50, 50), _boxTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 30, y - 10, 300, 20), label);
        }
    }
}
