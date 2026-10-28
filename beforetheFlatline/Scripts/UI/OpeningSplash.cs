using Godot;

public partial class OpeningSplash : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";
    private Control _content;

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildSplash();

        _content.Modulate = new Color(1f, 1f, 1f, 0f);
        var fadeIn = CreateTween();
        fadeIn.TweenProperty(_content, "modulate:a", 1f, 0.8f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(fadeIn, Tween.SignalName.Finished);
        await ToSignal(GetTree().CreateTimer(1.8f), SceneTreeTimer.SignalName.Timeout);

        var fadeOut = CreateTween();
        fadeOut.TweenProperty(_content, "modulate:a", 0f, 0.75f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(fadeOut, Tween.SignalName.Finished);

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(MainMenuPath);
        else
            GetTree().ChangeSceneToFile(MainMenuPath);
    }

    private void BuildSplash()
    {
        var background = new ColorRect
        {
            Color = new Color("080b14"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _content = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_content);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _content.AddChild(center);

        var stack = new VBoxContainer();
        stack.CustomMinimumSize = new Vector2(600, 0);
        stack.AddThemeConstantOverride("separation", 24);
        center.AddChild(stack);

        var presented = new Label
        {
            Text = "VOXTEK INC. PRESENTS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        presented.AddThemeFontSizeOverride("font_size", 18);
        presented.AddThemeColorOverride("font_color", new Color("a8a0c7"));
        stack.AddChild(presented);

        var logo = new PanelContainer { CustomMinimumSize = new Vector2(420, 210) };
        logo.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("111827"),
            BorderColor = new Color("655889"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18
        });
        stack.AddChild(logo);

        var logoText = new Label
        {
            Text = "VOXTEK\nLOGO PLACEHOLDER",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        logoText.AddThemeFontSizeOverride("font_size", 24);
        logoText.AddThemeColorOverride("font_color", new Color("d8c6ff"));
        logo.AddChild(logoText);

        var title = new Label
        {
            Text = "BEFORE THE FLATLINE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color("e2e8f0"));
        stack.AddChild(title);
    }
}
