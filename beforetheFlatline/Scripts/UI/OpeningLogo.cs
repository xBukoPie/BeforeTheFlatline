using Godot;

public partial class OpeningLogo : Control
{
    private const string LogoPath = "res://Assets/UI/Opening/VoxTekLogo.svg";
    private const string PresentsScenePath = "res://Scenes/Menu/OpeningPresents.tscn";

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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

        Modulate = new Color(1f, 1f, 1f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1f, 1.15f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenInterval(2.0f);
        tween.TweenProperty(this, "modulate:a", 0f, 0.95f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;

        Error result = GetTree().ChangeSceneToFile(PresentsScenePath);
        if (result != Error.Ok)
            GD.PushError($"OpeningLogo: could not load presents scene ({result}).");
    }
}
