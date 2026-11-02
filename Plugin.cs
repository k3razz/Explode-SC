using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using System.Collections.Generic;
using System;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("ESP loading...");
        AddComponent<EspBehaviour>();
        Log.LogInfo("ESP component added via BasePlugin.AddComponent");
    }
}

public class EspBehaviour : MonoBehaviour
{
    private readonly List<(Vector3 pos, bool isBot)> _toDraw = new();
    private readonly List<Transform> _botTransforms = new();
    private readonly List<Transform> _playerTransforms = new();
    private Camera _cam;
    private Texture2D _boxTexture;
    private float _scanTimer;
    private const float ScanInterval = 1.0f;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;

    private int _awakeCount;
    private int _startCount;
    private int _updateCount;
    private int _guiCount;

    void Awake()
    {
        _awakeCount++;
        Debug.Log($"[ESP] Awake called (count={_awakeCount})");
        _boxTexture = new Texture2D(1, 1);
        _boxTexture.SetPixel(0, 0, Color.white);
        _boxTexture.Apply();
        Debug.Log("[ESP] Awake done, texture created");
    }

    void Start()
    {
        _startCount++;
        Debug.Log($"[ESP] Start called (count={_startCount})");
        ResolveTypes();
    }

    void OnEnable()
    {
        Debug.Log("[ESP] OnEnable called");
    }

    void OnDisable()
    {
        Debug.Log("[ESP] OnDisable called");
    }

    void OnDestroy()
    {
        Debug.Log("[ESP] OnDestroy called");
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

            Debug.Log($"[ESP] ResolveTypes: Bot={(_botType != null ? "OK" : "NULL")}, Player={(_playerType != null ? "OK" : "NULL")}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolveTypes EX: {e}");
        }
    }

    void Update()
    {
        _updateCount++;
        if (_updateCount <= 3 || _updateCount % 300 == 0)
            Debug.Log($"[ESP] Update tick #{_updateCount}");

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            DoScan();
        }

        _toDraw.Clear();
        foreach (var t in _botTransforms)
        {
            if (t == null) continue;
            try { _toDraw.Add((t.position, true)); } catch { }
        }
        foreach (var t in _playerTransforms)
        {
            if (t == null) continue;
            try { _toDraw.Add((t.position, false)); } catch { }
        }
    }

    void DoScan()
    {
        ResolveTypes();

        if (_botType == null && _playerType == null)
        {
            Debug.LogWarning("[ESP] DoScan: no types resolved");
            return;
        }

        _botTransforms.Clear();
        _playerTransforms.Clear();

        int botCount = 0, playerCount = 0;

        if (_botType != null)
        {
            try
            {
                var arr = FindObjectsOfType(_botType, false);
                if (arr != null)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var o = arr[i];
                        if (o == null) continue;
                        var c = o.TryCast<Component>();
                        if (c == null) continue;
                        var tr = c.transform;
                        if (tr == null) continue;
                        _botTransforms.Add(tr);
                        botCount++;
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"[ESP] Bot Find EX: {e}"); }
        }

        if (_playerType != null)
        {
            try
            {
                var arr = FindObjectsOfType(_playerType, false);
                if (arr != null)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var o = arr[i];
                        if (o == null) continue;
                        var c = o.TryCast<Component>();
                        if (c == null) continue;
                        var tr = c.transform;
                        if (tr == null) continue;
                        _playerTransforms.Add(tr);
                        playerCount++;
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"[ESP] Player Find EX: {e}"); }
        }

        Debug.Log($"[ESP] Scan: bots={botCount}, players={playerCount}");
    }

    void OnGUI()
    {
        _guiCount++;
        if (_guiCount <= 3 || _guiCount % 300 == 0)
            Debug.Log($"[ESP] OnGUI tick #{_guiCount}, _toDraw={_toDraw.Count}");

        if (_cam == null) _cam = Camera.main;
        if (_cam == null)
        {
            GUI.Label(new Rect(10, 10, 400, 20), "ESP: Camera.main is null");
            return;
        }

        GUI.Label(new Rect(10, 10, 400, 20), $"ESP: drawing {_toDraw.Count}");

        foreach (var (worldPos, isBot) in _toDraw)
        {
            Vector3 screen = _cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            float bw = 60f, bh = 100f;

            GUI.color = isBot ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(x - bw / 2, y - bh, bw, bh), _boxTexture);
        }

        GUI.color = Color.white;
    }
}
