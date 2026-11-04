using UnityEngine;
using UnityEngine.Rendering;
using Il2CppInterop.Runtime;

namespace MyDynamicESP;

public static class AimRenderHook
{
    private static bool _subscribed;
    private static float _diagTimer;
    private static int _calls;
    private static Il2CppSystem.Action<ScriptableRenderContext, Camera> _srpDelegate;

    public static void Subscribe()
    {
        if (_subscribed) return;

        try
        {
            if (_srpDelegate == null)
            {
                _srpDelegate = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ScriptableRenderContext, Camera>>(
                    new System.Action<ScriptableRenderContext, Camera>(OnBeginCameraRendering));
            }
            RenderPipelineManager.beginCameraRendering = _srpDelegate;
            Plugin.LogSource?.LogInfo("[AIM] Subscribed to beginCameraRendering (direct assign)");
        }
        catch (System.Exception e)
        {
            Plugin.LogSource?.LogError($"[AIM] beginCameraRendering subscribe EX: {e.Message}");
        }

        _subscribed = true;
    }

    public static void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;

        try
        {
            if (_srpDelegate != null) RenderPipelineManager.beginCameraRendering = null;
        }
        catch { }
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        Apply(cam, "beginCameraRendering");
    }

    private static void Apply(Camera cam, string source)
    {
        _calls++;
        try
        {
            if (cam == null) return;
            if (!AimbotModule.Enabled) return;
            if (!KeybindState.AimbotActive) return;
            if (!AimbotModule.HasTarget) return;
            if (AimbotModule.TargetDirection.sqrMagnitude < 0.0001f) return;

            var main = Camera.main;
            if (main != null && cam != main) return;

            Quaternion look = Quaternion.LookRotation(AimbotModule.TargetDirection, Vector3.up);
            Vector3 e = look.eulerAngles;
            Quaternion targetRot = Quaternion.Euler(e.x, e.y, 0f);

            var holder = AimShared.MainCameraHolder;
            if (holder != null)
                holder.rotation = targetRot;
            else
                cam.transform.rotation = targetRot;

            _diagTimer += Time.unscaledDeltaTime;
            if (_diagTimer >= 1f)
            {
                _diagTimer = 0f;
                Plugin.LogSource?.LogInfo($"[AIM] {source} calls={_calls} holder={(holder != null ? holder.name : "null")} cam={cam.name} tgt={AimbotModule.TargetDirection}");
            }
        }
        catch { }
    }
}