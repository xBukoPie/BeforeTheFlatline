using Godot;

public partial class OpeningLogo : Control
{
    private const string LogoPath = "res://Assets/UI/Opening/VoxTekLogo.png";

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

        Texture2D logoTexture = GD.Load<Texture2D>(LogoPath);
        if (logoTexture == null)
            GD.PushWarning($"OpeningLogo: logo artwork could not be loaded: {LogoPath}");

        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var logo = new TextureRect
        {
            Texture = logoTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(760, 260),
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.AddChild(logo);
    }
}
