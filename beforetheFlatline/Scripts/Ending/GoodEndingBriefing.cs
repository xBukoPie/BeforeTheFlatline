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
            Color = new Color(0.015f, 0.025f, 0.05f, 0.82f),
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
            CustomMinimumSize = new Vector2(1190, 670),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.055f, 0.09f, 0.98f),
            BorderColor = new Color("c6a77a"),
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
            Text = "BEFORE THE FLATLINE  •  GOOD ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 15);
        eyebrow.AddThemeColorOverride("font_color", new Color("d7b98b"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "YOU FOUND YOUR WAY BACK",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 38);
        title.AddThemeColorOverride("font_color", new Color("f1e4d2"));
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = "THE STORY BEHIND THE ENDING",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 19);
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
        story.AddThemeConstantOverride("separation", 17);
        scroll.AddChild(story);

        AddStorySection(story, "THE ROUTINE THAT FELT REAL",
            "At the beginning, you wake in a familiar home and move through what seems to be an ordinary day. You get ready, make your way to school, meet classmates, complete assessments, and return home. Keys open rooms that were previously out of reach. The clock keeps moving, and each day appears to lead naturally into the next. Nothing about the routine immediately explains why you are there or why some moments feel strangely distant. It becomes the world you know, even as small details begin to feel wrong.");

        AddStorySection(story, "THE KITCHEN — WHEN EVEN EATING BECAME DIFFICULT",
            "The first photograph brings back a memory connected to food. Meals no longer feel comforting: food tastes bland, and its smell can turn your stomach. What should be a familiar, everyday activity has become difficult. The memory is not simply about a room or a meal. It is a fragment of how you were feeling before the events of the story, returning in pieces rather than as one clear explanation.");

        AddStorySection(story, "THE LAUNDRY — EXHAUSTION THAT BUILT UP",
            "The laundry memory reveals how tiring the repeated routine had become. Washing your uniform day after day leaves your hands feeling numb, and you admit how exhausted you are. You also feel reluctant to return to school. The memory adds another part of the pressure you had been carrying: even ordinary responsibilities had begun to feel heavy, and the same routine kept demanding that you continue.");

        AddStorySection(story, "THE BATHROOM — PRESSURE AND COMPARISON",
            "The bathroom memory brings the pressure closer to the surface. You are struggling with comparison and the feeling that expectations are closing in. The memory shows how deeply those thoughts affect you. It is another fragment of the life you had been living before the coma, and it helps explain why the familiar school routine in the false world never felt entirely peaceful.");

        AddStorySection(story, "THE MASTER BEDROOM — THE MOMENT YOU TRIED TO ASK FOR HELP",
            "The final memory takes place in the master bedroom. You try to reach out to your parents and tell them that something is wrong. Before you can fully express what you have been carrying, the moment overwhelms you. You collapse. This is the point where the separate memories connect to what happened outside the world you have been experiencing: you had fallen into a coma, and the home-and-school routine was not your present reality.");

        AddStorySection(story, "THE MYSTERIOUS GUIDE",
            "A mysterious person appears throughout the journey and encourages you to keep moving toward the memories. They speak as though they understand that the world around you is not the whole truth. Their identity is deliberately left unknown. The story does not confirm who they are or provide a final explanation for their presence. What matters is the direction they offer: keep remembering, face what has been hidden, and find your way back.");

        AddStorySection(story, "WAKING UP — THE PEOPLE WHO WAITED",
            "As the bedroom memory reaches its breaking point, the familiar world gives way to the sound of a hospital monitor. You open your eyes in a hospital room. Your parents are there, close beside you, and their relief makes it clear that they have been waiting for you to wake. You reach for them and embrace them. The reunion is emotional because the distance that separated you from them has finally ended. You are no longer alone inside the false routine.");

        AddStorySection(story, "WHAT THE ENDING MEANS",
            "The school days, assessments, keys, locked rooms, classmates, and photographs formed the path through which your memories returned. The photographs were fragments of your life before the coma; recovering them brought you closer to understanding what had happened. The ordinary routine gave structure to the world you experienced, but it could not replace the people and reality waiting outside it. Reaching the good ending does not mean every difficulty has disappeared. It means you have returned, you have people beside you, and you have the possibility of speaking up and moving forward with support.");

        var buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.End;
        stack.AddChild(buttons);

        var credits = CreateButton("Continue to Credits");
        credits.Pressed += OpenCredits;
        buttons.AddChild(credits);
    }

    private static void AddStorySection(VBoxContainer parent, string heading, string body)
    {
        var title = new Label
        {
            Text = heading,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("e4c99f"));
        parent.AddChild(title);

        var paragraph = new Label
        {
            Text = body,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        paragraph.AddThemeFontSizeOverride("font_size", 20);
        paragraph.AddThemeConstantOverride("line_spacing", 5);
        paragraph.AddThemeColorOverride("font_color", new Color("e8edf5"));
        parent.AddChild(paragraph);
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(250, 52),
            FocusMode = Control.FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 19);
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
