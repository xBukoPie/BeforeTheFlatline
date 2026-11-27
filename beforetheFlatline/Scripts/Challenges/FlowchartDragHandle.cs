using Godot;
using System;

public partial class FlowchartDragHandle : Label
{
    [Export] public string DefinitionId { get; set; } = "";

    public void Configure(Guid definitionId)
    {
        DefinitionId = definitionId.ToString("D");
        Text = "⠿";
        TooltipText = "Drag this block and drop it on an arrow to move it";
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        CustomMinimumSize = new Vector2(30, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Move;
        AddThemeFontSizeOverride("font_size", 24);
        AddThemeColorOverride("font_color", new Color("94a3b8"));
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!Guid.TryParse(DefinitionId, out _))
            return default;

        var preview = new Label
        {
            Text = "Moving flowchart block",
            CustomMinimumSize = new Vector2(180, 34)
        };
        preview.AddThemeColorOverride("font_color", Colors.White);
        preview.AddThemeColorOverride("font_outline_color", Colors.Black);
        preview.AddThemeConstantOverride("outline_size", 4);
        SetDragPreview(preview);
        return $"MOVE|{DefinitionId}";
    }
}
