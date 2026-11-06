using Godot;

public partial class OpeningPresents : Control
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var title = new Label
        {
            Text = "VoxTek INC Presents",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("d8d4e7"));
        title.AddThemeColorOverride("font_outline_color", new Color("24202f"));
        title.AddThemeConstantOverride("outline_size", 1);
        AddChild(title);
    }
}
