using Godot;
using SchoolYardGame;

public partial class AssetCredits : Control
{
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";
    private const string TeamCreditsPath = "res://Scenes/Ending/EndingCredits.tscn";

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
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 28);
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
        eyebrow.AddThemeFontSizeOverride("font_size", 13);
        eyebrow.AddThemeColorOverride("font_color", new Color("b8a4dd"));
        layout.AddChild(eyebrow);

        var title = new Label
        {
            Text = "ASSET CREDITS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color("eee8f7"));
        layout.AddChild(title);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        layout.AddChild(scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);

        AddHeading(content, "MUSIC — PIXABAY");
        AddParagraph(content, "The game's music tracks are sourced from Pixabay. The repository inventory includes:");
        AddFileList(content, new[]
        {
            "Menu/Menu.mp3", "Menu/Menu2.mp3", "Game/dayTime.mp3", "Game/nightTime.mp3",
            "Ending/gameOver.mp3", "Ending/goodEnding.mp3", "Ending/goodEnding2.wav", "Ending/sadEnding.mp3"
        });

        AddHeading(content, "SOUND EFFECTS & ENDING CUES — PIXABAY");
        AddFileList(content, new[]
        {
            "Ending/gameOverFlatline.mp3", "Ending/goodEndingHeartbeat.mp3",
            "InterfaceInteraction/uiClick.wav", "InterfaceInteraction/uiDrag.mp3",
            "ObjectInteraction/doorKey.wav", "ObjectInteraction/doorUnlock.mp3",
            "ObjectInteraction/objectObtain.wav", "Tasks/passedTask.mp3", "Tasks/failedTask.mp3",
            "SoundFX/fallThud.mp3"
        });

        AddHeading(content, "ART, ENVIRONMENTS & STORY IMAGES");
        AddFileList(content, new[]
        {
            "Characters: player sprite, Mich, Jepoy, Cess, Mysterious figure, Professor Vox portraits",
            "Home and school environment scenes and map textures",
            "Memory images: cutsceneKitcken.jpg, cutsceneLaundry.jpg, cutsceneBathroomSink.jpg, cutsceneToMasterBedroom.jpg, cutsceneMasterBedroom.jpg",
            "Ending images: cutsceneGoodEnding1.jpg, cutsceneGoodEnding2.jpg, cutsceneBadEnding.jpg",
            "Opening identity: VoxTekLogo.png; UI textures and interface artwork"
        });

        AddHeading(content, "FONT & OTHER THIRD-PARTY RESOURCES");
        AddFileList(content, new[]
        {
            "Assets/TextFonts/Ithaca-LVB75.ttf — source creator and license need verification before public distribution"
        });

        AddParagraph(content,
            "Source library: Pixabay — https://pixabay.com/ • Individual uploader names and original item-page URLs are being matched against the original download records and will be added when verified.");

        AddHeading(content, "PROJECT ASSET PREPARATION & INTEGRATION");
        AddParagraph(content,
            "VoxTek INC. — project asset preparation, scene integration, implementation, and presentation. Individual third-party artwork, font, and media licenses must be checked against their original sources before public distribution.");

        AddHeading(content, "PROJECT TEAM");
        AddFileList(content, new[]
        {
            "Edward Aniciete — Project Manager / Team Lead; Script Handler",
            "Chelzie Nicole Palaña — Game Design Handler",
            "Dominique Paul Guevarra — Assets / Texture Handler",
            "VoxTek INC. — Production"
        });

        AddParagraph(content,
            "Attribution note: this screen identifies Pixabay as the source library for the project's music and sound effects. It does not replace the original Pixabay item-page records or license checks. Track-specific titles and contributor names will only be displayed after the source pages have been verified.");

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 12);
        layout.AddChild(actions);

        var teamButton = new Button
        {
            Text = "Back to Team Credits",
            CustomMinimumSize = new Vector2(230, 46)
        };
        teamButton.Pressed += () => ChangeScene(TeamCreditsPath);
        actions.AddChild(teamButton);

        var menuButton = new Button
        {
            Text = "Return to Main Menu",
            CustomMinimumSize = new Vector2(230, 46)
        };
        menuButton.Pressed += () => ChangeScene(MainMenuPath);
        actions.AddChild(menuButton);
    }

    private static void AddHeading(VBoxContainer parent, string text)
    {
        var heading = new Label { Text = text };
        heading.AddThemeFontSizeOverride("font_size", 17);
        heading.AddThemeColorOverride("font_color", new Color("e1c99f"));
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
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        parent.AddChild(label);
    }

    private static void AddFileList(VBoxContainer parent, string[] lines)
    {
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 3);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(list);

        foreach (string line in lines)
        {
            var label = new Label
            {
                Text = "• " + line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", new Color("e2e8f0"));
            list.AddChild(label);
        }
    }

    private void ChangeScene(string path)
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(path, 0.8f);
        else
            GetTree().ChangeSceneToFile(path);
    }
}
