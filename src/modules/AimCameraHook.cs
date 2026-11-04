using UnityEngine;
using Il2CppInterop.Runtime.Attributes;

namespace MyDynamicESP;

public class AimCameraHook : MonoBehaviour
{
    private Camera _cam;
    private int _lastCamId = -1;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam != null) _lastCamId = _cam.GetInstanceID();
    }

    [HideFromIl2Cpp]
    public void Rebind(Camera cam)
    {
        if (cam == null) return;
        _cam = cam;
        _lastCamId = cam.GetInstanceID();
    }

    void OnPreCull()
    {
        try
        {
            if (_cam == null) return;
            if (!AimbotModule.Enabled) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.01f) return;

            Vector3 camForward = _cam.transform.forward;
            Quaternion delta = Quaternion.FromToRotation(camForward, AimbotModule.TargetDirection);

            float t = Mathf.Clamp01(AimbotModule.Smooth * Time.deltaTime);
            Quaternion smoothDelta = Quaternion.Slerp(Quaternion.identity, delta, t);

            _cam.transform.rotation = smoothDelta * _cam.transform.rotation;
        }
        catch { }
    }
}
