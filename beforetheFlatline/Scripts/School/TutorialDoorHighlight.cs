using Godot;

public partial class TutorialDoorHighlight : Node2D
{
    private float _elapsed;

    public TutorialDoorHighlight()
    {
        ZIndex = 100;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pulse = 1f + 0.10f * Mathf.Sin(_elapsed * 4f);
        float alpha = 0.76f + 0.20f * Mathf.Sin(_elapsed * 3f);
        Color glow = new(1f, 0.78f, 0.28f, alpha);
        float radius = 25f * pulse;

        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 40, glow, 3f, true);
        DrawArc(Vector2.Zero, radius + 7f, 0f, Mathf.Tau, 40,
            new Color(glow.R, glow.G, glow.B, alpha * 0.32f), 2f, true);
        DrawLine(new Vector2(0f, -radius - 22f), new Vector2(0f, -radius - 5f), glow, 3f, true);
        DrawLine(new Vector2(0f, -radius - 5f), new Vector2(-6f, -radius - 13f), glow, 3f, true);
        DrawLine(new Vector2(0f, -radius - 5f), new Vector2(6f, -radius - 13f), glow, 3f, true);
        DrawString(ThemeDB.FallbackFont, new Vector2(-43f, -radius - 30f),
            "GO HERE", HorizontalAlignment.Center, 86f, 11, glow);
    }
}
