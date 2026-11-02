using Godot;
using SchoolYardGame;

public partial class BadEnding : Control
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();

        // The flatline track begins with the hospital cutscene, not during the transition fade.
        AudioManager.Instance?.PlayGameOver();
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
        returnButton.Pressed += () =>
        {
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        };
        stack.AddChild(returnButton);
    }
}
