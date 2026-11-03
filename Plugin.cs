using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace MyDynamicESP;

[BepInPlugin("com.you.dynamicesp", "MyDynamicESP", "1.0.0.0")]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("[ESP] ===== Plugin.Load() =====");
        try
        {
            IL2CPPChainloader.AddUnityComponent<EspBehaviour>();
            Log.LogInfo("[ESP] EspBehaviour component added successfully");
        }
        catch (Exception e)
        {
            Log.LogError($"[ESP] Failed to add EspBehaviour: {e}");
        }
        Log.LogInfo("[ESP] ===== Plugin.Load() done =====");
    }
}

public class EspBehaviour : MonoBehaviour
{
    private class Tracked
    {
        public Transform Transform;
        public bool IsBot;
        public int Team;
        public string Name;
        public int LastSeenFrame;
    }

    private readonly Dictionary<int, Tracked> _tracked = new();
    private readonly List<Tracked> _toDraw = new();
    private readonly List<int> _seenList = new();

    private Camera _cam;
    private Texture2D _whiteTex;
    private float _scanTimer;
    private const float ScanInterval = 1.0f;

    private const float BoxWidth = 60f;
    private const float BoxHeight = 120f;
    private const float OutlineThickness = 2f;
    private const float HeadOffset = 1.8f;

    // ---- Настройки функций ----
    private bool _espBoxesEnabled = true;
    private bool _espNamesEnabled = true;
    private bool _espDistanceEnabled = true;
    private bool _menuVisible = false;

    // ---- Unity UI ----
    private GameObject _menuCanvas;
    private GameObject _menuPanel;

    private static Il2CppSystem.Type _botType;
    private static Il2CppSystem.Type _playerType;
    private static bool _resolved;
    private static bool _resolveFailed;

    private static PropertyInfo _localPlayerProp;
    private static PropertyInfo _teamProp;
    private int _localTeam = -1;
    private int _lastLoggedTeam = -2;

    // ---- Защита от спама логов ----
    private readonly Dictionary<string, float> _lastLogTime = new();
    private const float LogCooldown = 2.0f;

    // ---- Счётчики для отладки ----
    private int _updateCount;
    private float _lastUpdateLog;
    private int _scanCount;
    private int _guiFrameCount;

    private void LogThrottled(string key, string message, bool isError = false)
    {
        float now = Time.realtimeSinceStartup;
        if (_lastLogTime.TryGetValue(key, out float last) && now - last < LogCooldown)
            return;
        _lastLogTime[key] = now;
        if (isError) Debug.LogError(message);
        else Debug.Log(message);
    }

    // =========================================================
    //                       LIFECYCLE
    // =========================================================
    void Awake()
    {
        Debug.Log("[ESP] ===== Awake() =====");
        try
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
            Debug.Log("[ESP] White texture created");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] Awake texture EX: {e}");
        }

        try
        {
            BuildMenu();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] Awake BuildMenu EX: {e}");
        }
        Debug.Log("[ESP] ===== Awake() done =====");
    }

    void OnDestroy()
    {
        Debug.Log("[ESP] OnDestroy() - cleaning up");
        try
        {
            if (_menuCanvas != null) Destroy(_menuCanvas);
            if (_whiteTex != null) Destroy(_whiteTex);
        }
        catch (Exception e) { Debug.LogError($"[ESP] OnDestroy EX: {e}"); }
    }

    // =========================================================
    //                       UNITY UI MENU
    // =========================================================
    private void BuildMenu()
    {
        Debug.Log("[ESP] BuildMenu() start");
        _menuCanvas = new GameObject("ESP_MenuCanvas");
        DontDestroyOnLoad(_menuCanvas);
        var canvas = _menuCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var scaler = _menuCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _menuCanvas.AddComponent<GraphicRaycaster>();
        Debug.Log("[ESP] Canvas created");

        _menuPanel = new GameObject("Panel");
        _menuPanel.transform.SetParent(_menuCanvas.transform, false);
        var panelImg = _menuPanel.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        var panelRect = _menuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(20, -20);
        panelRect.sizeDelta = new Vector2(320, 300);

        CreateLabel(_menuPanel.transform, "DynamicESP", new Vector2(10, -10), 22, FontStyle.Bold, new Color(1f, 0.8f, 0.3f));

        float y = -55;
        y = CreateToggle(_menuPanel.transform, "ESP Boxes", y, _espBoxesEnabled, v =>
        {
            _espBoxesEnabled = v;
            Debug.Log($"[ESP] Menu: ESP Boxes = {v}");
        });
        y = CreateToggle(_menuPanel.transform, "Names", y, _espNamesEnabled, v =>
        {
            _espNamesEnabled = v;
            Debug.Log($"[ESP] Menu: Names = {v}");
        });
        y = CreateToggle(_menuPanel.transform, "Distance", y, _espDistanceEnabled, v =>
        {
            _espDistanceEnabled = v;
            Debug.Log($"[ESP] Menu: Distance = {v}");
        });

        y -= 10;
        CreateButton(_menuPanel.transform, "Hide Menu (Insert)", new Vector2(10, y), ToggleMenu);

        _menuPanel.SetActive(false);
        Debug.Log("[ESP] BuildMenu() done (panel hidden)");
    }

    private float CreateToggle(Transform parent, string text, float y, bool initial, Action<bool> onChange)
    {
        var row = new GameObject("Row_" + text);
        row.transform.SetParent(parent, false);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = new Color(0.12f, 0.12f, 0.16f, 1f);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(0, 1);
        rowRect.pivot = new Vector2(0, 1);
        rowRect.anchoredPosition = new Vector2(10, y);
        rowRect.sizeDelta = new Vector2(300, 38);

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(row.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 18;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.text = text;
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0, 0);
        txtRect.anchorMax = new Vector2(1, 1);
        txtRect.offsetMin = new Vector2(10, 0);
        txtRect.offsetMax = new Vector2(-70, 0);

        var btnGo = new GameObject("ToggleBtn");
        btnGo.transform.SetParent(row.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = initial ? new Color(0.2f, 0.7f, 0.3f, 1f) : new Color(0.6f, 0.2f, 0.2f, 1f);
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 0.5f);
        btnRect.anchorMax = new Vector2(1, 0.5f);
        btnRect.pivot = new Vector2(1, 0.5f);
        btnRect.anchoredPosition = new Vector2(-6, 0);
        btnRect.sizeDelta = new Vector2(56, 28);

        var btnTextGo = new GameObject("Text");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnText = btnTextGo.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        btnText.fontSize = 16;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        btnText.text = initial ? "ON" : "OFF";
        var btnTextRect = btnTextGo.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        bool state = initial;
        btn.onClick.AddListener((Action)(() =>
        {
            state = !state;
            btnImg.color = state ? new Color(0.2f, 0.7f, 0.3f, 1f) : new Color(0.6f, 0.2f, 0.2f, 1f);
            btnText.text = state ? "ON" : "OFF";
            try { onChange(state); }
            catch (Exception e) { Debug.LogError($"[ESP] Toggle '{text}' callback EX: {e}"); }
        }));

        return y - 46;
    }

    private GameObject CreateLabel(Transform parent, string text, Vector2 pos, int size, FontStyle style, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.color = color;
        txt.text = text;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(300, 30);
        return go;
    }

    private void CreateButton(Transform parent, string text, Vector2 pos, Action onClick)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.35f, 0.6f, 1f);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(300, 36);

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 18;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = text;
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener((Action)(() =>
        {
            try { onClick(); }
            catch (Exception e) { Debug.LogError($"[ESP] Button '{text}' EX: {e}"); }
        }));
    }

    private void ToggleMenu()
    {
        if (_menuPanel == null)
        {
            Debug.LogError("[ESP] ToggleMenu: _menuPanel is null!");
            return;
        }
        _menuVisible = !_menuVisible;
        _menuPanel.SetActive(_menuVisible);
        Cursor.visible = _menuVisible;
        Cursor.lockState = _menuVisible ? CursorLockMode.None : CursorLockMode.Locked;
        Debug.Log($"[ESP] Menu toggled: visible={_menuVisible}, cursor={(_menuVisible ? "unlocked" : "locked")}");
    }

    // =========================================================
    //                       UPDATE
    // =========================================================
    void Update()
    {
        try
        {
            _updateCount++;

            // ---- Ввод: Insert (основной) + F1 (альтернативный) ----
            try
            {
                if (Input.GetKeyDown(KeyCode.Insert))
                {
                    Debug.Log("[ESP] Insert pressed -> toggling menu");
                    ToggleMenu();
                }
                else if (Input.GetKeyDown(KeyCode.F1))
                {
                    Debug.Log("[ESP] F1 pressed -> toggling menu");
                    ToggleMenu();
                }
            }
            catch (Exception e)
            {
                LogThrottled("input_ex", $"[ESP] Input.GetKeyDown EX: {e.Message}", true);
            }

            // Периодический скан
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval)
            {
                _scanTimer = 0f;
                DoScan();
            }

            // Формируем список для отрисовки
            _toDraw.Clear();
            int skippedOwnTeam = 0, skippedNull = 0;
            foreach (var t in _tracked.Values)
            {
                if (t.Transform == null) { skippedNull++; continue; }
                if (_localTeam != -1 && t.Team == _localTeam) { skippedOwnTeam++; continue; }
                _toDraw.Add(t);
            }

            if (Time.realtimeSinceStartup - _lastUpdateLog > 5f)
            {
                _lastUpdateLog = Time.realtimeSinceStartup;
                Debug.Log($"[ESP] Update alive: tracked={_tracked.Count}, draw={_toDraw.Count}, " +
                          $"skippedOwnTeam={skippedOwnTeam}, skippedNull={skippedNull}, " +
                          $"localTeam={_localTeam}, menu={_menuVisible}, " +
                          $"boxes={_espBoxesEnabled}, names={_espNamesEnabled}, dist={_espDistanceEnabled}, " +
                          $"updates={_updateCount}");
            }
        }
        catch (Exception e)
        {
            LogThrottled("update_ex", $"[ESP] Update EX: {e}", true);
            _scanTimer = 0f;
        }
    }

    // =========================================================
    //                       SCAN
    // =========================================================
    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        Debug.Log("[ESP] ResolveTypes() start");
        try
        {
            Type bt = null, pt = null, pmType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    bt ??= asm.GetType("Chillow.StandChillow.Bots.Core.BotPlayerController");
                    pt ??= asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                    pmType ??= asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ESP] ResolveTypes asm '{asm.GetName().Name}' EX: {e.Message}");
                }
                if (bt != null && pt != null && pmType != null) break;
            }

            Debug.Log($"[ESP] Found types: Bot={bt?.FullName ?? "null"}, Player={pt?.FullName ?? "null"}, PM={pmType?.FullName ?? "null"}");

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);

            if (pmType != null && pt != null)
            {
                _localPlayerProp = pmType.GetProperty("DEEBDAFCBHCGFBA",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                _teamProp = pt.GetProperty("CGGHFHCAFFBBCBB",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                Debug.Log($"[ESP] Properties: LocalPlayer={(_localPlayerProp != null)}, Team={(_teamProp != null)}");
            }

            if (_botType == null && _playerType == null)
            {
                _resolveFailed = true;
                Debug.LogError("[ESP] ResolveTypes: NO types resolved! ESP will not work.");
            }
            else
            {
                Debug.Log($"[ESP] ResolveTypes OK: Bot={_botType != null}, Player={_playerType != null}");
            }
        }
        catch (Exception e)
        {
            _resolveFailed = true;
            Debug.LogError($"[ESP] ResolveTypes FATAL EX: {e}");
        }
    }

    void DoScan()
    {
        _scanCount++;
        try
        {
            ResolveTypes();
            if (_botType == null && _playerType == null)
            {
                if (_scanCount % 10 == 0)
                    Debug.LogWarning("[ESP] DoScan: no types resolved, skipping");
                return;
            }

            UpdateLocalTeam();

            _seenList.Clear();
            int beforeCount = _tracked.Count;
            ScanTypeInternal(_botType, true, "Bot");
            ScanTypeInternal(_playerType, false, "Player");

            var dead = new List<int>();
            foreach (var kv in _tracked)
                if (!_seenList.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) _tracked.Remove(id);

            int added = _tracked.Count - beforeCount + dead.Count;
            int removed = dead.Count;

            if (removed > 0 || added > 0 || _scanCount % 5 == 0)
            {
                Debug.Log($"[ESP] Scan #{_scanCount}: tracked={_tracked.Count} " +
                          $"(added={added}, removed={removed}), localTeam={_localTeam}");
            }
        }
        catch (Exception e)
        {
            LogThrottled("scan_ex", $"[ESP] DoScan EX: {e}", true);
        }
    }

    void UpdateLocalTeam()
    {
        try
        {
            if (_localPlayerProp == null || _teamProp == null) return;

            var pmType = _localPlayerProp.DeclaringType;
            object pmInstance = null;
            var instProp = pmType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instProp != null) pmInstance = instProp.GetValue(null);

            if (pmInstance == null)
            {
                if (_localTeam != _lastLoggedTeam)
                {
                    Debug.LogWarning("[ESP] UpdateLocalTeam: PlayerManager.Instance is null");
                    _lastLoggedTeam = _localTeam;
                }
                return;
            }

            var localPlayer = _localPlayerProp.GetValue(pmInstance);
            if (localPlayer == null)
            {
                if (_localTeam != _lastLoggedTeam)
                {
                    Debug.Log("[ESP] UpdateLocalTeam: localPlayer is null (not spawned yet?)");
                    _lastLoggedTeam = _localTeam;
                }
                return;
            }

            var teamObj = _teamProp.GetValue(localPlayer);
            if (teamObj != null)
            {
                int newTeam = Convert.ToInt32(teamObj);
                if (newTeam != _localTeam)
                {
                    Debug.Log($"[ESP] Local team changed: {_localTeam} -> {newTeam}");
                    _localTeam = newTeam;
                    _lastLoggedTeam = newTeam;
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled("localteam_ex", $"[ESP] UpdateLocalTeam EX: {e.Message}", true);
        }
    }

    void ScanTypeInternal(Il2CppSystem.Type type, bool isBot, string label)
    {
        if (type == null) return;
        try
        {
            var arr = FindObjectsOfType(type, false);
            if (arr == null)
            {
                LogThrottled($"find_null_{label}", $"[ESP] FindObjectsOfType({label}) returned null", true);
                return;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                try
                {
                    var o = arr[i];
                    if (o == null) continue;
                    var c = o.TryCast<Component>();
                    if (c == null) continue;
                    var tr = c.transform;
                    if (tr == null) continue;
                    var go = c.gameObject;
                    if (go == null) continue;

                    int id = go.GetInstanceID();
                    if (_seenList.Contains(id)) continue;
                    _seenList.Add(id);

                    int targetTeam = -1;
                    if (_teamProp != null)
                    {
                        try
                        {
                            var teamObj = _teamProp.GetValue(c);
                            if (teamObj != null) targetTeam = Convert.ToInt32(teamObj);
                        }
                        catch (Exception e)
                        {
                            LogThrottled($"team_{label}_{id}", $"[ESP] Get team EX ({label} id={id}): {e.Message}", true);
                        }
                    }

                    string name = TryGetName(c, isBot);

                    bool isNew = !_tracked.ContainsKey(id);
                    _tracked[id] = new Tracked
                    {
                        Transform = tr,
                        IsBot = isBot,
                        Team = targetTeam,
                        Name = name,
                        LastSeenFrame = Time.frameCount
                    };

                    if (isNew)
                        Debug.Log($"[ESP] +Tracked {label}: name='{name}', team={targetTeam}, id={id}");
                }
                catch (Exception e)
                {
                    LogThrottled($"scan_item_{label}", $"[ESP] Scan item EX ({label}): {e.Message}", true);
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled($"scan_type_{label}", $"[ESP] ScanTypeInternal({label}) EX: {e.Message}", true);
        }
    }

    string TryGetName(Component c, bool isBot)
    {
        try
        {
            var prop = c.GetType().GetProperty("HBBBCEFBCFEFBBA",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                var pp = prop.GetValue(c);
                if (pp != null)
                {
                    var nickProp = pp.GetType().GetProperty("NickName",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (nickProp != null)
                    {
                        var nick = nickProp.GetValue(pp) as string;
                        if (!string.IsNullOrEmpty(nick)) return nick;
                    }
                }
            }
        }
        catch (Exception e)
        {
            LogThrottled($"name_ex_{isBot}", $"[ESP] TryGetName EX (isBot={isBot}): {e.Message}", true);
        }
        try
        {
            var n = c.gameObject.name;
            if (!string.IsNullOrEmpty(n)) return n;
        }
        catch { }
        return isBot ? "BOT" : "PLAYER";
    }

    // =========================================================
    //                       OnGUI (ESP + кнопка меню)
    // =========================================================
    void OnGUI()
    {
        _guiFrameCount++;
        try
        {
            // Кнопка открытия меню (fallback, если клавиши перехвачены игрой)
            if (GUI.Button(new Rect(10, 10, 170, 30), _menuVisible ? "Close ESP Menu (Insert)" : "Open ESP Menu (Insert)"))
            {
                Debug.Log("[ESP] Menu button clicked");
                ToggleMenu();
            }

            if (!_espBoxesEnabled && !_espNamesEnabled)
            {
                if (_guiFrameCount % 600 == 0)
                    Debug.Log($"[ESP] OnGUI: all ESP disabled, drawing nothing (frame {_guiFrameCount})");
                return;
            }

            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
                Debug.LogWarning("[ESP] OnGUI: _whiteTex was null, recreated");
            }

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null)
                {
                    LogThrottled("no_cam", "[ESP] OnGUI: Camera.main is null!", true);
                    return;
                }
                Debug.Log($"[ESP] Camera found: {_cam.name}, pos={_cam.transform.position}");
            }

            int drawn = 0, skippedBehind = 0, skippedNull = 0;
            foreach (var t in _toDraw)
            {
                try
                {
                    Vector3 headWorld;
                    if (t.Transform == null) { skippedNull++; continue; }
                    headWorld = t.Transform.position + Vector3.up * HeadOffset;

                    Vector3 screen = _cam.WorldToScreenPoint(headWorld);
                    if (screen.z < 0) { skippedBehind++; continue; }

                    float x = screen.x;
                    float y = Screen.height - screen.y;
                    float dist = Vector3.Distance(_cam.transform.position, t.Transform.position);

                    if (_espBoxesEnabled)
                    {
                        float bw = BoxWidth;
                        float bh = BoxHeight;
                        float th = OutlineThickness;
                        float left = x - bw / 2f;
                        float top = y;
                        float bottom = y + bh;

                        Color fillColor = t.Team == 1
                            ? new Color(1f, 0f, 0f, 0.12f)
                            : new Color(0f, 0.5f, 1f, 0.12f);
                        Color outlineColor = t.Team == 1 ? Color.red : Color.cyan;

                        GUI.color = fillColor;
                        GUI.DrawTexture(new Rect(left, top, bw, bh), _whiteTex);

                        GUI.color = outlineColor;
                        GUI.DrawTexture(new Rect(left, top, bw, th), _whiteTex);
                        GUI.DrawTexture(new Rect(left, bottom - th, bw, th), _whiteTex);
                        GUI.DrawTexture(new Rect(left, top, th, bh), _whiteTex);
                        GUI.DrawTexture(new Rect(left + bw - th, top, th, bh), _whiteTex);
                        GUI.color = Color.white;
                    }

                    if (_espNamesEnabled)
                    {
                        string label = string.IsNullOrEmpty(t.Name)
                            ? (t.IsBot ? "BOT" : "PLAYER")
                            : t.Name;
                        if (_espDistanceEnabled)
                            label += $" [{dist:F0}m]";

                        float approxWidth = label.Length * 7f;
                        float left = x - approxWidth / 2f;

                        GUI.color = Color.black;
                        GUI.Label(new Rect(left - 1f, y - 19f, approxWidth, 20f), label);
                        GUI.Label(new Rect(left + 1f, y - 17f, approxWidth, 20f), label);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(left, y - 18f, approxWidth, 20f), label);
                    }

                    drawn++;
                }
                catch (Exception e)
                {
                    LogThrottled("gui_item", $"[ESP] OnGUI item EX: {e.Message}", true);
                }
            }

            if (_guiFrameCount % 600 == 0 && _toDraw.Count > 0)
            {
                Debug.Log($"[ESP] OnGUI: drawn={drawn}, total={_toDraw.Count}, " +
                          $"skippedBehind={skippedBehind}, skippedNull={skippedNull}");
            }
        }
        catch (Exception e)
        {
            LogThrottled("ongui_ex", $"[ESP] OnGUI EX: {e}", true);
        }
        finally
        {
            GUI.color = Color.white;
        }
    }
}
