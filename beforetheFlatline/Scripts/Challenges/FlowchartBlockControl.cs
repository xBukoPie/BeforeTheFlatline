using Godot;
using System;

/// <summary>
/// Editable Flowgorithm-inspired block. Start/End use terminator ovals, Input/Output
/// use parallelograms, Decision uses a diamond, and Process uses a rectangle.
/// </summary>
public partial class FlowchartBlockControl : Control
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string InitialText { get; set; } = "value = 0";

    private LineEdit _editor;
    private Label _typeLabel;

    public string BlockText => _editor?.Text ?? InitialText;

    public void Configure(string type, string text, int index, Action<int> moveUp, Action<int> moveDown, Action<int> remove)
    {
        BlockType = type;
        InitialText = text;
        Name = $"Block_{index}_{type}";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(530, 82);
        MouseFilter = MouseFilterEnum.Pass;

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);

        var shape = new Control
        {
            CustomMinimumSize = new Vector2(420, 78),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        shape.Draw += () => { };
        row.AddChild(shape);

        _typeLabel = new Label
        {
            Text = type.ToUpperInvariant(),
            Position = new Vector2(8, 3),
            Size = new Vector2(405, 16),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _typeLabel.AddThemeFontSizeOverride("font_size", 10);
        _typeLabel.AddThemeColorOverride("font_color", new Color("dbeafe"));
        shape.AddChild(_typeLabel);

        _editor = new LineEdit
        {
            Text = text,
            PlaceholderText = "Define this block...",
            Position = type == "Decision" ? new Vector2(100, 29) : new Vector2(52, 29),
            Size = type == "Decision" ? new Vector2(220, 29) : new Vector2(316, 29),
            Alignment = HorizontalAlignment.Center,
            MaxLength = 140
        };
        _editor.AddThemeColorOverride("font_color", Colors.White);
        _editor.AddThemeColorOverride("font_placeholder_color", new Color("cbd5e1"));
        shape.AddChild(_editor);

        foreach (var entry in new[] {
            (Text: "↑", Callback: moveUp),
            (Text: "↓", Callback: moveDown),
            (Text: "×", Callback: remove)
        })
        {
            var button = new Button { Text = entry.Text, CustomMinimumSize = new Vector2(30, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            button.Pressed += () => entry.Callback?.Invoke(index);
            row.AddChild(button);
        }

        shape.Draw += () => { };
        shape.SetProcess(false);
        shape.QueueRedraw();
        QueueRedraw();
    }

    public override void _Draw()
    {
        float width = Mathf.Max(Size.X, 420f);
        float height = Mathf.Max(Size.Y, 78f);
        Rect2 rect = new Rect2(2, 19, width - 4, height - 22);
        Color fill = new Color("1d4ed8");
        Color border = new Color("93c5fd");

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
            DrawColoredPolygon(points, fill);
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, border, 2f, true);
        }
        else if (BlockType == "Input" || BlockType == "Output")
        {
            float skew = 22f;
            Vector2[] points =
            {
                new(rect.Position.X + skew, rect.Position.Y),
                new(rect.End.X, rect.Position.Y),
                new(rect.End.X - skew, rect.End.Y),
                new(rect.Position.X, rect.End.Y)
            };
            DrawColoredPolygon(points, new Color("0f766e"));
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, new Color("99f6e4"), 2f, true);
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
                CornerRadiusTopLeft = 26,
                CornerRadiusTopRight = 26,
                CornerRadiusBottomLeft = 26,
                CornerRadiusBottomRight = 26
            };
            DrawStyleBox(style, rect);
        }
        else
        {
            DrawRect(rect, fill);
            DrawRect(rect, border, false, 2f);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree())
            QueueRedraw();
    }
}
