using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
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
    private string _gameRoot;

    private const float MoveThreshold = 0.01f;
    private const float ScanInterval = 1f;
    private const float AutoSaveInterval = 15f;
    private float _scanTimer;
    private float _autoSaveTimer;

    private bool _menuOpen;
    private Rect _menuRect = new Rect(20, 20, 350, 520);
    private Vector2 _scroll;
    private string _lastSaveStatus = "";
    private string _lastSavedFile = "";

    private static MethodInfo _getKeyDownMethod;
    private static bool _inputInitialized;

    private static Il2CppSystem.Type _playerControllerType;
    private static bool _typeResolved;

    void Awake()
    {
        _gameRoot = Path.GetDirectoryName(Application.dataPath);
        Debug.Log($"[ESP] game root: {_gameRoot}");
    }

    void OnApplicationQuit()
    {
        SaveCache();
    }

    private void SaveCache()
    {
        try
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"cache-{stamp}.txt";
            string fullPath = Path.Combine(_gameRoot, fileName);

            File.WriteAllLines(fullPath, _loggedNames);

            _lastSavedFile = fileName;
            _lastSaveStatus = $"Сохранено: {_loggedNames.Count} записей";
            Debug.Log($"[ESP] Cache saved to {fullPath} ({_loggedNames.Count} entries)");
        }
        catch (Exception e)
        {
            _lastSaveStatus = $"Ошибка: {e.Message}";
            Debug.LogWarning($"[ESP] Cache save failed: {e.Message}");
        }
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

    private static void ResolvePlayerType()
    {
        if (_typeResolved) return;
        _typeResolved = true;

        try
        {
            Type playerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                playerType = asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                if (playerType != null) break;
            }

            if (playerType == null)
            {
                Debug.LogError("[ESP] PlayerController type not found in loaded assemblies.");
                return;
            }

            _playerControllerType = Il2CppType.From(playerType);
            if (_playerControllerType == null)
            {
                Debug.LogError("[ESP] Il2CppType.From returned null for PlayerController.");
                return;
            }

            Debug.Log("[ESP] PlayerController type resolved.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolvePlayerType failed: {e.Message}");
        }
    }

    void Update()
    {
        InitInput();
        if (IsKeyDown(KeyCode.Insert))
            _menuOpen = !_menuOpen;

        _autoSaveTimer += Time.deltaTime;
        if (_autoSaveTimer >= AutoSaveInterval)
        {
            _autoSaveTimer = 0f;
            SaveCache();
        }

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

                if (!obj.LoggedToFile)
                {
                    _loggedNames.Add(obj.Name);
                    obj.LoggedToFile = true;
                }
            }
        }
    }

    void ScanAllObjects()
    {
        ResolvePlayerType();
        if (_playerControllerType == null) return;

        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<UnityEngine.Object> players;
        try
        {
            players = FindObjectsOfType(_playerControllerType, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ESP] FindObjectsOfType failed: {e.Message}");
            return;
        }

        if (players == null) return;

        var seen = new HashSet<int>();

        for (int i = 0; i < players.Length; i++)
        {
            var obj = players[i];
            if (obj == null) continue;

            Component component;
            try { component = obj.TryCast<Component>(); }
            catch { continue; }
            if (component == null) continue;

            GameObject go;
            try { go = component.gameObject; }
            catch { continue; }
            if (go == null) continue;

            string goName = go.name;
            if (goName == null) continue;
            if (!goName.StartsWith("TrPlayer") && !goName.StartsWith("CtPlayer")) continue;

            int id = go.GetInstanceID();
            seen.Add(id);

            if (_tracked.TryGetValue(id, out var tracked))
            {
                tracked.Transform = component.transform;
                tracked.Name = goName;
            }
            else
            {
                _tracked[id] = new TrackedObj
                {
                    Transform = component.transform,
                    LastPos = component.transform.position,
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
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Сохранить кэш сейчас"))
        {
            SaveCache();
        }
        if (GUILayout.Button("Очистить кэш"))
        {
            _loggedNames.Clear();
            foreach (var o in _tracked.Values) o.LoggedToFile = false;
            _lastSaveStatus = "Кэш очищен (не сохранён)";
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_lastSaveStatus))
            GUILayout.Label(_lastSaveStatus);
        if (!string.IsNullOrEmpty(_lastSavedFile))
            GUILayout.Label($"Файл: {_lastSavedFile}");

        GUILayout.Space(5);
        GUILayout.Label($"Обнаружено объектов: {_tracked.Count}");
        GUILayout.Label($"Записей в кэше: {_loggedNames.Count}");
        GUILayout.Label($"До автосохранения: {Math.Max(0, (int)(AutoSaveInterval - _autoSaveTimer))} сек");

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_menuRect.height - 200));

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
