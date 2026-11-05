using UnityEngine;

namespace MyDynamicESP;

public static class BombStatusRenderer
{
    public static bool Enabled = true;

    private const float PadX = 12f;
    private const float PadY = 8f;
    private const float RowH = 18f;
    private const float BaseY = 80f;
    private const float RightOffset = 20f;
    private const float PanelWidth = 260f;
    private const float Gap = 8f;

    public static float GetHeight()
    {
        if (!Enabled || !BombModule.Planted) return 0f;
        return PadY * 2f + RowH * 3f;
    }

    public static void Draw(float y)
    {
        if (!Enabled) return;
        if (!KeybindState.IntroDone) return;
        if (!BombModule.Planted) return;

        float x = Screen.width - RightOffset - PanelWidth;

        int minutes = Mathf.FloorToInt(BombModule.TimeLeft / 60f);
        int seconds = Mathf.FloorToInt(BombModule.TimeLeft % 60f);
        string timer = $"{minutes:00}:{seconds:00}";

        float h = PadY * 2f + RowH * 3f;
        var rect = new Rect(x, y, PanelWidth, h);

        UiPanel.Draw(rect, 0.85f);

        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY, PanelWidth - PadX * 2f, RowH), "Bomb");
        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY + RowH, PanelWidth - PadX * 2f, RowH), $"Site: {BombModule.Site}");
        GUI.color = BombModule.TimeLeft <= 10f ? new Color(1f, 0.3f, 0.3f) : Color.white;
        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY + RowH * 2f, PanelWidth - PadX * 2f, RowH), $"Timer: {timer}");
        GUI.color = Color.white;
    }
}
