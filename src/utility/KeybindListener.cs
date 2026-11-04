using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace MyDynamicESP;

public class KeybindListener : MonoBehaviour
{
    private void Awake() { Plugin.LogSource?.LogInfo("[KB] Awake"); }
    private void OnDestroy() { Plugin.LogSource?.LogInfo("[KB] OnDestroy"); }

    private void Update()
    {
        try
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Интро: отменяется любой клавишей или мышью
            if (KeybindState.IntroActive)
            {
                bool pressed = false;
                var mouse = Mouse.current;
                if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
                    pressed = true;

                if (kb.insertKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                    pressed = true;

                if (pressed)
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
            if (kb.insertKey.wasPressedThisFrame)
            {
                KeybindState.MenuOpen = !KeybindState.MenuOpen;
                UiState.MenuVisible = KeybindState.MenuOpen;
                UiState.MenuTab = 0;
                Plugin.LogSource?.LogInfo($"[ESP] Menu {(KeybindState.MenuOpen ? "opened" : "closed")}");
            }
        }
        catch (Exception e) { Debug.LogError($"[KB] EX: {e.Message}"); }
    }
}
