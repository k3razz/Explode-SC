using UnityEngine;

namespace MyDynamicESP;

public static class TeamFilter
{
    public static bool ShowTr = true;
    public static bool ShowCt = true;
    public static bool ShowSpectator = false;
    public static bool OnlyEnemies = false;
}

public static class KeybindState
{
    public static KeyCode AimbotKey = KeyCode.Mouse1;
    public static bool ToggleMode = false;
    public static bool Capturing = false;
    public static KeyCode CapturingBackup = KeyCode.None;

    // Меню
    public static KeyCode MenuKey = KeyCode.Insert;
    public static bool MenuOpen = false;
    public static float MenuAnimTime = 0f; // секунды с момента открытия
}

public static class AimbotState
{
    private static bool _active = false;
    public static bool Active
    {
        get => _active;
        set { _active = value; }
    }
}

public static class UiState
{
    public static bool MenuVisible = false;
    public static int MenuTab = 0;
}
