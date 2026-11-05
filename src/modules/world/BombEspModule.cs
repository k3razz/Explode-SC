    public static void Update()
    {
        float now = Time.time;
        if (now - _lastCheck < CheckInterval) return;
        _lastCheck = now;

        try
        {
            ResolveTypes();
            if (_bombManagerType == null) return;

            IntPtr bmPtr = FindBombManagerPtr();
            IntPtr plantedPtr = IntPtr.Zero;
            if (bmPtr != IntPtr.Zero)
            {
                try { plantedPtr = Marshal.ReadIntPtr(bmPtr, OFF_BM_CGQT); } catch { }
            }

            _logTimer += CheckInterval;
            if (_logTimer >= 2f)
            {
                _logTimer = 0f;
                Plugin.LogSource?.LogInfo($"[BOMB] bmPtr={bmPtr != IntPtr.Zero} plantedPtr={plantedPtr != IntPtr.Zero} Planted={Planted}");
            }

            if (bmPtr == IntPtr.Zero) { if (Planted) Reset(); return; }
            if (plantedPtr == IntPtr.Zero) { if (Planted) Reset(); return; }

            var plantedTr = TryGetTransform(plantedPtr);
            if (plantedTr == null) { if (Planted) Reset(); return; }

            if (!Planted)
            {
                _plantedAt = now;
                Planted = true;
                Plugin.LogSource?.LogInfo("[BOMB] Planted detected");
            }

            Position = plantedTr.position;
            Site = DetectSite(Position, bmPtr);

            float elapsed = now - _plantedAt;
            float remain = Mathf.Max(0f, FuseTime - elapsed);
            TimeLeft = remain;

            if (remain <= 0f && Planted) { Planted = false; _plantedAt = -1f; }
        }
        catch (Exception e) { Plugin.LogSource?.LogError($"[BOMB] Update EX: {e.Message}"); }
    }
