using Godot;

public partial class FlowchartTerminalControl : Control
{
    [Export] public string TerminalText { get; set; } = "START";

    public FlowchartTerminalControl()
    {
        CustomMinimumSize = new Vector2(520, 72);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
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
        DrawString(ThemeDB.FallbackFont,
            new Vector2(Size.X * 0.5f - 22f, Size.Y * 0.5f + 5f),
            TerminalText,
            HorizontalAlignment.Left, -1, 14, Colors.White);
    }
}
