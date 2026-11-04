using Godot;

public partial class OpeningPresents : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";

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

        var title = new Label
        {
            Text = "VOXTEK INC. PRESENTS",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("d8d4e7"));
        title.AddThemeColorOverride("font_outline_color", new Color("24202f"));
        title.AddThemeConstantOverride("outline_size", 1);
        AddChild(title);

        var tween = CreateTween();
        tween.TweenProperty(title, "modulate:a", 1f, 0.85f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenInterval(1.8f);
        tween.TweenProperty(title, "modulate:a", 0f, 0.8f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;

        Error result = GetTree().ChangeSceneToFile(MainMenuPath);
        if (result != Error.Ok)
            GD.PushError($"OpeningPresents: could not load Main Menu ({result}).");
    }
}
