using Godot;
using SchoolYardGame;

public partial class EndingCredits : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";

    [Export]
    public string[] TeamMemberCredits { get; set; } = new[]
    {
        "Aniciete, Edward — Project Manager / Team Lead",
        "Palaña, Chelzie Nicole — Game Design Handler",
        "Guevarra, Dominique — Game Assets Handler"
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = new Color("080b14"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            Name = "CreditsPanel",
            CustomMinimumSize = new Vector2(900, 600)
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("121a29"),
            BorderColor = new Color("8570a8"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 44,
            ContentMarginRight = 44,
            ContentMarginTop = 36,
            ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 15);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 13);
        eyebrow.AddThemeColorOverride("font_color", new Color("b8a4dd"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "CREDITS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 38);
        title.AddThemeColorOverride("font_color", new Color("eee8f7"));
        stack.AddChild(title);

        var group = new Label
        {
            Text = "VoxTek INC",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        group.AddThemeFontSizeOverride("font_size", 24);
        group.AddThemeColorOverride("font_color", new Color("e1c99f"));
        stack.AddChild(group);

        var teamHeading = new Label
        {
            Text = "GROUP MEMBERS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        teamHeading.AddThemeFontSizeOverride("font_size", 15);
        teamHeading.AddThemeColorOverride("font_color", new Color("b8a4dd"));
        stack.AddChild(teamHeading);

        var team = new VBoxContainer();
        team.AddThemeConstantOverride("separation", 6);
        team.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        stack.AddChild(team);

        foreach (string credit in TeamMemberCredits)
        {
            if (string.IsNullOrWhiteSpace(credit))
                continue;

            var member = new Label
            {
                Text = credit.Trim(),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            member.AddThemeFontSizeOverride("font_size", 17);
            member.AddThemeColorOverride("font_color", new Color("e2e8f0"));
            team.AddChild(member);
        }

        var divider = new HSeparator();
        divider.AddThemeColorOverride("separator_color", new Color("514766"));
        stack.AddChild(divider);

        var purposeHeading = new Label
        {
            Text = "WHY THIS GAME WAS MADE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        purposeHeading.AddThemeFontSizeOverride("font_size", 16);
        purposeHeading.AddThemeColorOverride("font_color", new Color("e1c99f"));
        stack.AddChild(purposeHeading);

        var purpose = new Label
        {
            Text = "BeforeTheFlatline was created to fulfill a school project requirement. This submission is a starting point, not the end of development. The game will continue to be developed, refined, and improved beyond the project requirement.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        purpose.AddThemeFontSizeOverride("font_size", 16);
        purpose.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(purpose);

        var thanks = new Label
        {
            Text = "THANK YOU FOR PLAYING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        thanks.AddThemeFontSizeOverride("font_size", 22);
        thanks.AddThemeColorOverride("font_color", new Color("a9d8bc"));
        stack.AddChild(thanks);

        var footer = new Label
        {
            Text = "A VOXTEK INC PRODUCTION",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", new Color("8994a8"));
        stack.AddChild(footer);

        var returnButton = new Button
        {
            Text = "Return to Main Menu",
            CustomMinimumSize = new Vector2(250, 48),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        returnButton.Pressed += ReturnToMainMenu;
        stack.AddChild(returnButton);
    }

    private void ReturnToMainMenu()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(MainMenuPath, 1.1f);
        else
            GetTree().ChangeSceneToFile(MainMenuPath);
    }
}
