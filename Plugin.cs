using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using System;
using System.Reflection;

namespace ObjectInspector;

[BepInPlugin("com.you.objectinspector", "ObjectInspector", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("Object Inspector loaded. Press F8 to inspect object under mouse.");
        AddComponent<InspectorBehaviour>();
    }
}

public class InspectorBehaviour : MonoBehaviour
{
    private static MethodInfo _getKeyDownMethod;
    private static bool _inputInitialized;

    void Update()
    {
        InitInput();
        if (IsKeyDown(KeyCode.F8))
        {
            InspectUnderMouse();
        }
    }

    void InspectUnderMouse()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Inspector] Camera.main is null.");
            return;
        }

        // Берём позицию мыши
        Vector3 mousePos = GetMousePosition();
        Ray ray = cam.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out var hit, 1000f))
        {
            Debug.Log("[Inspector] No collider under mouse.");
            return;
        }

        GameObject go = hit.collider.gameObject;
        if (go == null) return;

        Debug.Log("========== OBJECT INSPECT ==========");
        Debug.Log($"[Inspector] GameObject Name: {go.name}");

        // Родитель
        Transform parent = go.transform.parent;
        string parentName = parent != null ? parent.gameObject.name : "null (root)";
        Debug.Log($"[Inspector] Parent: {parentName}");

        // Полный путь в иерархии
        Debug.Log($"[Inspector] Hierarchy Path: {GetPath(go.transform)}");

        // Компоненты и их типы
        var components = go.GetComponents<Component>();
        Debug.Log($"[Inspector] Components count: {components.Length}");
        foreach (var comp in components)
        {
            if (comp == null) continue;
            Type t = comp.GetType();
            Debug.Log($"[Inspector]   - {t.FullName} (Assembly: {t.Assembly.GetName().Name})");
        }

        Debug.Log("====================================");
    }

    string GetPath(Transform t)
    {
        string path = t.gameObject.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.gameObject.name + "/" + path;
        }
        return path;
    }

    Vector3 GetMousePosition()
    {
        // Пытаемся получить позицию мыши через рефлексию (старый Input)
        try
        {
            var inputType = Type.GetType("UnityEngine.Input, UnityEngine.InputLegacyModule");
            if (inputType != null)
            {
                var prop = inputType.GetProperty("mousePosition", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                    return (Vector3)prop.GetValue(null);
            }
        }
        catch { }

        // Если не вышло — возвращаем центр экрана (тогда бинд будет работать по прицелу)
        return new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
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
    }

    private static bool IsKeyDown(KeyCode key)
    {
        if (_getKeyDownMethod == null) return false;
        try { return (bool)_getKeyDownMethod.Invoke(null, new object[] { key }); }
        catch { return false; }
    }
}
