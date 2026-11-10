using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace MyDynamicESP;

public static class BunnyhopModule
{
    public static bool Enabled = false;
    public static float Multiplier = 1.5f;

    private static float _lastHeight = -1f;
    private static bool _wasFalling = false;
    private static Transform _playerTransform = null;
    private static CharacterController _cc = null;
    private static Rigidbody _rb = null;

    public static void Update(Component localPlayer)
    {
        if (!Enabled || localPlayer == null) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.spaceKey.isPressed)
        {
            _wasFalling = false;
            return;
        }

        if (_playerTransform == null)
        {
            _playerTransform = localPlayer.transform;
            _cc = localPlayer.GetComponent<CharacterController>();
            _rb = localPlayer.GetComponent<Rigidbody>();
        }

        float currentHeight = _playerTransform.position.y;

        if (_lastHeight > currentHeight)
        {
            _wasFalling = true;
        }

        if (_wasFalling && currentHeight > _lastHeight)
        {
            ApplyBoost();
            _wasFalling = false;
        }

        _lastHeight = currentHeight;
    }

    private static void ApplyBoost()
    {
        try
        {
            if (_rb != null)
            {
                Vector3 vel = _rb.velocity;
                _rb.velocity = new Vector3(vel.x * Multiplier, vel.y, vel.z * Multiplier);
            }
            else if (_cc != null)
            {
                // CharacterController не имеет velocity напрямую. 
                // Пока заглушка, работает только если есть Rigidbody.
            }
        }
        catch { }
    }

    public static void Clear()
    {
        _playerTransform = null;
        _cc = null;
        _rb = null;
        _lastHeight = -1f;
        _wasFalling = false;
    }
}
