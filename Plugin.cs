using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using Il2CppInterop.Runtime;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace WeaponDumper;

[BepInPlugin("com.you.weapondumper", "WeaponDumper", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("WeaponDumper loaded. Press J in game to dump current weapon.");
        IL2CPPChainloader.AddUnityComponent<DumperBehaviour>();
    }
}

public class DumperBehaviour : MonoBehaviour
{
    private bool _jWasPressed;

    void Update()
    {
        // Ловим J через OnGUI, так как Input может не работать напрямую
    }

    void OnGUI()
    {
        var e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.J)
        {
            if (!_jWasPressed)
            {
                _jWasPressed = true;
                DumpCurrentWeapon();
            }
        }
        else if (e != null && e.type == EventType.KeyUp && e.keyCode == KeyCode.J)
        {
            _jWasPressed = false;
        }
    }

    void DumpCurrentWeapon()
    {
        Debug.Log("[Dumper] ========================================");
        Debug.Log("[Dumper] J pressed! Looking for local player's weapon...");

        try
        {
            // 1. Ищем PlayerManager
            Type pmType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                pmType = asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                if (pmType != null) break;
            }
            if (pmType == null) { Debug.LogError("[Dumper] PlayerManager not found"); return; }

            // 2. Достаем Instance
            object pmInstance = null;
            var instProp = pmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instProp != null) pmInstance = instProp.GetValue(null);
            if (pmInstance == null) { Debug.LogError("[Dumper] PlayerManager.Instance is null"); return; }

            // 3. Достаем локального игрока (DEEBDAFCBHCGFBA)
            var localProp = pmType.GetProperty("DEEBDAFCBHCGFBA", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (localProp == null) { Debug.LogError("[Dumper] Local player property not found"); return; }
            var localPlayer = localProp.GetValue(pmInstance);
            if (localPlayer == null) { Debug.LogError("[Dumper] Local player is null"); return; }

            // 4. Достаем WeaponryController (HDFDDHHEAGCFFFG)
            var weaponryProp = localPlayer.GetType().GetProperty("HDFDDHHEAGCFFFG", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (weaponryProp == null) { Debug.LogError("[Dumper] WeaponryController property not found"); return; }
            var weaponry = weaponryProp.GetValue(localPlayer);
            if (weaponry == null) { Debug.LogError("[Dumper] WeaponryController is null"); return; }

            // 5. Достаем текущий WeaponController (ADFFFAGDHFAHEBE)
            var currentWeaponProp = weaponry.GetType().GetProperty("ADFFFAGDHFAHEBE", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (currentWeaponProp == null) { Debug.LogError("[Dumper] Current weapon property not found"); return; }
            var currentWeapon = currentWeaponProp.GetValue(weaponry);
            if (currentWeapon == null) { Debug.LogError("[Dumper] Current weapon is null (nothing in hands?)"); return; }

            Debug.Log($"[Dumper] Found weapon: {currentWeapon.GetType().Name}");
            
            // 6. Выводим все поля и свойства оружия
            DumpObjectFields(currentWeapon, "Weapon");

            // 7. Пытаемся достать AccuracyData из WeaponController
            // Метод AEFAEGGFADGCEFE() возвращает AccuracyData
            var accMethod = currentWeapon.GetType().GetMethod("AEFAEGGFADGCEFE", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (accMethod != null)
            {
                var accuracyData = accMethod.Invoke(currentWeapon, null);
                if (accuracyData != null)
                {
                    Debug.Log($"[Dumper] --- AccuracyData Found ---");
                    DumpObjectFields(accuracyData, "AccuracyData");
                }
                else
                {
                    Debug.Log("[Dumper] AccuracyData is null");
                }
            }
            else
            {
                Debug.Log("[Dumper] AccuracyData method not found on weapon");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Dumper] Exception: {e}");
        }
        Debug.Log("[Dumper] ========================================");
    }

    void DumpObjectFields(object obj, string prefix)
    {
        var type = obj.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var f in fields)
        {
            try
            {
                object val = f.GetValue(obj);
                string valStr = val != null ? val.ToString() : "null";
                
                // Если это структура или класс, не примитив, выводим только тип
                if (val != null && !f.FieldType.IsPrimitive && !(val is string))
                {
                    valStr = $"[{f.FieldType.Name}]";
                }
                
                Debug.Log($"[Dumper] {prefix}.{f.Name} ({f.FieldType.Name}) = {valStr}");
            }
            catch (Exception e)
            {
                Debug.Log($"[Dumper] {prefix}.{f.Name} = <error: {e.Message}>");
            }
        }

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var p in props)
        {
            try
            {
                object val = p.GetValue(obj);
                string valStr = val != null ? val.ToString() : "null";
                Debug.Log($"[Dumper] {prefix}.get_{p.Name} ({p.PropertyType.Name}) = {valStr}");
            }
            catch
            {
                // Игнорируем свойства, которые кидают исключения (например, требующие аргументов)
            }
        }
    }
}