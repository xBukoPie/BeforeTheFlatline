using Godot;
using SchoolYardGame;
using System;
using System.Collections.Generic;

public partial class JournalController : CanvasLayer
{
    public static JournalController Instance { get; private set; }

    private const string WorldScenePath = "res://Scenes/Home/Areas/World.tscn";
    private static readonly string[] MemoryNames =
    {
        "Kitchen",
        "Laundry",
        "Bathroom",
        "MasterBedroom"
    };

    private Control _root;
    private VBoxContainer _pageBody;
    private readonly List<Button> _tabButtons = new();
    private Action _onClosed;
    private bool _isOpen;
    private bool _openedFromPauseMenu;
    private bool _replaying;
    private bool _previousTreePaused;
    private bool _previousClockRunning;
    private int _activeTab;

    public bool IsOpen => _isOpen;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        Name = "JournalController";
        Layer = 125;
        ProcessMode = ProcessModeEnum.Always;

        if (!InputMap.HasAction("openJournal"))
        {
            InputMap.AddAction("openJournal");
            InputMap.ActionAddEvent("openJournal", new InputEventKey { PhysicalKeycode = Key.J });
        }

        BuildUi();
        _root.Visible = false;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (GetTree().GetNodesInGroup("story_cutscene").Count > 0)
            return;

        if (!_isOpen)
        {
            if (!Input.IsActionJustPressed("openJournal"))
                return;

            if (PauseMenuController.Instance?.IsPaused == true)
            {
                PauseMenuController.Instance.OpenJournalFromPauseMenu();
                return;
            }

            if (CanOpenInGameplay())
                OpenFromGameplay();
            return;
        }

        if (Input.IsActionJustPressed("openJournal") || Input.IsActionJustPressed("pauseGame"))
            CloseJournal();
    }

    private bool CanOpenInGameplay()
    {
        string path = GetTree()?.CurrentScene?.SceneFilePath ?? string.Empty;
        return string.Equals(path, WorldScenePath, StringComparison.OrdinalIgnoreCase) &&
               SceneTransition.Instance?.IsBusy != true;
    }

    private void OpenFromGameplay()
    {
        if (_isOpen || !CanOpenInGameplay())
            return;

        _previousTreePaused = GetTree().Paused;
        _previousClockRunning = DayClock.Instance?.Running ?? false;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        GetTree().Paused = true;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(true);
        _openedFromPauseMenu = false;
        _onClosed = null;
        _isOpen = true;
        _activeTab = 0;
        _root.Visible = true;
        ShowTab(0);
    }

    public void OpenFromPauseMenu(Action onClosed)
    {
        if (_isOpen)
            return;

        _openedFromPauseMenu = true;
        _onClosed = onClosed;
        _isOpen = true;
        _activeTab = 0;
        _root.Visible = true;
        ShowTab(0);
    }

    public void CloseJournal()
    {
        if (!_isOpen || _replaying)
            return;

        _isOpen = false;
        _root.Visible = false;

        if (_openedFromPauseMenu)
        {
            Action callback = _onClosed;
            _onClosed = null;
            _openedFromPauseMenu = false;
            callback?.Invoke();
            return;
        }

        GetTree().Paused = _previousTreePaused;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
    }

    private void BuildUi()
    {
        _root = new Control
        {
            Name = "JournalRoot",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dimmer = new ColorRect
        {
            Name = "JournalDimmer",
            Color = new Color(0.015f, 0.02f, 0.045f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dimmer);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "JournalPanel",
            CustomMinimumSize = new Vector2(1120, 650),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("111827"),
            BorderColor = new Color("8272a7"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 26,
            ContentMarginRight = 26,
            ContentMarginTop = 22,
            ContentMarginBottom = 22
        });
        center.AddChild(panel);

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 16);
        panel.AddChild(outer);

        var header = new HBoxContainer();
        outer.AddChild(header);

        var heading = new Label
        {
            Text = "PERSONAL JOURNAL",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        heading.AddThemeFontSizeOverride("font_size", 28);
        heading.AddThemeColorOverride("font_color", new Color("e7ddf7"));
        header.AddChild(heading);

        var close = CreateButton("CLOSE  [J]", false);
        close.CustomMinimumSize = new Vector2(145, 42);
        close.Pressed += CloseJournal;
        header.AddChild(close);

        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator_color", new Color("45405d"));
        outer.AddChild(separator);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 18);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outer.AddChild(body);

        var navigation = new VBoxContainer
        {
            Name = "JournalTabs",
            CustomMinimumSize = new Vector2(225, 0)
        };
        navigation.AddThemeConstantOverride("separation", 10);
        body.AddChild(navigation);

        AddTab(navigation, "Memory Scenes", 0);
        AddTab(navigation, "Friends Info", 1);
        AddTab(navigation, "Assessment Scores", 2);
        AddTab(navigation, "Player Stats", 3);

        var pagePanel = new PanelContainer
        {
            Name = "JournalPagePanel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        pagePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("192235"),
            BorderColor = new Color("39445d"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 18,
            ContentMarginBottom = 18
        });
        body.AddChild(pagePanel);

        var scroll = new ScrollContainer
        {
            Name = "JournalPageScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollModeEnum.Disabled
        };
        pagePanel.AddChild(scroll);

        _pageBody = new VBoxContainer
        {
            Name = "JournalPageBody",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _pageBody.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_pageBody);

        var footer = new Label
        {
            Text = "Your journal keeps the memories and records from this playthrough.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", new Color("8994a8"));
        outer.AddChild(footer);
    }

    private void AddTab(VBoxContainer parent, string title, int index)
    {
        Button button = CreateButton(title, false);
        button.CustomMinimumSize = new Vector2(215, 50);
        button.Pressed += () => ShowTab(index);
        parent.AddChild(button);
        _tabButtons.Add(button);
    }

    private void ShowTab(int index)
    {
        _activeTab = Mathf.Clamp(index, 0, 3);
        foreach (Node child in _pageBody.GetChildren())
        {
            _pageBody.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].AddThemeStyleboxOverride(
                "normal",
                MakeButtonStyle(i == _activeTab ? new Color("51436f") : new Color("252d42"),
                    i == _activeTab ? new Color("d8c6ff") : new Color("665b7e")));
        }

        switch (_activeTab)
        {
            case 0: BuildMemoryPage(); break;
            case 1: BuildFriendsPage(); break;
            case 2: BuildAssessmentHistoryPage(); break;
            case 3: BuildPlayerStatsPage(); break;
        }
    }

    private void BuildMemoryPage()
    {
        AddPageHeading("MEMORY SCENES", "Only memories already recovered in this progression can be replayed.");

        string[] memories = StoryProgression.Instance?.GetMemoryOrder() ?? MemoryNames;
        int displayedMemories = 0;
        foreach (string memoryId in memories)
        {
            bool unlocked = StoryProgression.Instance?.HasSeenMemory(memoryId) == true;
            if (!unlocked)
                continue;

            displayedMemories++;
            string description = memoryId switch
            {
                "Kitchen" => "A familiar meal, an absent appetite, and the first uneasy fragment.",
                "Laundry" => "A washing machine, a school uniform, and the exhaustion of another day.",
                "Bathroom" => "The pressure of comparison, panic, and thoughts the player struggles to quiet.",
                "MasterBedroom" => "An attempt to ask for help, followed by the final memory and the good ending.",
                _ => "A recovered memory fragment."
            };

            var card = CreateCard();
            _pageBody.AddChild(card);
            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 8);
            card.AddChild(stack);

            var row = new HBoxContainer();
            stack.AddChild(row);

            var title = new Label
            {
                Text = memoryId switch
                {
                    "MasterBedroom" => "Master Bedroom",
                    _ => memoryId
                },
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 20);
            title.AddThemeColorOverride("font_color", new Color("eee8f7"));
            row.AddChild(title);

            var status = new Label
            {
                Text = unlocked ? "RECOVERED" : "LOCKED",
                VerticalAlignment = VerticalAlignment.Center
            };
            status.AddThemeFontSizeOverride("font_size", 13);
            status.AddThemeColorOverride("font_color", unlocked
                ? new Color("8ce0b4")
                : new Color("a0a8b8"));
            row.AddChild(status);

            var text = CreateBodyLabel(description);
            stack.AddChild(text);

            var replay = CreateButton(memoryId == "MasterBedroom" ? "Replay Memory & Good Ending" : "Replay Memory", true);
            replay.CustomMinimumSize = new Vector2(0, 42);
            replay.Pressed += () => ReplayMemory(memoryId);
            stack.AddChild(replay);
        }

        if (displayedMemories == 0)
            _pageBody.AddChild(CreateBodyLabel("No memories have been recovered in this progression yet. Return to the photographs as they become available."));
        else
            _pageBody.AddChild(CreateMutedLabel($"{displayedMemories} of {MemoryNames.Length} memory scenes recovered."));
    }

    private void BuildFriendsPage()
    {
        AddPageHeading("FRIENDS INFO", "A few notes about the classmates who spend time with you around school.");

        AddFriendCard(
            "Mich",
            "A friendly classmate with a quick sense of humor. Mich talks about canteen food, worksheets, small breaks, and the odd details of everyday school life.",
            "res://Assets/Characters/Mich.png");
        AddFriendCard(
            "Jepoy",
            "An energetic friend who enjoys sports and practice. His conversations often drift toward games, training, classroom antics, and music.",
            "res://Assets/Characters/Jepoy.png");
        AddFriendCard(
            "Cess",
            "A creative classmate who likes sketching and noticing little details. Cess talks about drawing the school, books, clouds, and study playlists.",
            "res://Assets/Characters/Cess.png");
    }

    private void AddFriendCard(string name, string description, string portraitPath)
    {
        var card = CreateCard();
        _pageBody.AddChild(card);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        card.AddChild(row);

        Texture2D portrait = GD.Load<Texture2D>(portraitPath);
        if (portrait != null)
        {
            var image = new TextureRect
            {
                Texture = portrait,
                CustomMinimumSize = new Vector2(108, 124),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(image);
        }

        var textStack = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(textStack);

        var title = CreatePageTitle(name);
        textStack.AddChild(title);
        textStack.AddChild(CreateBodyLabel(description));
    }

    private void BuildAssessmentHistoryPage()
    {
        AddPageHeading("ASSESSMENT SCORES HISTORY", "Your completed assessments, earned points, and recorded rewards.");

        IReadOnlyList<AssessmentHistoryEntry> history = DailyTestSession.AssessmentHistory;
        if (history.Count == 0)
        {
            _pageBody.AddChild(CreateBodyLabel("No completed assessments have been recorded in this playthrough yet."));
            return;
        }

        for (int i = history.Count - 1; i >= 0; i--)
        {
            AssessmentHistoryEntry entry = history[i];
            var card = CreateCard();
            _pageBody.AddChild(card);

            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 6);
            card.AddChild(stack);

            string status = entry.Passed ? "PASSED" : "FAILED";
            var title = CreatePageTitle($"Day {entry.Day}  •  Attempt {entry.Attempt}  •  {status}");
            title.AddThemeColorOverride("font_color", entry.Passed
                ? new Color("8ce0b4")
                : new Color("f0a0a0"));
            stack.AddChild(title);
            stack.AddChild(CreateBodyLabel($"Score: {entry.Score}/{entry.MaximumScore}   •   Passing score: {entry.PassingScore}"));
            stack.AddChild(CreateBodyLabel($"Points received: {entry.PointsReceived}"));
            stack.AddChild(CreateBodyLabel($"Sanity after assessment: {entry.SanityAfter}/{SanityManager.MaxSanity}"));
            stack.AddChild(CreateBodyLabel($"Rewards / penalties: {entry.RewardText}"));
            if (!string.IsNullOrWhiteSpace(entry.Timestamp))
                stack.AddChild(CreateMutedLabel(entry.Timestamp));
        }
    }

    private void BuildPlayerStatsPage()
    {
        AddPageHeading("PLAYER STATS", "Your current condition and progress in this playthrough.");

        int sanity = SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity;
        int totalMemories = StoryProgression.Instance?.CompletedMemoryCount ?? 0;

        AddStatCard("SANITY", $"{sanity} / {SanityManager.MaxSanity}");
        AddStatCard("UNLOCKED MEMORIES", $"{totalMemories} / {MemoryNames.Length}");

        var penaltyCard = CreateCard();
        _pageBody.AddChild(penaltyCard);
        var penaltyStack = new VBoxContainer();
        penaltyStack.AddThemeConstantOverride("separation", 7);
        penaltyCard.AddChild(penaltyStack);
        penaltyStack.AddChild(CreatePageTitle("FAILED-ASSESSMENT PENALTIES"));
        penaltyStack.AddChild(CreateMutedLabel("Memory scene effects are intentionally excluded from this section."));

        bool anyPenalty = false;
        foreach (SanityManager.DebuffType type in Enum.GetValues(typeof(SanityManager.DebuffType)))
        {
            int level = SanityManager.Instance?.GetAssessmentPenaltyLevel(type) ?? 0;
            if (level <= 0)
                continue;

            anyPenalty = true;
            penaltyStack.AddChild(CreateBodyLabel($"{type}: {level}"));
        }

        if (!anyPenalty)
            penaltyStack.AddChild(CreateBodyLabel("No active failed-assessment penalties."));
    }

    private void ReplayMemory(string memoryId)
    {
        if (_replaying || StoryProgression.Instance?.HasSeenMemory(memoryId) != true)
            return;

        Node roomManager = GetTree().GetFirstNodeInGroup("room_manager");
        Node uiRoot = roomManager?.GetNodeOrNull<Node>("UI");
        if (uiRoot == null)
        {
            GD.PushError("JournalController: Could not find the world UI layer for memory replay.");
            return;
        }

        List<StoryCutsceneBeat> beats = StoryPhotoInteractable.BuildMemoryBeats(memoryId);
        if (beats == null || beats.Count == 0)
            return;

        _root.Visible = false;
        _replaying = true;
        bool isEnding = string.Equals(memoryId, "MasterBedroom", StringComparison.OrdinalIgnoreCase);

        var overlay = new StoryCutsceneOverlay();
        overlay.Configure(
            StoryPhotoInteractable.GetMemoryHeading(memoryId),
            beats,
            OnReplayFinished,
            isEnding,
            memoryId,
            replayOnly: true,
            callbackAfterReveal: true);
        uiRoot.AddChild(overlay);
    }

    private void OnReplayFinished()
    {
        _replaying = false;
        if (_isOpen && GodotObject.IsInstanceValid(_root))
            _root.Visible = true;
    }

    private void AddPageHeading(string title, string description)
    {
        _pageBody.AddChild(CreatePageTitle(title, 25));
        _pageBody.AddChild(CreateMutedLabel(description));
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator_color", new Color("45405d"));
        _pageBody.AddChild(separator);
    }

    private void AddStatCard(string label, string value)
    {
        var card = CreateCard();
        _pageBody.AddChild(card);
        var row = new HBoxContainer();
        card.AddChild(row);

        var left = CreatePageTitle(label);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left);
        var right = CreatePageTitle(value);
        right.AddThemeColorOverride("font_color", new Color("d8c6ff"));
        row.AddChild(right);
    }

    private static PanelContainer CreateCard()
    {
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("222d42"),
            BorderColor = new Color("46546e"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 12
        });
        return card;
    }

    private static Label CreatePageTitle(string text, int size = 19)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("eee8f7"));
        return label;
    }

    private static Label CreateBodyLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        return label;
    }

    private static Label CreateMutedLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("939db1"));
        return label;
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(
            primary ? new Color("65528d") : new Color("252d42"),
            primary ? new Color("b9a6e8") : new Color("665b7e")));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(
            primary ? new Color("7660a5") : new Color("343e59"),
            new Color("b9a6e8")));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(
            new Color("51436f"), new Color("d8c6ff")));
        return button;
    }

    private static StyleBoxFlat MakeButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
    }
}
