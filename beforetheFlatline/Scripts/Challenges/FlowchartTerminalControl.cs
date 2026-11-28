using Godot;

public partial class FlowchartTerminalControl : Control
{
    [Export] public string TerminalText { get; set; } = "START";

    public FlowchartTerminalControl()
    {
        float textWidth = ThemeDB.FallbackFont.GetStringSize(TerminalText,
            HorizontalAlignment.Left, -1f, 14).X;
        CustomMinimumSize = new Vector2(Mathf.Max(104f, textWidth + 48f), 72f);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        var label = new Label
        {
            Text = TerminalText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Colors.White);
        AddChild(label);
    }

    public override void _Draw()
    {
        Rect2 shape = new Rect2(2f, 2f, Mathf.Max(0f, Size.X - 4f), Mathf.Max(0f, Size.Y - 4f));
        var style = new StyleBoxFlat
        {
            BgColor = new Color("166534"),
            BorderColor = new Color("86efac"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 30,
            CornerRadiusTopRight = 30,
            CornerRadiusBottomLeft = 30,
            CornerRadiusBottomRight = 30
        };
        DrawStyleBox(style, shape);
    }
}
