using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("ESP loading...");
        AddComponent<EspBehaviour>();
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

    // Для локального игрока
    private static object _localPlayer;
    private static PropertyInfo _localPlayerProp;
    private static bool _localPlayerResolved;

    void Awake()
    {
        _boxTexture = new Texture2D(1, 1);
        _boxTexture.SetPixel(0, 0, Color.white);
        _boxTexture.Apply();
        Debug.Log("[ESP] Awake done");
    }

    void Start()
    {
        Debug.Log("[ESP] Start called");
        ResolveTypes();
        ResolveLocalPlayer();
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
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolveTypes EX: {e}");
        }
    }

    private static void ResolveLocalPlayer()
    {
        if (_localPlayerResolved) return;
        _localPlayerResolved = true;

        try
        {
            Type pmType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                pmType = asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                if (pmType != null) break;
            }

            if (pmType == null)
            {
                Debug.Log("[ESP] LocalPlayer: PlayerManager type not found");
                return;
            }

            // Ищем статическое свойство Instance (LazySingleton)
            PropertyInfo inst = null;
            foreach (var name in new[] { "Instance", "instance" })
            {
                inst = pmType.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (inst != null) break;
            }
            if (inst == null)
            {
                foreach (var p in pmType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    if (p.PropertyType == pmType) { inst = p; break; }
            }
            if (inst == null)
            {
                Debug.Log("[ESP] LocalPlayer: Instance property not found");
                return;
            }

            var mgr = inst.GetValue(null);
            if (mgr == null)
            {
                Debug.Log("[ESP] LocalPlayer: PlayerManager.Instance is null");
                return;
            }

            // Свойство DEEBDAFCBHCGFBA (локальный PlayerController)
            _localPlayerProp = pmType.GetProperty("DEEBDAFCBHCGFBA", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (_localPlayerProp == null)
            {
                Debug.Log("[ESP] LocalPlayer: DEEBDAFCBHCGFBA property not found");
                return;
            }

            _localPlayer = _localPlayerProp.GetValue(mgr);
            Debug.Log($"[ESP] LocalPlayer: {(_localPlayer != null ? "found" : "null")}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolveLocalPlayer EX: {e}");
        }
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
        ResolveLocalPlayer();

        if (_botType == null && _playerType == null)
        {
            Debug.LogWarning("[ESP] Scan: no types, skip");
            return;
        }

        _botTransforms.Clear();
        _playerTransforms.Clear();

        // Обновляем локального игрока (мог появиться позже)
        try
        {
            if (_localPlayer == null && _localPlayerProp != null)
            {
                // пере-резолвим Instance и локального
                _localPlayerResolved = false;
                ResolveLocalPlayer();
            }
        }
        catch { }

        int botCount = 0, playerCount = 0;
        int otherBots = 0, otherPlayers = 0;
        bool hasLocal = _localPlayer != null;

        // Боты
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
                        otherBots++;
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"[ESP] Bot Find EX: {e}"); }
        }

        // Игроки
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

                        // Пропускаем локального игрока
                        bool isLocal = false;
                        if (hasLocal && _localPlayer != null)
                        {
                            try
                            {
                                var localComp = _localPlayer as Il2CppObjectBase;
                                var thisComp = o as Il2CppObjectBase;
                                if (localComp != null && thisComp != null)
                                {
                                    // Сравниваем указатели IL2CPP
                                    if (localComp.Pointer == thisComp.Pointer) isLocal = true;
                                }
                            }
                            catch { }
                        }

                        var c = o.TryCast<Component>();
                        if (c == null) continue;
                        var tr = c.transform;
                        if (tr == null) continue;

                        if (isLocal)
                        {
                            // локального не добавляем в отрисовку, но считаем
                            continue;
                        }

                        _playerTransforms.Add(tr);
                        playerCount++;
                        otherPlayers++;
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"[ESP] Player Find EX: {e}"); }
        }

        // Итоговая статистика в консоль
        string localStatus = hasLocal ? "LOCAL: yes" : "LOCAL: no";
        Debug.Log($"[ESP] Scan: {localStatus} | bots total={botCount}, players total={playerCount} | others: bots={otherBots}, players={otherPlayers}");
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) { GUI.Label(new Rect(10, 10, 300, 20), "ESP: Camera.main is null"); return; }

        GUI.Label(new Rect(10, 10, 500, 20), $"ESP: drawing {_toDraw.Count} (bots+others)");

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
