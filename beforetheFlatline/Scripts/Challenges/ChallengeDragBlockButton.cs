using Godot;

public partial class ChallengeDragBlockButton : Button
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string BlockName { get; set; } = "value = 0";

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = $"{BlockType}: {BlockName}" };
        preview.AddThemeColorOverride("font_color", Colors.White);
        preview.AddThemeColorOverride("font_outline_color", Colors.Black);
        preview.AddThemeConstantOverride("outline_size", 4);
        SetDragPreview(preview);
        return $"{BlockType}|{BlockName}";
    }
}
