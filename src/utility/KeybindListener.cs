using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

namespace MyDynamicESP;

public class KeybindListener : MonoBehaviour
{
    private bool _waitingForRelease = false;
    private float _dbgTimer;

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
            var kb = Keyboard.current;
            if (kb == null) return;

            if (KeybindState.IntroActive)
            {
                var mouse = Mouse.current;
                bool pressed = kb.insertKey.wasPressedThisFrame
                    || kb.escapeKey.wasPressedThisFrame
                    || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame));

                if (pressed)
                {
                    KeybindState.IntroActive = false;
                    KeybindState.IntroDone = true;
                    KeybindState.IntroAnimTime = 0f;
                    KeybindState.MenuOpen = true;
                    UiState.MenuVisible = true;
                }
                return;
            }

            if (kb.insertKey.wasPressedThisFrame && !KeybindState.Capturing)
            {
                KeybindState.MenuOpen = !KeybindState.MenuOpen;
                UiState.MenuVisible = KeybindState.MenuOpen;
                UiState.MenuTab = 0;
            }

            if (KeybindState.Capturing) { HandleCapture(); return; }

            if (KeybindState.AimbotKey == KeyCode.None)
            {
                KeybindState.AimbotActive = false;
                return;
            }

            bool kd, kdDown;
            GetKeyState(KeybindState.AimbotKey, out kd, out kdDown);

            if (KeybindState.ToggleMode)
            {
                if (kdDown) KeybindState.AimbotActive = !KeybindState.AimbotActive;
            }
            else
            {
                KeybindState.AimbotActive = kd;
            }

            _dbgTimer += Time.deltaTime;
            if (_dbgTimer >= 1f)
            {
                _dbgTimer = 0f;
                Plugin.LogSource?.LogInfo($"[KB] key={KeybindState.AimbotKey} active={KeybindState.AimbotActive} capturing={KeybindState.Capturing} toggle={KeybindState.ToggleMode} kd={kd} kdDown={kdDown}");
            }
        }
        catch (Exception e) { Debug.LogError($"[KB] EX: {e.Message}"); }
    }

    private void GetKeyState(KeyCode kc, out bool pressed, out bool pressedThisFrame)
    {
        pressed = false;
        pressedThisFrame = false;

        var mouse = Mouse.current;
        var kb = Keyboard.current;

        if (kc == KeyCode.Mouse0)
        {
            if (mouse != null) { pressed = mouse.leftButton.isPressed; pressedThisFrame = mouse.leftButton.wasPressedThisFrame; }
            return;
        }
        if (kc == KeyCode.Mouse1)
        {
            if (mouse != null) { pressed = mouse.rightButton.isPressed; pressedThisFrame = mouse.rightButton.wasPressedThisFrame; }
            return;
        }
        if (kc == KeyCode.Mouse2)
        {
            if (mouse != null) { pressed = mouse.middleButton.isPressed; pressedThisFrame = mouse.middleButton.wasPressedThisFrame; }
            return;
        }

        if (kb == null) return;

        if (KeyMap.TryGetValue(kc, out Key key))
        {
            var ctrl = kb[key];
            if (ctrl != null) { pressed = ctrl.isPressed; pressedThisFrame = ctrl.wasPressedThisFrame; }
            return;
        }

        if (Enum.TryParse<Key>(kc.ToString(), out Key parsed))
        {
            var ctrl = kb[parsed];
            if (ctrl != null) { pressed = ctrl.isPressed; pressedThisFrame = ctrl.wasPressedThisFrame; }
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
            KeybindState.AimbotActive = false;
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
        KeybindState.AimbotActive = false;
        _waitingForRelease = true;
        if (Plugin.CfgAimbotKey != null) Plugin.CfgAimbotKey.Value = kc.ToString();
    }
}
