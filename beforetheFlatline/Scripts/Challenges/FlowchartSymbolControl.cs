using Godot;
using System;

public partial class FlowchartSymbolControl : Control
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string InitialText { get; set; } = "";
    private LineEdit _editor;

    public event Action TextChanged;
    public string BlockText => _editor?.Text ?? InitialText;

    public void Configure(string type, string text, bool compact = false)
    {
        BlockType = type ?? "Process";
        InitialText = text ?? "";
        CustomMinimumSize = new Vector2(compact ? 210 : 420, compact ? 88 : 90);
        SizeFlagsHorizontal = compact ? SizeFlags.ExpandFill : SizeFlags.ShrinkCenter;
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
            ClearButtonEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _editor.AddThemeColorOverride("font_color", Colors.White);
        _editor.AddThemeColorOverride("font_placeholder_color", new Color("cbd5e1"));
        _editor.TextChanged += _ => TextChanged?.Invoke();
        AddChild(_editor);
        LayoutEditor();
    }

    public override void _Draw()
    {
        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        float top = 22f;
        float height = BlockType == "Decision" ? 62f : 54f;
        float inset = BlockType == "Decision" ? width * 0.19f : 3f;
        Rect2 rect = new Rect2(inset, top, width - inset * 2f, height);

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
            float skew = Mathf.Min(26f, width * 0.08f);
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
            DrawRect(rect, new Color("1d4ed8"));
            DrawRect(rect, new Color("93c5fd"), false, 2f);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(12, 16), BlockType.ToUpperInvariant(),
            HorizontalAlignment.Left, -1, 10, new Color("dbeafe"));
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

        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        if (BlockType == "Decision")
        {
            _editor.Position = new Vector2(width * 0.28f, 42f);
            _editor.Size = new Vector2(width * 0.44f, 28f);
        }
        else
        {
            _editor.Position = new Vector2(width * 0.13f, 37f);
            _editor.Size = new Vector2(width * 0.74f, 28f);
        }
    }
}
