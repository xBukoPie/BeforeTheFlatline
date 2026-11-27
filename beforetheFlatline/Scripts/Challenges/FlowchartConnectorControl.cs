using Godot;

public partial class FlowchartConnectorControl : Control
{
    [Export] public string Kind { get; set; } = "Down";
    private static readonly Color ConnectorColor = new("7dd3fc");
    private static readonly Color BranchColor = new("c4b5fd");

    public event System.Action<Variant> Dropped;

    public FlowchartConnectorControl()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 34);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return false;

        string[] parts = data.AsString().Split('|', 3);
        if (parts.Length == 3 && parts[0] == "ADD")
            return IsBlockType(parts[1]);
        if (parts.Length == 2 && parts[0] == "MOVE")
            return int.TryParse(parts[1], out _);
        return parts.Length == 2 && IsBlockType(parts[0]);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        Dropped?.Invoke(data);
    }

    private static bool IsBlockType(string type) =>
        type == "Start" || type == "Input" || type == "Process" ||
        type == "Output" || type == "Decision" || type == "End";

    public override void _Draw()
    {
        float width = Size.X;
        float height = Size.Y;
        if (width <= 1f || height <= 1f)
            return;

        if (Kind == "Split")
        {
            DrawBranch(true, width, height);
            DrawBranch(false, width, height);
            return;
        }

        if (Kind == "Merge")
        {
            float left = width * 0.25f;
            float right = width * 0.75f;
            float center = width * 0.5f;
            DrawLine(new Vector2(left, 0), new Vector2(left, height * 0.48f), ConnectorColor, 2f, true);
            DrawLine(new Vector2(right, 0), new Vector2(right, height * 0.48f), ConnectorColor, 2f, true);
            DrawLine(new Vector2(left, height * 0.48f), new Vector2(center, height - 8f), ConnectorColor, 2f, true);
            DrawLine(new Vector2(right, height * 0.48f), new Vector2(center, height - 8f), ConnectorColor, 2f, true);
            DrawArrowDown(center, height - 8f);
            return;
        }

        float x = width * 0.5f;
        DrawLine(new Vector2(x, 0), new Vector2(x, height - 8f), ConnectorColor, 2f, true);
        DrawArrowDown(x, height - 8f);
    }

    private void DrawBranch(bool isTrue, float width, float height)
    {
        float center = width * 0.5f;
        float branchX = width * (isTrue ? 0.25f : 0.75f);
        float junctionY = 13f;
        float arrowY = height - 8f;
        DrawLine(new Vector2(center, 0), new Vector2(center, junctionY), ConnectorColor, 2f, true);
        DrawLine(new Vector2(center, junctionY), new Vector2(branchX, junctionY), ConnectorColor, 2f, true);
        DrawLine(new Vector2(branchX, junctionY), new Vector2(branchX, arrowY), ConnectorColor, 2f, true);
        DrawArrowDown(branchX, arrowY);

        string label = isTrue ? "TRUE" : "FALSE";
        float labelX = isTrue ? branchX - 30f : branchX + 8f;
        DrawString(ThemeDB.FallbackFont, new Vector2(labelX, 12f), label,
            HorizontalAlignment.Left, -1, 10, BranchColor);
    }

    private void DrawArrowDown(float x, float y)
    {
        DrawLine(new Vector2(x, y), new Vector2(x - 5f, y - 7f), ConnectorColor, 2f, true);
        DrawLine(new Vector2(x, y), new Vector2(x + 5f, y - 7f), ConnectorColor, 2f, true);
    }
}
