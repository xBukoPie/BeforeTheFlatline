using Godot;

public partial class FlowchartDecisionConnectorControl : Control
{
    [Export] public string Kind { get; set; } = "Split";
    [Export] public bool HasTrueBranch { get; set; }
    [Export] public bool HasFalseBranch { get; set; }
    [Export] public bool HasFollowingBlock { get; set; }

    private static readonly Color LineColor = new("7dd3fc");
    private static readonly Color TrueColor = new("86efac");
    private static readonly Color FalseColor = new("fca5a5");

    public FlowchartDecisionConnectorControl()
    {
        CustomMinimumSize = new Vector2(0, 36);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float width = Size.X;
        float height = Size.Y;
        if (width < 4f || height < 12f)
            return;

        float left = width * 0.25f;
        float right = width * 0.75f;
        float center = width * 0.5f;
        float junctionY = 7f;
        float endY = height - 7f;

        if (Kind == "Split")
        {
            DrawLine(new Vector2(center, 0f), new Vector2(center, junctionY), LineColor, 2f, true);
            DrawLine(new Vector2(left, junctionY), new Vector2(right, junctionY), LineColor, 2f, true);
            DrawLine(new Vector2(left, junctionY), new Vector2(left, endY), LineColor, 2f, true);
            DrawLine(new Vector2(right, junctionY), new Vector2(right, endY), LineColor, 2f, true);

            if (HasFalseBranch)
                DrawArrowDown(left, endY);
            if (HasTrueBranch)
                DrawArrowDown(right, endY);

            DrawString(ThemeDB.FallbackFont, new Vector2(left - 50f, 25f), "FALSE",
                HorizontalAlignment.Right, 44f, 10, FalseColor);
            DrawString(ThemeDB.FallbackFont, new Vector2(right + 6f, 25f), "TRUE",
                HorizontalAlignment.Left, 44f, 10, TrueColor);
            return;
        }

        DrawLine(new Vector2(left, 0f), new Vector2(left, junctionY), LineColor, 2f, true);
        DrawLine(new Vector2(right, 0f), new Vector2(right, junctionY), LineColor, 2f, true);
        DrawLine(new Vector2(left, junctionY), new Vector2(right, junctionY), LineColor, 2f, true);
        DrawLine(new Vector2(center, junctionY), new Vector2(center, height), LineColor, 2f, true);

        if (HasFollowingBlock)
            DrawArrowDown(center, height - 1f);
    }

    private void DrawArrowDown(float x, float y)
    {
        DrawLine(new Vector2(x, y), new Vector2(x - 5f, y - 7f), LineColor, 2f, true);
        DrawLine(new Vector2(x, y), new Vector2(x + 5f, y - 7f), LineColor, 2f, true);
    }
}
