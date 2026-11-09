using Godot;
using SchoolYardGame;

public partial class GoodEndingBriefing : Control
{
    private const string CreditsPath = "res://Scenes/Ending/EndingCredits.tscn";
    private bool _creditsStarted;
    private Button _creditsButton;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        AudioManager.Instance?.PlayGoodEnding1();
        AudioManager.Instance?.FadeOutGoodEndingHeartbeat(0.7);
        BuildUi();
        RevealCreditsButton();
    }

    public override void _ExitTree()
    {
        AudioManager.Instance?.FadeOutGoodEnding1(0.75);
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new TextureRect
        {
            Name = "HospitalBackground",
            Texture = GD.Load<Texture2D>("res://Assets/Cutscenes/cutsceneGoodEnding2.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var shade = new ColorRect
        {
            Color = new Color(0.015f, 0.025f, 0.05f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            Name = "GoodEndingBriefingPanel",
            CustomMinimumSize = new Vector2(940, 440),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.055f, 0.09f, 0.97f),
            BorderColor = new Color("c6a77a"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 42,
            ContentMarginRight = 42,
            ContentMarginTop = 34,
            ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 18);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE  •  GOOD ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 14);
        eyebrow.AddThemeColorOverride("font_color", new Color("d7b98b"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "YOU FOUND YOUR WAY BACK",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color("f1e4d2"));
        stack.AddChild(title);

        var summary = new Label
        {
            Text = "The school days and familiar rooms were part of a world your mind built while you were in a coma. Each photograph returned a piece of the life you had been struggling through. When you finally reached your parents, you woke in the hospital. They were there, waiting for you.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        summary.AddThemeFontSizeOverride("font_size", 21);
        summary.AddThemeColorOverride("font_color", new Color("e8edf5"));
        summary.AddThemeConstantOverride("line_spacing", 7);
        stack.AddChild(summary);

        var closing = new Label
        {
            Text = "You are awake. You do not have to face everything alone.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        closing.AddThemeFontSizeOverride("font_size", 19);
        closing.AddThemeColorOverride("font_color", new Color("a9d8bc"));
        stack.AddChild(closing);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        stack.AddChild(buttons);

        _creditsButton = CreateButton("Continue to Credits");
        _creditsButton.Disabled = true;
        _creditsButton.Visible = false;
        _creditsButton.Modulate = new Color(1f, 1f, 1f, 0f);
        _creditsButton.Pressed += OpenCredits;
        buttons.AddChild(_creditsButton);
    }

    private async void RevealCreditsButton()
    {
        SceneTreeTimer timer = GetTree().CreateTimer(2.6, true);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

        if (!IsInsideTree() || _creditsStarted ||
            _creditsButton == null || !GodotObject.IsInstanceValid(_creditsButton))
            return;

        _creditsButton.Visible = true;
        Tween reveal = CreateTween();
        reveal.TweenProperty(_creditsButton, "modulate:a", 1f, 0.5f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(reveal, Tween.SignalName.Finished);

        if (IsInsideTree() && !_creditsStarted &&
            _creditsButton != null && GodotObject.IsInstanceValid(_creditsButton))
            _creditsButton.Disabled = false;
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(270, 50),
            FocusMode = Control.FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("65528d"),
            BorderColor = new Color("b9a6e8"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7
        });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color("7660a5"),
            BorderColor = new Color("d8c6ff"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7
        });
        return button;
    }

    private void OpenCredits()
    {
        if (_creditsStarted)
            return;

        _creditsStarted = true;
        if (_creditsButton != null && GodotObject.IsInstanceValid(_creditsButton))
            _creditsButton.Disabled = true;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(CreditsPath, 1.25f);
        else
            GetTree().ChangeSceneToFile(CreditsPath);
    }
}
