using UnityEngine;

namespace MyDynamicESP;

public class AimCameraHook : MonoBehaviour
{
    private Camera _cam;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void OnPreCull()
    {
        try
        {
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            Quaternion targetRot = Quaternion.Euler(e.x, e.y, 0f);

            var holder = AimShared.MainCameraHolder;
            if (holder != null)
                holder.rotation = targetRot;
            else if (_cam != null)
                _cam.transform.rotation = targetRot;
        }
        catch { }
    }
}
