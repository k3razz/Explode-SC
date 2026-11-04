using UnityEngine;
using UnityEngine.Rendering;
using Il2CppInterop.Runtime;
using System;

namespace MyDynamicESP;

public static class AimRenderHook
{
    private static bool _subscribed;
    private static float _diagTimer;
    private static int _calls;
    private static Il2CppSystem.Action<ScriptableRenderContext, Camera> _delegate;

    public static void Subscribe()
    {
        if (_subscribed) return;

        try
        {
            _delegate = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ScriptableRenderContext, Camera>>(
                new Action<ScriptableRenderContext, Camera>(OnBeginCameraRendering));

            RenderPipelineManager.beginCameraRendering = 
                (Il2CppSystem.Action<ScriptableRenderContext, Camera>)Il2CppSystem.Delegate.Combine(
                    RenderPipelineManager.beginCameraRendering, _delegate);

            _subscribed = true;
            Plugin.LogSource?.LogInfo("[AIM] Subscribed to beginCameraRendering");
        }
        catch (Exception e)
        {
            Plugin.LogSource?.LogError($"[AIM] beginCameraRendering subscribe EX: {e.Message}");
        }
    }

    public static void Unsubscribe()
    {
        if (!_subscribed || _delegate == null) return;

        try
        {
            RenderPipelineManager.beginCameraRendering = 
                (Il2CppSystem.Action<ScriptableRenderContext, Camera>)Il2CppSystem.Delegate.Remove(
                    RenderPipelineManager.beginCameraRendering, _delegate);
            _subscribed = false;
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
