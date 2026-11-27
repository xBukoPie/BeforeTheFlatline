using Godot;

public partial class FlowchartDecisionConnectorControl : Control
{
    [Export] public string Kind { get; set; } = "Split";

    private static readonly Color LineColor = new("7dd3fc");

    public FlowchartDecisionConnectorControl()
    {
        CustomMinimumSize = new Vector2(0, 28);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float width = Size.X;
        float height = Size.Y;
        if (width < 4f || height < 8f)
            return;

        float left = width * 0.25f;
        float right = width * 0.75f;
        float center = width * 0.5f;

        if (Kind == "Split")
        {
            DrawLine(new Vector2(center, 0f), new Vector2(center, 7f), LineColor, 2f, true);
            DrawLine(new Vector2(center, 7f), new Vector2(left, height - 7f), LineColor, 2f, true);
            DrawLine(new Vector2(center, 7f), new Vector2(right, height - 7f), LineColor, 2f, true);
            DrawArrowDown(left, height - 7f);
            DrawArrowDown(right, height - 7f);
            return;
        }

        DrawLine(new Vector2(left, 0f), new Vector2(left, 8f), LineColor, 2f, true);
        DrawLine(new Vector2(right, 0f), new Vector2(right, 8f), LineColor, 2f, true);
        DrawLine(new Vector2(left, 8f), new Vector2(center, height - 7f), LineColor, 2f, true);
        DrawLine(new Vector2(right, 8f), new Vector2(center, height - 7f), LineColor, 2f, true);
        DrawArrowDown(center, height - 7f);
    }

    private void DrawArrowDown(float x, float y)
    {
        DrawLine(new Vector2(x, y), new Vector2(x - 5f, y - 7f), LineColor, 2f, true);
        DrawLine(new Vector2(x, y), new Vector2(x + 5f, y - 7f), LineColor, 2f, true);
    }
}
