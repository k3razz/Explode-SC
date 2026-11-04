using UnityEngine;

namespace MyDynamicESP;

public class AimCameraHook : MonoBehaviour
{
    private Camera _cam;

    void Awake() { _cam = GetComponent<Camera>(); }

    void OnPreCull()
    {
        if (_cam == null) return;
        if (!AimbotModule.Enabled || !KeybindState.AimbotActive) return;
        if (!AimbotModule.HasTarget) return;
        if (AimbotModule.TargetDirection.sqrMagnitude < 0.01f) return;

        Vector3 camForward = _cam.transform.forward;
        Quaternion delta = Quaternion.FromToRotation(camForward, AimbotModule.TargetDirection);

        float t = Mathf.Clamp01(AimbotModule.Smooth * Time.deltaTime);
        Quaternion smoothDelta = Quaternion.Slerp(Quaternion.identity, delta, t);

        _cam.transform.rotation = smoothDelta * _cam.transform.rotation;
    }
}
