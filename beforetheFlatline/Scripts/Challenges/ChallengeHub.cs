using Godot;
using SchoolYardGame;

public partial class ChallengeHub : Control
{
    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = true;

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("101522"), MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(700, 450) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("1a2335"),
            BorderColor = new Color("3a4a65"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 34, ContentMarginRight = 34, ContentMarginTop = 30, ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 18);
        panel.AddChild(stack);

        var title = new Label { Text = "ALGORITHM LAB", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = "A four-question assessment. Write pseudocode or build a flowchart for each prompt. You may submit an answer or give up and continue. Your score and any sanity effects appear at the end.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 92)
        };
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(subtitle);

        var start = new Button
        {
            Text = "Start Daily Assessment",
            CustomMinimumSize = new Vector2(0, 54)
        };
        start.Pressed += () =>
        {
            DailyTestSession.Reset();
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
        };
        stack.AddChild(start);

        var back = new Button { Text = "Return to Main Menu", CustomMinimumSize = new Vector2(0, 42) };
        back.Pressed += () =>
        {
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        };
        stack.AddChild(back);
    }
}
