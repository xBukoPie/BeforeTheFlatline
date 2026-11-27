using Godot;

public partial class FlowchartDragHandle : Label
{
    [Export] public int BlockIndex { get; set; }

    public void Configure(int index)
    {
        BlockIndex = index;
        Text = "⠿";
        TooltipText = "Drag to reorder this block";
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        CustomMinimumSize = new Vector2(28, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Move;
        AddThemeFontSizeOverride("font_size", 24);
        AddThemeColorOverride("font_color", new Color("94a3b8"));
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label
        {
            Text = "Moving flowchart block",
            CustomMinimumSize = new Vector2(180, 34)
        };
        preview.AddThemeColorOverride("font_color", Colors.White);
        preview.AddThemeColorOverride("font_outline_color", Colors.Black);
        preview.AddThemeConstantOverride("outline_size", 4);
        SetDragPreview(preview);
        return $"MOVE|{BlockIndex}";
    }
}
