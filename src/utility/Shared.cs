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
