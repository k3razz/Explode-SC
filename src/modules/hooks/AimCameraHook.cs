using UnityEngine;

namespace MyDynamicESP;

public class AimCameraHook : MonoBehaviour
{
    private Camera _cam;
    private Transform _holder;
    private float _checkTimer;
    private const float CheckInterval = 1.0f;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        // Периодически обновляем ссылку на holder (мог появиться позже)
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= CheckInterval)
        {
            _checkTimer = 0f;
            if (_holder == null && EspBehaviour.Instance != null)
                _holder = EspBehaviour.Instance.MainCameraHolder;
        }
    }

    void OnPreCull()
    {
        try
        {
            if (_cam == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            // Обновляем holder, если ещё не нашли
            if (_holder == null && EspBehaviour.Instance != null)
                _holder = EspBehaviour.Instance.MainCameraHolder;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            Quaternion targetRot = Quaternion.Euler(e.x, e.y, 0f);

            if (_holder != null)
                _holder.rotation = targetRot;
            else
                _cam.transform.rotation = targetRot;
        }
        catch { }
    }
}
