using Godot;

public partial class FlowchartDecisionConnectorControl : Control
{
    [Export] public string Kind { get; set; } = "Split";
    [Export] public bool HasTrueBranch { get; set; }
    [Export] public bool HasFalseBranch { get; set; }
    [Export] public bool HasFollowingBlock { get; set; }

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
            DrawLine(new Vector2(center, 0f), new Vector2(center, 6f), LineColor, 2f, true);
            DrawBranch(center, left, height - 7f, HasTrueBranch);
            DrawBranch(center, right, height - 7f, HasFalseBranch);
            return;
        }

        DrawLine(new Vector2(left, 0f), new Vector2(left, 8f), LineColor, 2f, true);
        DrawLine(new Vector2(right, 0f), new Vector2(right, 8f), LineColor, 2f, true);
        DrawLine(new Vector2(left, 8f), new Vector2(center, height - 7f), LineColor, 2f, true);
        DrawLine(new Vector2(right, 8f), new Vector2(center, height - 7f), LineColor, 2f, true);
        DrawLine(new Vector2(center, height - 7f), new Vector2(center, height), LineColor, 2f, true);

        if (HasFollowingBlock)
            DrawArrowDown(center, height - 1f);
    }

    private void DrawBranch(float sourceX, float targetX, float y, bool hasTarget)
    {
        DrawLine(new Vector2(sourceX, 0f), new Vector2(targetX, y), LineColor, 2f, true);
        if (hasTarget)
            DrawArrowDown(targetX, y);
    }

    private void DrawArrowDown(float x, float y)
    {
        DrawLine(new Vector2(x, y), new Vector2(x - 5f, y - 7f), LineColor, 2f, true);
        DrawLine(new Vector2(x, y), new Vector2(x + 5f, y - 7f), LineColor, 2f, true);
    }
}
