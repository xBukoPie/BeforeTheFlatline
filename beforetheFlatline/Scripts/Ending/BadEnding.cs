using Godot;
using SchoolYardGame;

public partial class BadEnding : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";
    private bool _returningToTitle;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();
        AudioManager.Instance?.PlayGameOver();
    }

    private void ReturnToTitle()
    {
        if (_returningToTitle)
            return;

        _returningToTitle = true;
        AudioManager.Instance?.FadeOutGameOver(0.7);
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(MainMenuPath);
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
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
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
            Color = new Color(0.015f, 0.008f, 0.018f, 0.72f),
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
            CustomMinimumSize = new Vector2(1190, 670),
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
            ContentMarginLeft = 38,
            ContentMarginRight = 38,
            ContentMarginTop = 28,
            ContentMarginBottom = 26
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 13);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE  •  BAD ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 15);
        eyebrow.AddThemeColorOverride("font_color", new Color("d99aa3"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "THE SIGNAL WENT QUIET",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", new Color("f3dfe3"));
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = "THE STORY BEHIND THIS ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 19);
        subtitle.AddThemeColorOverride("font_color", new Color("d99aa3"));
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
        story.AddThemeConstantOverride("separation", 17);
        scroll.AddChild(story);

        AddStorySection(story, "THE ROUTINE THAT FELT REAL",
            "You began in a familiar home and followed what seemed to be an ordinary routine: getting ready, going to school, meeting classmates, completing assessments, and returning home. Keys gradually opened more rooms, and each new day gave the routine another shape. It felt like a life continuing normally, even though pieces of the world around you never quite fit together.");

        AddStorySection(story, "THE MEMORIES THAT RETURNED",
            "Photographs led you back through fragments of your life. The kitchen recalled how difficult eating had become. The laundry brought back the exhaustion of washing your uniform and facing another school day. The bathroom exposed the pressure of comparing yourself with others and feeling left behind. In the master bedroom, you finally tried to reach your parents and ask for help. These memories pointed toward a reality beyond the daily school routine.");

        AddStorySection(story, "THE MYSTERIOUS GUIDE",
            "A mysterious person appeared along the way and encouraged you to keep moving toward the memories. Their identity remained hidden, and the story never confirmed exactly who they were. Their guidance offered a path toward remembering, but it could not remove the pressure or make the difficult moments disappear for you.");

        AddStorySection(story, "WHEN THE PRESSURE BECAME TOO MUCH",
            "The strain carried through the memories, daily routine, and assessments continued to affect your sanity. Setbacks and unresolved feelings accumulated. Reaching the master bedroom brought you close to asking for help, but the moment overwhelmed you before you could say what you needed to say. The final memory connected the false world to the reality that you had been in a coma.");

        AddStorySection(story, "THE LAST MOMENT",
            "Your sanity reached zero before the story could reach the hospital-room reunion. The monitor's steady rhythm became a flatline, and the screen went quiet. Unlike the good ending, you did not wake up in this version of the story, and the embrace with your parents never took place.");

        AddStorySection(story, "WHAT THIS ENDING LEAVES UNANSWERED",
            "This ending shows that the player did not regain consciousness during the events of the story. It does not confirm what happens beyond the flatline or establish a final medical outcome. The memories were uncovered, but the chance to reconnect with the people waiting outside that world was not reached.");

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        stack.AddChild(buttons);

        var returnButton = new Button
        {
            Text = "Return to Title",
            CustomMinimumSize = new Vector2(250, 52),
            FocusMode = Control.FocusModeEnum.All
        };
        returnButton.AddThemeFontSizeOverride("font_size", 19);
        returnButton.Pressed += ReturnToTitle;
        buttons.AddChild(returnButton);
    }

    private static void AddStorySection(VBoxContainer parent, string heading, string body)
    {
        var title = new Label
        {
            Text = heading,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("e5b6be"));
        parent.AddChild(title);

        var paragraph = new Label
        {
            Text = body,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        paragraph.AddThemeFontSizeOverride("font_size", 20);
        paragraph.AddThemeConstantOverride("line_spacing", 5);
        paragraph.AddThemeColorOverride("font_color", new Color("eee7ea"));
        parent.AddChild(paragraph);
    }
}
