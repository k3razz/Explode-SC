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
        Log.LogInfo("PlayerManager ESP loaded");
        AddComponent<EspBehaviour>();
    }
}

public class EspBehaviour : MonoBehaviour
{
    private readonly List<(Vector3 pos, string name)> _toDraw = new();
    private Camera _cam;
    private Texture2D _boxTexture;
    private string _gameRoot;

    private const float ScanInterval = 0.25f;
    private float _scanTimer;

    // Рефлексия для PlayerManager
    private static object _playerManagerInstance;
    private static PropertyInfo _playersProperty;
    private static bool _managerResolved;
    private static bool _managerFailed;

    void Awake()
    {
        _gameRoot = Path.GetDirectoryName(Application.dataPath);
        _boxTexture = new Texture2D(1, 1);
        _boxTexture.SetPixel(0, 0, Color.white);
        _boxTexture.Apply();
        Debug.Log($"[ESP] game root: {_gameRoot}");
    }

    private static void ResolvePlayerManager()
    {
        if (_managerResolved || _managerFailed) return;
        _managerResolved = true;

        try
        {
            // 1. Ищем тип PlayerManager
            Type managerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                managerType = asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                if (managerType != null) break;
            }

            if (managerType == null)
            {
                Debug.LogError("[ESP] PlayerManager type not found.");
                _managerFailed = true;
                return;
            }

            // 2. Ищем свойство Instance в базовом классе LazySingleton (или в самом PlayerManager)
            //    Пробуем разные варианты, т.к. имя может быть обфусцировано.
            PropertyInfo instanceProp = null;
            string[] possibleNames = { "Instance", "instance", "dpkt", "HGGHFBCAGEEGBDH" }; // dpkt из дампа PlayerMainCamera, но LazySingleton может иметь своё

            foreach (var name in possibleNames)
            {
                instanceProp = managerType.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (instanceProp != null) break;
            }

            // Если не нашли по имени, пробуем найти любое статическое свойство, возвращающее PlayerManager
            if (instanceProp == null)
            {
                foreach (var prop in managerType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                {
                    if (prop.PropertyType == managerType)
                    {
                        instanceProp = prop;
                        break;
                    }
                }
            }

            if (instanceProp == null)
            {
                Debug.LogError("[ESP] Could not find static Instance property on PlayerManager.");
                _managerFailed = true;
                return;
            }

            _playerManagerInstance = instanceProp.GetValue(null);
            if (_playerManagerInstance == null)
            {
                Debug.LogWarning("[ESP] PlayerManager.Instance is null (not initialized yet).");
                return;
            }

            // 3. Ищем свойство CEADFCHEEGGGDHC (HashSet<PlayerController>) из дампа
            _playersProperty = managerType.GetProperty("CEADFCHEEGGGDHC", BindingFlags.Public | BindingFlags.Instance);
            if (_playersProperty == null)
            {
                // Альтернатива: метод BDDEGCBCFDGBEBH()
                var method = managerType.GetMethod("BDDEGCBCFDGBEBH", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    Debug.Log("[ESP] Found method BDDEGCBCFDGBEBH, will invoke it to get players.");
                    // Сохраним MethodInfo отдельно, если свойство не найдено
                    // (упрощённо: используем рефлексию для вызова метода в Scan)
                }
                else
                {
                    Debug.LogError("[ESP] Could not find players property/method on PlayerManager.");
                    _managerFailed = true;
                    return;
                }
            }

            Debug.Log("[ESP] PlayerManager resolved successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] ResolvePlayerManager failed: {e.Message}");
            _managerFailed = true;
        }
    }

    void Update()
    {
        ResolvePlayerManager();
        if (_managerFailed) return;

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            ScanPlayers();
        }
    }

    void ScanPlayers()
    {
        _toDraw.Clear();

        try
        {
            // Обновляем Instance, если он был null
            if (_playerManagerInstance == null)
            {
                ResolvePlayerManager();
                if (_playerManagerInstance == null) return;
            }

            // Получаем HashSet<PlayerController>
            object playersObj = null;
            if (_playersProperty != null)
            {
                playersObj = _playersProperty.GetValue(_playerManagerInstance);
            }
            else
            {
                // Fallback: вызываем метод BDDEGCBCFDGBEBH()
                var method = _playerManagerInstance.GetType().GetMethod("BDDEGCBCFDGBEBH", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                    playersObj = method.Invoke(_playerManagerInstance, null);
            }

            if (playersObj == null) return;

            // Приводим к IEnumerable (Il2Cpp HashSet)
            var enumerable = playersObj as System.Collections.IEnumerable;
            if (enumerable == null) return;

            foreach (var item in enumerable)
            {
                if (item == null) continue;

                // item — это PlayerController (Il2CppObject)
                // Приводим к Component, чтобы получить transform
                var component = item.TryCast<Component>();
                if (component == null) continue;

                var transform = component.transform;
                if (transform == null) continue;

                Vector3 pos = transform.position;
                string goName = component.gameObject.name;

                // Фильтр по имени GameObject (TrPlayer / CtPlayer)
                if (goName == null) continue;
                if (!goName.StartsWith("TrPlayer") && !goName.StartsWith("CtPlayer")) continue;

                _toDraw.Add((pos, goName));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ESP] ScanPlayers failed: {e.Message}");
        }
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

            // Рисуем рамку через DrawTexture (цвет через GUI.color)
            GUI.color = name.StartsWith("TrPlayer") ? Color.red : Color.cyan;
            GUI.DrawTexture(new Rect(x - 25, y - 25, 50, 50), _boxTexture);

            // Текст
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 30, y - 10, 250, 20), name);
        }
    }
}
