using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MyDynamicESP;

public class KeybindListener : MonoBehaviour
{
    private float _lastLog;
    private bool _waitingForRelease = false;

    // Кэш для HudManager, чтобы не перебирать все сборки каждый кадр
    private static Type _hudType = null;
    private static bool _hudTypeSearched = false;

    private static readonly Dictionary<KeyCode, Key> KeyMap = new Dictionary<KeyCode, Key>
    {
        { KeyCode.A, Key.A }, { KeyCode.B, Key.B }, { KeyCode.C, Key.C }, { KeyCode.D, Key.D },
        { KeyCode.E, Key.E }, { KeyCode.F, Key.F }, { KeyCode.G, Key.G }, { KeyCode.H, Key.H },
        { KeyCode.I, Key.I }, { KeyCode.J, Key.J }, { KeyCode.K, Key.K }, { KeyCode.L, Key.L },
        { KeyCode.M, Key.M }, { KeyCode.N, Key.N }, { KeyCode.O, Key.O }, { KeyCode.P, Key.P },
        { KeyCode.Q, Key.Q }, { KeyCode.R, Key.R }, { KeyCode.S, Key.S }, { KeyCode.T, Key.T },
        { KeyCode.U, Key.U }, { KeyCode.V, Key.V }, { KeyCode.W, Key.W }, { KeyCode.X, Key.X },
        { KeyCode.Y, Key.Y }, { KeyCode.Z, Key.Z },
        { KeyCode.Alpha0, Key.Digit0 }, { KeyCode.Alpha1, Key.Digit1 }, { KeyCode.Alpha2, Key.Digit2 },
        { KeyCode.Alpha3, Key.Digit3 }, { KeyCode.Alpha4, Key.Digit4 }, { KeyCode.Alpha5, Key.Digit5 },
        { KeyCode.Alpha6, Key.Digit6 }, { KeyCode.Alpha7, Key.Digit7 }, { KeyCode.Alpha8, Key.Digit8 },
        { KeyCode.Alpha9, Key.Digit9 },
        { KeyCode.F1, Key.F1 }, { KeyCode.F2, Key.F2 }, { KeyCode.F3, Key.F3 }, { KeyCode.F4, Key.F4 },
        { KeyCode.F5, Key.F5 }, { KeyCode.F6, Key.F6 }, { KeyCode.F7, Key.F7 }, { KeyCode.F8, Key.F8 },
        { KeyCode.F9, Key.F9 }, { KeyCode.F10, Key.F10 }, { KeyCode.F11, Key.F11 }, { KeyCode.F12, Key.F12 },
        { KeyCode.Space, Key.Space }, { KeyCode.LeftShift, Key.LeftShift }, { KeyCode.RightShift, Key.RightShift },
        { KeyCode.LeftControl, Key.LeftCtrl }, { KeyCode.RightControl, Key.RightCtrl },
        { KeyCode.LeftAlt, Key.LeftAlt }, { KeyCode.RightAlt, Key.RightAlt },
        { KeyCode.Tab, Key.Tab }, { KeyCode.CapsLock, Key.CapsLock },
        { KeyCode.Escape, Key.Escape }, { KeyCode.Return, Key.Enter },
        { KeyCode.Backspace, Key.Backspace }, { KeyCode.Delete, Key.Delete },
        { KeyCode.Insert, Key.Insert }, { KeyCode.Home, Key.Home }, { KeyCode.End, Key.End },
        { KeyCode.PageUp, Key.PageUp }, { KeyCode.PageDown, Key.PageDown },
        { KeyCode.UpArrow, Key.UpArrow }, { KeyCode.DownArrow, Key.DownArrow },
        { KeyCode.LeftArrow, Key.LeftArrow }, { KeyCode.RightArrow, Key.RightArrow },
    };

    private void Awake() { Plugin.LogSource?.LogInfo("[KB] Awake"); }
    private void OnDestroy() { Plugin.LogSource?.LogInfo("[KB] OnDestroy"); }

    private void Update()
    {
        try
        {
            if (Time.realtimeSinceStartup - _lastLog > 5f)
            {
                _lastLog = Time.realtimeSinceStartup;
                Plugin.LogSource?.LogInfo($"[KB] Tick key={KeybindState.AimbotKey} intro={KeybindState.IntroActive} menu={KeybindState.MenuOpen}");
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            // Если идёт интро — проверяем только мышь и Insert (без перебора всех клавиш)
            if (KeybindState.IntroActive)
            {
                if (AnyKeyOrMousePressed())
                {
                    KeybindState.IntroActive = false;
                    KeybindState.IntroDone = true;
                    KeybindState.IntroAnimTime = 0f;
                    KeybindState.MenuOpen = true;
                    UiState.MenuVisible = true;
                    Plugin.LogSource?.LogInfo("[ESP] Intro cancelled by input, menu opened");
                }
                return;
            }

            // Insert — открыть/закрыть меню
            if (kb.insertKey.wasPressedThisFrame && !KeybindState.Capturing && KeybindState.IntroDone)
            {
                KeybindState.MenuOpen = !KeybindState.MenuOpen;
                UiState.MenuVisible = KeybindState.MenuOpen;
                UiState.MenuTab = 0;
                Plugin.LogSource?.LogInfo($"[ESP] Menu {(KeybindState.MenuOpen ? "opened" : "closed")}");
            }

            if (KeybindState.Capturing) { HandleCapture(); return; }
            if (IsTypingInChat()) return;
            if (KeybindState.AimbotKey == KeyCode.None) return;

            bool kd = false, kdDown = false;
            GetKeyState(KeybindState.AimbotKey, out kd, out kdDown);

            if (KeybindState.ToggleMode)
            {
                if (kdDown)
                {
                    AimbotState.Active = !AimbotState.Active;
                    Plugin.LogSource?.LogInfo($"[ESP] Aimbot toggled: {AimbotState.Active}");
                }
            }
            else
            {
                if (kd == KeybindState.LastKeyState) return;
                KeybindState.LastKeyState = kd;
                AimbotState.Active = kd;
            }
        }
        catch (Exception e) { Debug.LogError($"[KB] EX: {e.Message}"); }
    }

    private bool AnyKeyOrMousePressed()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) return true;
            if (mouse.rightButton.wasPressedThisFrame) return true;
            if (mouse.middleButton.wasPressedThisFrame) return true;
        }

        if (kb != null)
        {
            // Проверяем только Insert и Escape, не перебираем ВСЕ клавиши
            if (kb.insertKey.wasPressedThisFrame) return true;
            if (kb.escapeKey.wasPressedThisFrame) return true;
        }
        return false;
    }

    private void GetKeyState(KeyCode kc, out bool pressed, out bool pressedThisFrame)
    {
        pressed = false;
        pressedThisFrame = false;

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kc == KeyCode.Mouse0) { pressed = mouse != null && mouse.leftButton.isPressed; pressedThisFrame = mouse != null && mouse.leftButton.wasPressedThisFrame; return; }
        if (kc == KeyCode.Mouse1) { pressed = mouse != null && mouse.rightButton.isPressed; pressedThisFrame = mouse != null && mouse.rightButton.wasPressedThisFrame; return; }
        if (kc == KeyCode.Mouse2) { pressed = mouse != null && mouse.middleButton.isPressed; pressedThisFrame = mouse != null && mouse.middleButton.wasPressedThisFrame; return; }

        if (kb == null) return;

        if (KeyMap.TryGetValue(kc, out Key key))
        {
            var ctrl = kb[key];
            pressed = ctrl.isPressed;
            pressedThisFrame = ctrl.wasPressedThisFrame;
            return;
        }

        if (Enum.TryParse<Key>(kc.ToString(), out Key parsed))
        {
            var ctrl = kb[parsed];
            pressed = ctrl.isPressed;
            pressedThisFrame = ctrl.wasPressedThisFrame;
        }
    }

    private void HandleCapture()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            KeybindState.AimbotKey = KeybindState.CapturingBackup;
            KeybindState.Capturing = false;
            Plugin.LogSource?.LogInfo($"[KB] Cancelled, restored {KeybindState.AimbotKey}");
            return;
        }

        if (_waitingForRelease)
        {
            bool anyDown = false;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)) anyDown = true;
            if (!anyDown) _waitingForRelease = false;
            return;
        }

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) { SetBind(KeyCode.Mouse0); return; }
            if (mouse.rightButton.wasPressedThisFrame) { SetBind(KeyCode.Mouse1); return; }
            if (mouse.middleButton.wasPressedThisFrame) { SetBind(KeyCode.Mouse2); return; }
        }

        if (kb != null)
        {
            // Вместо перебора allKeys — проверяем только те, что в KeyMap
            foreach (var kvp in KeyMap)
            {
                var ctrl = kb[kvp.Value];
                if (ctrl != null && ctrl.wasPressedThisFrame)
                {
                    SetBind(kvp.Key);
                    return;
                }
            }
        }
    }

    private void SetBind(KeyCode kc)
    {
        KeybindState.AimbotKey = kc;
        KeybindState.Capturing = false;
        _waitingForRelease = true;
        if (Plugin.CfgAimbotKey != null) Plugin.CfgAimbotKey.Value = kc.ToString();
        Plugin.LogSource?.LogInfo($"[KB] Set {kc}");
    }

    private bool IsTypingInChat()
    {
        try
        {
            // Ищем HudManager ОДИН раз, а не каждый кадр
            if (!_hudTypeSearched)
            {
                _hudTypeSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { foreach (var t in asm.GetTypes()) if (t.Name == "HudManager") { _hudType = t; break; } } catch { }
                    if (_hudType != null) break;
                }
                Plugin.LogSource?.LogInfo($"[KB] HudManager search: {(_hudType != null ? "found" : "null")}");
            }

            if (_hudType == null) return false;

            var instProp = _hudType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instProp == null) return false;
            var hud = instProp.GetValue(null);
            if (hud == null) return false;

            var chatProp = _hudType.GetProperty("Chat", BindingFlags.Public | BindingFlags.Instance);
            if (chatProp == null) return false;
            var chat = chatProp.GetValue(hud);
            if (chat == null) return false;

            var openProp = chat.GetType().GetProperty("IsOpenOrOpening", BindingFlags.Public | BindingFlags.Instance);
            if (openProp == null) return false;
            var open = openProp.GetValue(chat);
            return open is bool o && o;
        }
        catch { return false; }
    }
}