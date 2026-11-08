using Godot;
using SchoolYardGame;

public partial class BadEnding : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";
    private const string CreditsPath = "res://Scenes/Ending/EndingCredits.tscn";
    private bool _leavingEnding;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();

        double flatlineLength = AudioManager.Instance?.PlayGameOver() ?? 0.0;
        AudioManager.Instance?.PlaySadEndingAfter(Math.Max(0.1, flatlineLength));
    }

    private void OpenCredits()
    {
        if (_leavingEnding)
            return;

        _leavingEnding = true;
        AudioManager.Instance?.FadeOutGameOver(0.55);
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(CreditsPath, 1.1f);
        else
            GetTree().ChangeSceneToFile(CreditsPath);
    }

    private void ReturnToTitle()
    {
        if (_leavingEnding)
            return;

        _leavingEnding = true;
        AudioManager.Instance?.FadeOutGameOver(0.55);
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(MainMenuPath, 1.1f);
        else
            GetTree().ChangeSceneToFile(MainMenuPath);
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new TextureRect
        {
            Name = "FlatlineCutscene",
            Texture = GD.Load<Texture2D>("res://Assets/Cutscenes/cutsceneBadEnding.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        Texture2D vignetteTexture = GD.Load<Texture2D>("res://Assets/Effects/flatlineVignette.png");
        if (vignetteTexture != null)
        {
            var vignette = new TextureRect
            {
                Name = "FlatlineVignette",
                Texture = vignetteTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = TextureFilterEnum.Linear,
                Modulate = new Color(0f, 0f, 0f, 0.68f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            vignette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(vignette);
        }
        else
        {
            GD.PushWarning("BadEnding: flatline vignette texture could not be loaded.");
        }

        var shade = new ColorRect
        {
            Color = new Color(0.015f, 0.008f, 0.018f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            Name = "BadEndingBriefingPanel",
            CustomMinimumSize = new Vector2(960, 470),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.045f, 0.025f, 0.04f, 0.97f),
            BorderColor = new Color("a65e69"),
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
        stack.AddThemeConstantOverride("separation", 19);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE  •  BAD ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 14);
        eyebrow.AddThemeColorOverride("font_color", new Color("d99aa3"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "THE SIGNAL WENT QUIET",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color("f3dfe3"));
        stack.AddChild(title);

        var summary = new Label
        {
            Text = "Your sanity reached zero before you could wake. The memories had brought you close to asking for help, but the moment never came. The monitor flatlined, and you did not reach the reunion with your parents in this ending.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        summary.AddThemeFontSizeOverride("font_size", 21);
        summary.AddThemeColorOverride("font_color", new Color("eee7ea"));
        summary.AddThemeConstantOverride("line_spacing", 7);
        stack.AddChild(summary);

        var closing = new Label
        {
            Text = "The memories returned. The way back did not.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        closing.AddThemeFontSizeOverride("font_size", 19);
        closing.AddThemeColorOverride("font_color", new Color("d99aa3"));
        stack.AddChild(closing);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        buttons.AddThemeConstantOverride("separation", 14);
        stack.AddChild(buttons);

        var credits = CreateButton("View Credits", true);
        credits.Pressed += OpenCredits;
        buttons.AddChild(credits);

        var titleButton = CreateButton("Return to Title", false);
        titleButton.Pressed += ReturnToTitle;
        buttons.AddChild(titleButton);
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(240, 50),
            FocusMode = Control.FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(
            primary ? new Color("76505f") : new Color("34303b")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(
            primary ? new Color("936273") : new Color("49424f")));
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color color) => new()
    {
        BgColor = color,
        BorderColor = new Color("a65e69"),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 7,
        CornerRadiusTopRight = 7,
        CornerRadiusBottomLeft = 7,
        CornerRadiusBottomRight = 7
    };
}
