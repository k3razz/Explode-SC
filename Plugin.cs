using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("Dynamic ESP loaded");
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
    }

    private readonly Dictionary<int, TrackedObj> _tracked = new();
    private readonly List<(Vector3 pos, string name)> _toDraw = new();
    private readonly HashSet<string> _loggedNames = new();
    private Camera _cam;
    private string _cachePath;

    private const float MoveThreshold = 0.01f;
    private const float ScanInterval = 0.25f;
    private float _scanTimer;

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

    void Update()
    {
        // 1. Полное сканирование (редко)
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanAllObjects();
        }

        // 2. Проверка движения каждый кадр (только для известных объектов)
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
                _toDraw.Add((currentPos, obj.Name));

                // Записываем имя в файл один раз при первом обнаружении движения
                if (!obj.LoggedToFile && !_loggedNames.Contains(obj.Name))
                {
                    _loggedNames.Add(obj.Name);
                    try
                    {
                        File.AppendAllText(_cachePath, obj.Name + Environment.NewLine);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ESP] cache write failed: {e.Message}");
                    }
                    obj.LoggedToFile = true;
                }
            }
        }
    }

    void ScanAllObjects()
    {
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        if (allObjects == null) return;

        var seen = new HashSet<int>();

        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave)
                continue;

            Transform tr;
            try { tr = go.transform; }
            catch { continue; }
            if (tr == null) continue;

            int id = go.GetInstanceID();
            seen.Add(id);

            if (_tracked.TryGetValue(id, out var tracked))
            {
                tracked.Transform = tr;
                tracked.Name = go.name;
            }
            else
            {
                _tracked[id] = new TrackedObj
                {
                    Transform = tr,
                    LastPos = tr.position,
                    Name = go.name,
                    IsMoving = false,
                    LoggedToFile = false
                };
            }
        }

        // Удаляем объекты, которых больше нет
        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!seen.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);
    }

    void OnGUI()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var (worldPos, name) in _toDraw)
        {
            Vector3 screen = _cam.WorldToScreenPoint(worldPos);
            if (screen.z < 0) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            GUI.color = Color.yellow;
            GUI.Box(new Rect(x - 25, y - 25, 50, 50), "");

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 30, y - 10, 250, 20), name);
        }
    }
}
