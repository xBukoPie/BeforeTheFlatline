using Godot;

/// <summary>Draggable palette item used by the flowchart challenge.</summary>
public partial class ChallengeDragBlockButton : Button
{
    [Export] public string BlockName { get; set; } = "";

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = BlockName };
        preview.AddThemeColorOverride("font_color", Colors.White);
        preview.AddThemeColorOverride("font_outline_color", Colors.Black);
        preview.AddThemeConstantOverride("outline_size", 4);
        SetDragPreview(preview);
        return BlockName;
    }
}
