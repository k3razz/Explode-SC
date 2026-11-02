using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
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
        Log.LogInfo("Player ESP loaded");
        AddComponent<EspBehaviour>();
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class TrackedObj
    {
        public Transform Transform;
        public Vector3 LastPos;
        public string Name;
        public bool IsMoving;
        public bool LoggedToFile;
        public bool Enabled;
    }

    private readonly Dictionary<int, TrackedObj> _tracked = new();
    private readonly List<(Vector3 pos, string name)> _toDraw = new();
    private readonly HashSet<string> _loggedNames = new();
    private Camera _cam;
    private string _cachePath;

    private const float MoveThreshold = 0.01f;
    private const float ScanInterval = 1f; // временно 1 сек для проверки ФПС
    private float _scanTimer;

    private bool _menuOpen;
    private Rect _menuRect = new Rect(20, 20, 350, 500);
    private Vector2 _scroll;

    private static MethodInfo _getKeyDownMethod;
    private static bool _inputInitialized;

    void Awake()
    {
        string gameRoot = Path.GetDirectoryName(Application.dataPath);
        _cachePath = Path.Combine(gameRoot, "cache.txt");

        if (File.Exists(_cachePath))
        {
            foreach (var line in File.ReadAllLines(_cachePath))
                if (!string.IsNullOrWhiteSpace(line))
                    _loggedNames.Add(line.Trim());
        }
        else
        {
            File.WriteAllText(_cachePath, "");
        }

        Debug.Log($"[ESP] cache.txt: {_cachePath}");
    }

    private static void InitInput()
    {
        if (_inputInitialized) return;
        _inputInitialized = true;

        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var inputType = asm.GetType("UnityEngine.Input");
                if (inputType != null)
                {
                    _getKeyDownMethod = inputType.GetMethod("GetKeyDown", new[] { typeof(KeyCode) });
                    if (_getKeyDownMethod != null) return;
                }
            }
        }
        catch { }

        Debug.LogWarning("[ESP] Could not find UnityEngine.Input.GetKeyDown via reflection.");
    }

    private static bool IsKeyDown(KeyCode key)
    {
        if (_getKeyDownMethod == null) return false;
        try { return (bool)_getKeyDownMethod.Invoke(null, new object[] { key }); }
        catch { return false; }
    }

    void Update()
    {
        InitInput();
        if (IsKeyDown(KeyCode.Insert))
            _menuOpen = !_menuOpen;

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanAllObjects();
        }

        _toDraw.Clear();
        foreach (var kvp in _tracked)
        {
            var obj = kvp.Value;
            if (obj.Transform == null) continue;

            Vector3 currentPos;
            try { currentPos = obj.Transform.position; }
            catch { continue; }

            float dist = Vector3.Distance(currentPos, obj.LastPos);
            obj.IsMoving = dist > MoveThreshold;

            if (obj.IsMoving)
            {
                obj.LastPos = currentPos;

                if (obj.Enabled)
                    _toDraw.Add((currentPos, obj.Name));

                if (!obj.LoggedToFile && !_loggedNames.Contains(obj.Name))
                {
                    _loggedNames.Add(obj.Name);
                    try { File.AppendAllText(_cachePath, obj.Name + Environment.NewLine); }
                    catch (Exception e) { Debug.LogWarning($"[ESP] cache write failed: {e.Message}"); }
                    obj.LoggedToFile = true;
                }
            }
        }
    }

    void ScanAllObjects()
    {
        // ЗАМЕНИТЕ PlayerView на реальное имя класса компонента игрока из UnityExplorer.
        // Если не знаете — попробуйте Player, BasePlayer, PlayerController и т.д.
        var players = FindObjectsOfType<PlayerView>();
        var seen = new HashSet<int>();

        foreach (var p in players)
        {
            if (p == null) continue;
            GameObject go = p.gameObject;
            string goName = go.name;
            if (goName == null) continue;
            if (!goName.StartsWith("TrPlayer") && !goName.StartsWith("CtPlayer")) continue;

            int id = go.GetInstanceID();
            seen.Add(id);

            if (_tracked.TryGetValue(id, out var tracked))
            {
                tracked.Transform = p.transform;
                tracked.Name = goName;
            }
            else
            {
                _tracked[id] = new TrackedObj
                {
                    Transform = p.transform,
                    LastPos = p.transform.position,
                    Name = goName,
                    IsMoving = false,
                    LoggedToFile = false,
                    Enabled = true
                };
            }
        }

        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!seen.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            foreach (var (worldPos, name) in _toDraw)
            {
                Vector3 screen = _cam.WorldToScreenPoint(worldPos);
                if (screen.z < 0) continue;

                float x = screen.x;
                float y = Screen.height - screen.y;

                GUI.color = name.StartsWith("TrPlayer") ? Color.red : Color.cyan;
                GUI.Box(new Rect(x - 25, y - 25, 50, 50), "");

                GUI.color = Color.white;
                GUI.Label(new Rect(x + 30, y - 10, 250, 20), name);
            }
        }

        if (!_menuOpen) return;

        GUI.color = Color.white;
        _menuRect = GUI.Window(0, _menuRect, (GUI.WindowFunction)(id => DrawMenu(id)), "Dynamic ESP — Insert to toggle");
    }

    void DrawMenu(int windowId)
    {
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Включить все"))
        {
            foreach (var o in _tracked.Values) o.Enabled = true;
        }
        if (GUILayout.Button("Выключить все"))
        {
            foreach (var o in _tracked.Values) o.Enabled = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label($"Обнаружено объектов: {_tracked.Count}");

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_menuRect.height - 100));

        var movingList = new List<KeyValuePair<int, TrackedObj>>();
        foreach (var kvp in _tracked)
            if (kvp.Value.IsMoving && kvp.Value.Transform != null)
                movingList.Add(kvp);

        if (movingList.Count == 0)
        {
            GUILayout.Label("Нет движущихся объектов...");
        }
        else
        {
            foreach (var kvp in movingList)
            {
                var obj = kvp.Value;
                GUILayout.BeginHorizontal();
                bool newVal = GUILayout.Toggle(obj.Enabled, "");
                if (newVal != obj.Enabled)
                    obj.Enabled = newVal;
                GUILayout.Label($"{obj.Name}  (ID: {kvp.Key})");
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0, 0, _menuRect.width, 20));
    }
}
