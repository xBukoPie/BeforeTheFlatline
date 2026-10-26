using Godot;

/// <summary>Entry point for the optional programming challenge path.</summary>
public partial class ChallengeHub : Control
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("111827"), MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(700, 480) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("1f2937"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12, ContentMarginLeft = 34, ContentMarginRight = 34, ContentMarginTop = 30, ContentMarginBottom = 30 });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 16);
        panel.AddChild(stack);
        var title = new Label { Text = "ALGORITHM LAB", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        stack.AddChild(title);
        var subtitle = new Label { Text = "Solve the exercises to progress from basic sequences to a complete algorithm.", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(subtitle);

        AddChallengeButton(stack, "Start challenge path", "res://Scenes/Challenges/SequenceChallenge.tscn");
        AddChallengeButton(stack, "Practice: IF / ELSE", "res://Scenes/Challenges/ConditionChallenge.tscn");
        AddChallengeButton(stack, "Practice: Flowchart blocks", "res://Scenes/Challenges/FlowchartChallenge.tscn");
        var back = new Button { Text = "Return to Main Menu", CustomMinimumSize = new Vector2(0, 42) };
        back.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        stack.AddChild(back);
    }

    private static void AddChallengeButton(VBoxContainer parent, string text, string path)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 46) };
        button.Pressed += () => button.GetTree().ChangeSceneToFile(path);
        parent.AddChild(button);
    }
}
