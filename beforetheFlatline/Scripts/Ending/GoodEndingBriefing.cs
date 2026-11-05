using Godot;
using SchoolYardGame;

public partial class GoodEndingBriefing : Control
{
    private const string CreditsPath = "res://Scenes/Ending/EndingCredits.tscn";

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

        var background = new TextureRect
        {
            Name = "HospitalBackground",
            Texture = GD.Load<Texture2D>("res://Assets/Cutscenes/cutsceneGoodEnding.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var shade = new ColorRect
        {
            Color = new Color(0.025f, 0.035f, 0.065f, 0.72f),
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
            CustomMinimumSize = new Vector2(1080, 640),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.075f, 0.11f, 0.96f),
            BorderColor = new Color("c6a77a"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 36,
            ContentMarginRight = 36,
            ContentMarginTop = 28,
            ContentMarginBottom = 26
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE  •  GOOD ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 13);
        eyebrow.AddThemeColorOverride("font_color", new Color("d7b98b"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "YOU FOUND YOUR WAY BACK",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color("f1e4d2"));
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = "GOOD ENDING ACHIEVED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", new Color("a9d8bc"));
        stack.AddChild(subtitle);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        stack.AddChild(scroll);

        var story = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        story.AddThemeConstantOverride("separation", 11);
        scroll.AddChild(story);

        AddStorySection(story, "THE LIFE YOU THOUGHT YOU WERE LIVING",
            "You woke in a familiar home and followed an ordinary routine: getting ready, going to school, meeting classmates, completing assessments, and unlocking rooms with the keys you earned. The routine felt real, but small moments kept breaking through it. The places around you seemed familiar for reasons you could not explain.");
        AddStorySection(story, "FOUR MEMORIES",
            "The kitchen brought back the struggle to eat. Food had begun to taste bland, and even its smell could turn your stomach. The laundry memory revealed how exhausted you felt from washing your uniform and how much you dreaded returning to school. In the bathroom, pressure and comparison became overwhelming. In the master bedroom, you finally tried to ask your parents for help. The weight of everything became too much, and you collapsed.");
        AddStorySection(story, "THE PERSON WHO KEPT GUIDING YOU",
            "A mysterious person appeared along the way. They encouraged you to keep remembering, spoke of a false reality, and reminded you that people cared about you. Their identity was never revealed. What mattered was the direction they pointed you toward: the truth you had been avoiding.");
        AddStorySection(story, "WHAT THE MEMORIES MEANT",
            "The photographs were fragments of repressed memories from your life before you fell into a coma. The school routine and familiar places had become a false reality that kept you distant from what had happened. Each recovered memory brought you closer to the life and people waiting beyond it. The story leaves the exact nature of the mysterious guide unexplained.");
        AddStorySection(story, "BACK TO THE PEOPLE WHO CARE",
            "The monitor's beeping returned. You opened your eyes in a hospital room and saw your parents beside you, overwhelmed with relief. They had been waiting for you. You reached them, embraced them, and finally let the tears come. You were no longer facing everything alone.");
        AddStorySection(story, "THE END IS ALSO A BEGINNING",
            "The assessments, keys, rooms, classmates, and memories were all part of the path that brought you here. You faced what had been buried and found your way back to the people who love you. Your story does not end with everything suddenly becoming easy; it continues with the chance to ask for help and move forward.");

        var buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.End;
        stack.AddChild(buttons);

        var credits = CreateButton("Continue to Credits");
        credits.Pressed += OpenCredits;
        buttons.AddChild(credits);
    }

    private static void AddStorySection(VBoxContainer parent, string heading, string body)
    {
        var title = new Label { Text = heading };
        title.AddThemeFontSizeOverride("font_size", 17);
        title.AddThemeColorOverride("font_color", new Color("e4c99f"));
        parent.AddChild(title);

        var paragraph = new Label
        {
            Text = body,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        paragraph.AddThemeFontSizeOverride("font_size", 15);
        paragraph.AddThemeColorOverride("font_color", new Color("e3e7ed"));
        parent.AddChild(paragraph);
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(230, 48),
            FocusMode = Control.FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 17);
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
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(CreditsPath, 1.25f);
        else
            GetTree().ChangeSceneToFile(CreditsPath);
    }
}
