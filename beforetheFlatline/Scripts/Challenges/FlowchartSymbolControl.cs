using Godot;

/// <summary>Renders a Flowgorithm-inspired symbol and its editable block definition.</summary>
public partial class FlowchartSymbolControl : Control
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string InitialText { get; set; } = "";
    private LineEdit _editor;

    public string BlockText => _editor?.Text ?? InitialText;

    public void Configure(string type, string text)
    {
        BlockType = type;
        InitialText = text;
        CustomMinimumSize = new Vector2(420, 78);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Pass;
        QueueRedraw();
    }

    public override void _Ready()
    {
        _editor = new LineEdit
        {
            Text = InitialText,
            PlaceholderText = "Define this block...",
            MaxLength = 140,
            Alignment = HorizontalAlignment.Center,
            ClearButtonEnabled = false
        };
        _editor.AddThemeColorOverride("font_color", Colors.White);
        _editor.AddThemeColorOverride("font_placeholder_color", new Color("cbd5e1"));
        AddChild(_editor);
        LayoutEditor();
    }

    public override void _Draw()
    {
        float width = Mathf.Max(Size.X, 420f);
        const float top = 19f;
        const float height = 52f;
        Rect2 rect = new Rect2(3, top, width - 6, height);
        var fill = new Color("1d4ed8");
        var edge = new Color("93c5fd");

        if (BlockType == "Decision")
        {
            Vector2 center = rect.GetCenter();
            Vector2[] points =
            {
                new(center.X, rect.Position.Y),
                new(rect.End.X, center.Y),
                new(center.X, rect.End.Y),
                new(rect.Position.X, center.Y)
            };
            DrawColoredPolygon(points, new Color("92400e"));
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, new Color("fcd34d"), 2f);
        }
        else if (BlockType == "Input" || BlockType == "Output")
        {
            const float skew = 24f;
            Vector2[] points =
            {
                new(rect.Position.X + skew, rect.Position.Y),
                new(rect.End.X, rect.Position.Y),
                new(rect.End.X - skew, rect.End.Y),
                new(rect.Position.X, rect.End.Y)
            };
            DrawColoredPolygon(points, new Color("0f766e"));
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, new Color("99f6e4"), 2f);
        }
        else if (BlockType == "Start" || BlockType == "End")
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color("166534"),
                BorderColor = new Color("86efac"),
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 25,
                CornerRadiusTopRight = 25,
                CornerRadiusBottomLeft = 25,
                CornerRadiusBottomRight = 25
            };
            DrawStyleBox(style, rect);
        }
        else
        {
            DrawRect(rect, fill);
            DrawRect(rect, edge, false, 2f);
        }

        var title = BlockType.ToUpperInvariant();
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 15), title, HorizontalAlignment.Left, -1, 10, new Color("dbeafe"));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree())
            LayoutEditor();
    }

    private void LayoutEditor()
    {
        if (_editor == null)
            return;

        float width = Mathf.Max(Size.X, 420f);
        _editor.Position = BlockType == "Decision" ? new Vector2(width * 0.23f, 32) : new Vector2(width * 0.13f, 32);
        _editor.Size = BlockType == "Decision" ? new Vector2(width * 0.54f, 27) : new Vector2(width * 0.74f, 27);
    }
}
