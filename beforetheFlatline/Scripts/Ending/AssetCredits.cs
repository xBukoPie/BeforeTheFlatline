using Godot;
using SchoolYardGame;

public partial class AssetCredits : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";
    private const string TeamCreditsPath = "res://Scenes/Ending/EndingCredits.tscn";
    private const string PixabayUrl = "https://pixabay.com/";
    private const string IthacaFontUrl = "https://www.fontspace.com/ithaca-font-f144503";

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

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 56);
        margin.AddThemeConstantOverride("margin_right", 56);
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_bottom", 26);
        AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 14);
        margin.AddChild(layout);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE  •  VOXTEK INC.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 16);
        eyebrow.AddThemeColorOverride("font_color", new Color("c4b5fd"));
        layout.AddChild(eyebrow);

        var title = new Label
        {
            Text = "ASSET CREDITS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 38);
        title.AddThemeColorOverride("font_color", new Color("ffffff"));
        layout.AddChild(title);

        var intro = new Label
        {
            Text = "Credits for the music, sound effects, artwork, and font used in the game.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        intro.AddThemeFontSizeOverride("font_size", 18);
        intro.AddThemeColorOverride("font_color", new Color("e2e8f0"));
        layout.AddChild(intro);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        layout.AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 11);
        scroll.AddChild(content);

        AddHeading(content, "MUSIC & SOUND EFFECTS — PIXABAY");
        AddSubheading(content, "Music");
        AddFileList(content, new[]
        {
            "Menu/Menu.mp3",
            "Menu/Menu2.mp3",
            "Game/dayTime.mp3",
            "Game/nightTime.mp3",
            "Ending/gameOver.mp3",
            "Ending/goodEnding.mp3",
            "Ending/goodEnding2.wav",
            "Ending/sadEnding.mp3"
        });

        AddSubheading(content, "Sound effects");
        AddFileList(content, new[]
        {
            "Ending/gameOverFlatline.mp3",
            "Ending/goodEndingHeartbeat.mp3",
            "InterfaceInteraction/uiClick.wav",
            "InterfaceInteraction/uiDrag.mp3",
            "ObjectInteraction/doorKey.wav",
            "ObjectInteraction/doorUnlock.mp3",
            "ObjectInteraction/objectObtain.wav",
            "Tasks/passedTask.mp3",
            "Tasks/failedTask.mp3",
            "SoundFX/fallThud.mp3"
        });
        AddLink(content, "Pixabay source library", PixabayUrl);

        AddHeading(content, "ART & VISUAL ASSETS");
        AddFileList(content, new[]
        {
            "Character design and main menu — Chelzie Nicole Palaña (Game Design Handler)",
            "Environment and story images — Dominique Paul Guevarra (Game Assets Handler)",
            "Character artwork and portraits: player, Mich, Jepoy, Cess, Mysterious Person, and Professor Vox",
            "Home and school environments, memory photographs, and ending illustrations",
            "VoxTek logo, user-interface textures, and other interface artwork"
        });

        AddHeading(content, "FONT");
        AddParagraph(content, "Ithaca — used in the game's visual design.");
        AddLink(content, "Ithaca font source — FontSpace", IthacaFontUrl);

        AddParagraph(content,
            "Third-party assets remain subject to the usage terms and licenses provided by their respective sources.");

        var actions = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        actions.AddThemeConstantOverride("separation", 12);
        layout.AddChild(actions);

        var teamButton = new Button
        {
            Text = "Back to Team Credits",
            CustomMinimumSize = new Vector2(230, 48)
        };
        teamButton.Pressed += () => ChangeScene(TeamCreditsPath);
        actions.AddChild(teamButton);

        var menuButton = new Button
        {
            Text = "Return to Main Menu",
            CustomMinimumSize = new Vector2(230, 48)
        };
        menuButton.Pressed += () => ChangeScene(MainMenuPath);
        actions.AddChild(menuButton);
    }

    private static void AddHeading(VBoxContainer parent, string text)
    {
        var heading = new Label { Text = text };
        heading.AddThemeFontSizeOverride("font_size", 22);
        heading.AddThemeColorOverride("font_color", new Color("f3d9a6"));
        parent.AddChild(heading);
    }

    private static void AddSubheading(VBoxContainer parent, string text)
    {
        var heading = new Label { Text = text };
        heading.AddThemeFontSizeOverride("font_size", 19);
        heading.AddThemeColorOverride("font_color", new Color("c4b5fd"));
        parent.AddChild(heading);
    }

    private static void AddParagraph(VBoxContainer parent, string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", new Color("e2e8f0"));
        parent.AddChild(label);
    }

    private static void AddFileList(VBoxContainer parent, string[] lines)
    {
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(list);

        foreach (string line in lines)
        {
            var label = new Label
            {
                Text = "• " + line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            label.AddThemeFontSizeOverride("font_size", 17);
            label.AddThemeColorOverride("font_color", new Color("f1f5f9"));
            list.AddChild(label);
        }
    }

    private static void AddLink(VBoxContainer parent, string text, string url)
    {
        var link = new LinkButton
        {
            Text = text,
            TooltipText = url,
            FocusMode = Control.FocusModeEnum.All
        };
        link.AddThemeFontSizeOverride("font_size", 17);
        link.AddThemeColorOverride("font_color", new Color("93c5fd"));
        link.AddThemeColorOverride("font_hover_color", new Color("bfdbfe"));
        link.Pressed += () => OS.ShellOpen(url);
        parent.AddChild(link);
    }

    private void ChangeScene(string path)
    {
        // The music is shared by both credits scenes. Only fade it when leaving credits.
        if (string.Equals(path, MainMenuPath, System.StringComparison.OrdinalIgnoreCase))
            AudioManager.Instance?.FadeOutGoodEnding2(0.85);

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(path, 0.8f);
        else
            GetTree().ChangeSceneToFile(path);
    }
}
