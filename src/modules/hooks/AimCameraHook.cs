using UnityEngine;

namespace MyDynamicESP;

/// <summary>
/// Не используется в текущей архитектуре (лок делается в EspBehaviour.LateUpdate).
/// Оставлен на случай, если понадобится хук через OnPreCull (например, не-URP сборка).
/// </summary>
public class AimCameraHook : MonoBehaviour
{
    private Camera _cam;

    void Awake() { _cam = GetComponent<Camera>(); }

    void OnPreCull()
    {
        try
        {
            if (_cam == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            // Мгновенный лок без roll (Smooth больше нет — лок всегда мгновенный)
            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            _cam.transform.rotation = Quaternion.Euler(e.x, e.y, 0f);
        }
        catch { }
    }
}
