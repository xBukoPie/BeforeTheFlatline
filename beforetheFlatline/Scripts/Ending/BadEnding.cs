using Godot;
using SchoolYardGame;

public partial class BadEnding : Control
{
    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = new Color("090811"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var wash = new ColorRect
        {
            Color = new Color(0.36f, 0.035f, 0.11f, 0.28f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(wash);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(700, 390) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("17121e"),
            BorderColor = new Color("8d314d"),
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
            ContentMarginBottom = 34
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 20);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 14);
        eyebrow.AddThemeColorOverride("font_color", new Color("c084fc"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "YOU LOST YOURSELF",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color("fb7185"));
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 4);
        stack.AddChild(title);

        var message = new Label
        {
            Text = "Your sanity has reached zero. The pressure, confusion, and exhaustion have become too much.\n\nThere is nothing left to do today.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        message.AddThemeFontSizeOverride("font_size", 17);
        message.AddThemeColorOverride("font_color", new Color("e2dbe7"));
        stack.AddChild(message);

        var returnButton = new Button
        {
            Text = "Return to Title",
            CustomMinimumSize = new Vector2(0, 50),
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
