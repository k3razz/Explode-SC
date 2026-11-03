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
        Log.LogInfo("ESP loading...");
        IL2CPPChainloader.AddUnityComponent<EspBehaviour>();
        Log.LogInfo("ESP component added");
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

    // ---- Настройки функций (переключаются в меню) ----
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

    private static PropertyInfo _localPlayerProp;
    private static PropertyInfo _teamProp;
    private int _localTeam = -1;

    void Awake()
    {
        Debug.Log("[ESP] Awake");
        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
        BuildMenu();
    }

    // =========================================================
    //                       UNITY UI MENU
    // =========================================================
    private void BuildMenu()
    {
        try
        {
            _menuCanvas = new GameObject("ESP_MenuCanvas");
            DontDestroyOnLoad(_menuCanvas);
            var canvas = _menuCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            var scaler = _menuCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _menuCanvas.AddComponent<GraphicRaycaster>();

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
            y = CreateToggle(_menuPanel.transform, "ESP Boxes", y, _espBoxesEnabled, v => _espBoxesEnabled = v);
            y = CreateToggle(_menuPanel.transform, "Names", y, _espNamesEnabled, v => _espNamesEnabled = v);
            y = CreateToggle(_menuPanel.transform, "Distance", y, _espDistanceEnabled, v => _espDistanceEnabled = v);

            y -= 10;
            CreateButton(_menuPanel.transform, "Hide Menu (F1)", new Vector2(10, y), () => ToggleMenu());

            _menuPanel.SetActive(false);
            Debug.Log("[ESP] Menu built");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ESP] BuildMenu EX: {e}");
        }
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
            onChange(state);
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
        btn.onClick.AddListener((Action)onClick);
    }

    private void ToggleMenu()
    {
        if (_menuPanel == null) return;
        _menuVisible = !_menuVisible;
        _menuPanel.SetActive(_menuVisible);
        Cursor.visible = _menuVisible;
        Cursor.lockState = _menuVisible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // =========================================================
    //                       UPDATE
    // =========================================================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            ToggleMenu();

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0f;
            DoScan();
        }

        _toDraw.Clear();
        foreach (var t in _tracked.Values)
        {
            if (t.Transform == null) continue;
            if (_localTeam != -1 && t.Team == _localTeam) continue;
            _toDraw.Add(t);
        }
    }

    private static void ResolveTypes()
    {
        if (_resolved) return;
        _resolved = true;
        try
        {
            Type bt = null, pt = null, pmType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                bt ??= asm.GetType("Chillow.StandChillow.Bots.Core.BotPlayerController");
                pt ??= asm.GetType("Axlebolt.Standoff.Player.PlayerController");
                pmType ??= asm.GetType("Axlebolt.Standoff.Player.PlayerManager");
                if (bt != null && pt != null && pmType != null) break;
            }

            if (bt != null) _botType = Il2CppType.From(bt);
            if (pt != null) _playerType = Il2CppType.From(pt);

            if (pmType != null && pt != null)
            {
                _localPlayerProp = pmType.GetProperty("DEEBDAFCBHCGFBA", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                _teamProp = pt.GetProperty("CGGHFHCAFFBBCBB", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            Debug.Log($"[ESP] Types: Bot={(_botType != null)}, Player={(_playerType != null)}, LocalProp={(_localPlayerProp != null)}");
        }
        catch (Exception e) { Debug.LogError($"[ESP] ResolveTypes EX: {e.Message}"); }
    }

    void DoScan()
    {
        ResolveTypes();
        if (_botType == null && _playerType == null) return;

        UpdateLocalTeam();

        _seenList.Clear();
        ScanTypeInternal(_botType, true);
        ScanTypeInternal(_playerType, false);

        var dead = new List<int>();
        foreach (var id in _tracked.Keys)
            if (!_seenList.Contains(id)) dead.Add(id);
        foreach (var id in dead) _tracked.Remove(id);
    }

    void UpdateLocalTeam()
    {
        try
        {
            if (_localPlayerProp == null || _teamProp == null) return;

            var pmType = _localPlayerProp.DeclaringType;
            object pmInstance = null;
            var instProp = pmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instProp != null) pmInstance = instProp.GetValue(null);

            var localPlayer = _localPlayerProp.GetValue(pmInstance);
            if (localPlayer != null)
            {
                var teamObj = _teamProp.GetValue(localPlayer);
                if (teamObj != null) _localTeam = Convert.ToInt32(teamObj);
            }
        }
        catch { }
    }

    void ScanTypeInternal(Il2CppSystem.Type type, bool isBot)
    {
        if (type == null) return;
        try
        {
            var arr = FindObjectsOfType(type, false);
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
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
                try
                {
                    if (_teamProp != null)
                    {
                        var teamObj = _teamProp.GetValue(c);
                        if (teamObj != null) targetTeam = Convert.ToInt32(teamObj);
                    }
                }
                catch { }

                _tracked[id] = new Tracked
                {
                    Transform = tr,
                    IsBot = isBot,
                    Team = targetTeam,
                    Name = TryGetName(c, isBot)
                };
            }
        }
        catch (Exception e) { Debug.LogError($"[ESP] ScanType EX: {e.Message}"); }
    }

    string TryGetName(Component c, bool isBot)
    {
        try
        {
            var prop = c.GetType().GetProperty("HBBBCEFBCFEFBBA", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                var pp = prop.GetValue(c);
                if (pp != null)
                {
                    var nickProp = pp.GetType().GetProperty("NickName", BindingFlags.Public | BindingFlags.Instance);
                    if (nickProp != null)
                    {
                        var nick = nickProp.GetValue(pp) as string;
                        if (!string.IsNullOrEmpty(nick)) return nick;
                    }
                }
            }
        }
        catch { }
        try { var n = c.gameObject.name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
        return isBot ? "BOT" : "PLAYER";
    }

    // =========================================================
    //                       OnGUI (ESP)
    // =========================================================
    void OnGUI()
    {
        if (!_espBoxesEnabled && !_espNamesEnabled) return;

        // Страховка на случай, если Awake не успел выполниться
        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        foreach (var t in _toDraw)
        {
            Vector3 headWorld;
            try { headWorld = t.Transform.position + Vector3.up * HeadOffset; }
            catch { continue; }

            Vector3 screen = _cam.WorldToScreenPoint(headWorld);
            if (screen.z < 0) continue;

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

                Color fillColor = t.Team == 1 ? new Color(1f, 0f, 0f, 0.12f) : new Color(0f, 0.5f, 1f, 0.12f);
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
                string label = string.IsNullOrEmpty(t.Name) ? (t.IsBot ? "BOT" : "PLAYER") : t.Name;
                if (_espDistanceEnabled)
                    label += $" [{dist:F0}m]";

                // Приблизительная ширина для центровки (дефолтный шрифт ~7px на символ)
                float approxWidth = label.Length * 7f;
                float left = x - approxWidth / 2f;

                // Тень (два прохода)
                GUI.color = Color.black;
                GUI.Label(new Rect(left - 1f, y - 19f, approxWidth, 20f), label);
                GUI.Label(new Rect(left + 1f, y - 17f, approxWidth, 20f), label);
                // Основной текст
                GUI.color = Color.white;
                GUI.Label(new Rect(left, y - 18f, approxWidth, 20f), label);
            }
        }
        GUI.color = Color.white;
    }
}
