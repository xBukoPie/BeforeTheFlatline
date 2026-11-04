using Godot;
using SchoolYardGame;

public partial class BadEnding : Control
{
    private bool _returningToTitle;

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();

        // Start the monitor track with the cutscene and return before its final tail.
        double trackLength = AudioManager.Instance?.PlayGameOver() ?? 8.0;
        double returnDelay = System.Math.Max(3.0, trackLength - 1.6);
        SceneTreeTimer timer = GetTree().CreateTimer(returnDelay, true);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

        if (IsInsideTree())
            ReturnToTitle();
    }

    private void ReturnToTitle()
    {
        if (_returningToTitle)
            return;

        _returningToTitle = true;
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        else
            GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
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
            MouseFilter = MouseFilterEnum.Ignore
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
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                Modulate = new Color(0f, 0f, 0f, 0.78f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            vignette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(vignette);
        }
        else
        {
            GD.PushWarning("BadEnding: flatline vignette texture could not be loaded.");
        }

        var darken = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        darken.AnchorLeft = 0f;
        darken.AnchorRight = 1f;
        darken.AnchorTop = 0.72f;
        darken.AnchorBottom = 1f;
        AddChild(darken);

        var center = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.AnchorLeft = 0f;
        center.AnchorRight = 1f;
        center.AnchorTop = 0.72f;
        center.AnchorBottom = 1f;
        AddChild(center);

        var stack = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(600, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        stack.AddThemeConstantOverride("separation", 14);
        center.AddChild(stack);

        var title = new Label
        {
            Text = "GAME OVER",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color("fca5a5"));
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 4);
        stack.AddChild(title);

        var message = new Label
        {
            Text = "The monitor's steady rhythm has become a flatline.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        message.AddThemeFontSizeOverride("font_size", 17);
        message.AddThemeColorOverride("font_color", Colors.White);
        stack.AddChild(message);

        var returnButton = new Button
        {
            Text = "Return to Title",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        returnButton.Pressed += ReturnToTitle;
        stack.AddChild(returnButton);
    }
}
