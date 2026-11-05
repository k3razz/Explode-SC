using UnityEngine;

namespace MyDynamicESP;

public static class BombStatusRenderer
{
    public static bool Enabled = true;
    public static Vector2 Position = new Vector2(-1f, -1f);
    public static bool IsDragging = false;
    public static Vector2 DragOffset = Vector2.zero;

    private const float PadX = 12f;
    private const float PadY = 8f;
    private const float RowH = 18f;
    private const float PanelWidth = 260f;
    private const float DefaultX = 20f;
    private const float DefaultY = 180f;

    private static Vector2 GetPosition()
    {
        if (Position.x < 0f || Position.y < 0f)
            return new Vector2(Screen.width - PanelWidth - DefaultX, DefaultY);
        return Position;
    }

    public static Rect GetRect()
    {
        float h = PadY * 2f + RowH * 3f;
        var pos = GetPosition();
        return new Rect(pos.x, pos.y, PanelWidth, h);
    }

    public static void HandleDrag()
    {
        var rect = GetRect();
        Event e = Event.current;
        if (e == null) return;
        Vector2 mouse = e.mousePosition;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (rect.Contains(mouse) && !UiDragState.AnyDragging)
            {
                IsDragging = true;
                UiDragState.AnyDragging = true;
                DragOffset = mouse - new Vector2(rect.x, rect.y);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && IsDragging)
        {
            Vector2 newPos = mouse - DragOffset;
            Position = newPos;
            Plugin.CfgBombStatusX.Value = newPos.x;
            Plugin.CfgBombStatusY.Value = newPos.y;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && IsDragging)
        {
            IsDragging = false;
            UiDragState.AnyDragging = false;
            e.Use();
        }
    }

    public static void Draw()
    {
        if (!Enabled) return;
        if (!KeybindState.IntroDone) return;
        if (!KeybindState.MenuOpen && !BombEspModule.Planted) return;

        var rect = GetRect();
        UiPanel.Draw(rect, 0.85f);

        GUI.color = new Color(1f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string site = BombEspModule.Planted ? BombEspModule.Site : "-";
        int minutes = Mathf.FloorToInt(BombEspModule.TimeLeft / 60f);
        int seconds = Mathf.FloorToInt(BombEspModule.TimeLeft % 60f);
        string timer = $"{minutes:00}:{seconds:00}";

        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY, PanelWidth - PadX * 2f, RowH), "Bomb");
        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY + RowH, PanelWidth - PadX * 2f, RowH), $"Site: {site}");
        GUI.color = BombEspModule.Planted && BombEspModule.TimeLeft <= 10f ? new Color(1f, 0.3f, 0.3f) : Color.white;
        GUI.Label(new Rect(rect.x + PadX, rect.y + PadY + RowH * 2f, PanelWidth - PadX * 2f, RowH), $"Timer: {timer}");
        GUI.color = Color.white;
    }
}
