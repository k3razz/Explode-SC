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

    private static object _playerManagerInstance;
    private static PropertyInfo _playersProperty;
    private static MethodInfo _playersMethod;
    private static PropertyInfo _instanceProperty;
    private static Type _managerType;
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
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _managerType = asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                if (_managerType != null) break;
            }

            if (_managerType == null)
            {
                Debug.LogError("[ESP] PlayerManager type not found.");
                _managerFailed = true;
                return;
            }

            // Ищем статическое свойство, возвращающее PlayerManager (Instance из LazySingleton)
            string[] possibleNames = { "Instance", "instance", "dpkt" };
            foreach (var name in possibleNames)
            {
                _instanceProperty = _managerType.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (_instanceProperty != null) break;
            }

            if (_instanceProperty == null)
            {
                foreach (var prop in _managerType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                {
                    if (prop.PropertyType == _managerType)
                    {
                        _instanceProperty = prop;
                        break;
                    }
                }
            }

            if (_instanceProperty == null)
            {
                Debug.LogError("[ESP] Could not find static Instance property on PlayerManager.");
                _managerFailed = true;
                return;
            }

            // Свойство CEADFCHEEGGGDHC (HashSet<PlayerController>)
            _playersProperty = _managerType.GetProperty("CEADFCHEEGGGDHC", BindingFlags.Public | BindingFlags.Instance);

            // Fallback: метод BDDEGCBCFDGBEBH()
            if (_playersProperty == null)
                _playersMethod = _managerType.GetMethod("BDDEGCBCFDGBEBH", BindingFlags.Public | BindingFlags.Instance);

            if (_playersProperty == null && _playersMethod == null)
            {
                Debug.LogError("[ESP] Could not find players property/method on PlayerManager.");
                _managerFailed = true;
                return;
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
            if (_playerManagerInstance == null && _instanceProperty != null)
                _playerManagerInstance = _instanceProperty.GetValue(null);

            if (_playerManagerInstance == null) return;

            object playersObj = null;
            if (_playersProperty != null)
                playersObj = _playersProperty.GetValue(_playerManagerInstance);
            else if (_playersMethod != null)
                playersObj = _playersMethod.Invoke(_playerManagerInstance, null);

            if (playersObj == null) return;

            var enumerable = playersObj as System.Collections.IEnumerable;
            if (enumerable == null) return;

            int total = 0, filtered = 0;

            foreach (var item in enumerable)
            {
                total++;
                if (item == null) continue;

                var il2cppObj = item as Il2CppObjectBase;
                if (il2cppObj == null) continue;

                var component = il2cppObj.TryCast<Component>();
                if (component == null) continue;

                var transform = component.transform;
                if (transform == null) continue;

                Vector3 pos = transform.position;
                string goName = component.gameObject.name;

                if (goName == null) continue;
                if (!goName.StartsWith("TrPlayer") && !goName.StartsWith("CtPlayer")) continue;

                filtered++;
                _toDraw.Add((pos, goName));
            }

            if (total > 0 && filtered == 0)
                Debug.Log($"[ESP] {total} players, 0 matching TrPlayer/CtPlayer filter.");
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

            GUI.color = name.StartsWith("TrPlayer") ? Color.red : Color.cyan;
            GUI.DrawTexture(new Rect(x - 25, y - 25, 50, 50), _boxTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 30, y - 10, 250, 20), name);
        }
    }
}
