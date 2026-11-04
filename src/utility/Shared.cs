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
    public static KeyCode MenuKey = KeyCode.Insert;

    // Аимбот
    public static KeyCode AimbotKey = KeyCode.Mouse1;
    public static bool ToggleMode = false;
    public static bool Capturing = false;
    public static KeyCode CapturingBackup = KeyCode.None;
    public static bool AimbotActive = false; // зажат бинд или toggle on
    public static bool LastKeyState = false;

    // Интро
    public static bool IntroPlayed = false;
    public static bool IntroActive = false;
    public static float IntroAnimTime = 0f;
    public static bool IntroDone = false;

    public static bool MenuOpen = false;
}

public static class UiState
{
    public static bool MenuVisible = false;
    public static int MenuTab = 0;
}
