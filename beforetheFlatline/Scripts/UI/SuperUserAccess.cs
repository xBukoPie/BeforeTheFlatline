using Godot;
using SchoolYardGame;

public partial class SuperUserAccess : CanvasLayer
{
    private const string AccessPhrase = "SUPERBFT";

    private Control _root;
    private Button _toggleButton;
    private PanelContainer _panel;
    private VBoxContainer _lockedSection;
    private VBoxContainer _adminSection;
    private LineEdit _accessInput;
    private Label _status;
    private OptionButton _debuffType;
    private SpinBox _amount;
    private SpinBox _debuffIntensity;
    private OptionButton _keyRoom;
    private bool _authenticated;

    private static readonly string[] KeyRoomIds =
    {
        "Kitchen",
        "Laundry",
        "Bathroom",
        "MasterBedroom"
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100;
        BuildUi();
    }

    private void BuildUi()
    {
        _root = new Control { Name = "SuperUserOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _toggleButton = new Button
        {
            Text = "SUPERUSER",
            TooltipText = "Open SuperUser access",
            FocusMode = Control.FocusModeEnum.None
        };
        SetTopRight(_toggleButton, -164, 10, -12, 44);
        _toggleButton.Pressed += TogglePanel;
        _root.AddChild(_toggleButton);

        _panel = new PanelContainer { Name = "SuperUserPanel", Visible = false, CustomMinimumSize = new Vector2(390, 620) };
        _panel.AnchorLeft = 1f;
        _panel.AnchorRight = 1f;
        _panel.AnchorTop = 0f;
        _panel.AnchorBottom = 0f;
        _panel.OffsetLeft = -402;
        _panel.OffsetRight = -12;
        _panel.OffsetTop = 52;
        _panel.OffsetBottom = 682;
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("101827"),
            BorderColor = new Color("7c6be8"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        });
        _root.AddChild(_panel);

        var scroll = new ScrollContainer
        {
            Name = "AdminScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _panel.AddChild(scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);

        var title = new Label
        {
            Text = "SUPERUSER / ADMIN CONSOLE",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color("c4b5fd"));
        content.AddChild(title);

        _lockedSection = new VBoxContainer();
        _lockedSection.AddThemeConstantOverride("separation", 8);
        content.AddChild(_lockedSection);
        _lockedSection.AddChild(new Label
        {
            Text = "Enter the access phrase to enable testing controls.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        _accessInput = new LineEdit
        {
            PlaceholderText = "Access phrase",
            Secret = true,
            CustomMinimumSize = new Vector2(0, 38)
        };
        _accessInput.TextSubmitted += _ => Unlock();
        _lockedSection.AddChild(_accessInput);
        var unlockButton = MakeButton("Unlock Admin Access");
        unlockButton.Pressed += Unlock;
        _lockedSection.AddChild(unlockButton);

        _adminSection = new VBoxContainer { Visible = false };
        _adminSection.AddThemeConstantOverride("separation", 10);
        content.AddChild(_adminSection);

        AddSectionHeader(_adminSection, "ASSESSMENT");
        var solveButton = MakeButton("Auto-solve current question & continue");
        solveButton.Pressed += SolveCurrentQuestion;
        _adminSection.AddChild(solveButton);

        AddSectionHeader(_adminSection, "SANITY & DEBUFFS");
        var amountRow = new HBoxContainer();
        amountRow.AddChild(new Label { Text = "Amount", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        _amount = new SpinBox { MinValue = 1, MaxValue = 100, Step = 1, Value = 10, CustomMinimumSize = new Vector2(110, 34) };
        amountRow.AddChild(_amount);
        _adminSection.AddChild(amountRow);

        var debuffRow = new HBoxContainer();
        debuffRow.AddChild(new Label { Text = "Debuff", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        _debuffType = new OptionButton { CustomMinimumSize = new Vector2(180, 34) };
        foreach (SanityManager.DebuffType type in System.Enum.GetValues(typeof(SanityManager.DebuffType)))
            _debuffType.AddItem(type.ToString());
        _debuffType.Select(0);
        debuffRow.AddChild(_debuffType);
        _adminSection.AddChild(debuffRow);

        var debuffIntensityRow = new HBoxContainer();
        debuffIntensityRow.AddChild(new Label { Text = "Debuff level", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        _debuffIntensity = new SpinBox { MinValue = 1, MaxValue = SanityManager.MaxSanity, Step = 1, Value = 25, CustomMinimumSize = new Vector2(110, 34) };
        debuffIntensityRow.AddChild(_debuffIntensity);
        _adminSection.AddChild(debuffIntensityRow);

        var sanityActions = new HBoxContainer();
        var removeSanity = MakeButton("Remove sanity");
        removeSanity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        removeSanity.Pressed += RemoveSanity;
        sanityActions.AddChild(removeSanity);
        var addDebuff = MakeButton("Add debuff");
        addDebuff.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        addDebuff.Pressed += AddDebuff;
        sanityActions.AddChild(addDebuff);
        _adminSection.AddChild(sanityActions);

        var clearDebuff = MakeButton("Remove selected debuff");
        clearDebuff.Pressed += ClearDebuff;
        _adminSection.AddChild(clearDebuff);

        AddSectionHeader(_adminSection, "ROOM KEYS");
        _keyRoom = new OptionButton { CustomMinimumSize = new Vector2(0, 34) };
        _keyRoom.AddItem("Kitchen");
        _keyRoom.AddItem("Laundry");
        _keyRoom.AddItem("Bathroom");
        _keyRoom.AddItem("Master Bedroom");
        _keyRoom.Select(0);
        _adminSection.AddChild(_keyRoom);
        var grantKey = MakeButton("Grant selected room key");
        grantKey.Pressed += GrantKey;
        _adminSection.AddChild(grantKey);

        AddSectionHeader(_adminSection, "EVENTS & CONDITIONS");
        var schoolComplete = MakeButton("Mark school assessment complete");
        schoolComplete.Pressed += () =>
        {
            DayClock.Instance?.MarkSchoolChallengeCompletedToday();
            SetStatus("School assessment completion condition triggered.");
        };
        _adminSection.AddChild(schoolComplete);

        var schoolOpen = MakeButton("Set time to 08:00 (school open)");
        schoolOpen.Pressed += () =>
        {
            DayClock.Instance?.SetHourOfDay(8f);
            SetStatus("Clock set to 08:00.");
        };
        _adminSection.AddChild(schoolOpen);

        var nextDay = MakeButton("Advance one day");
        nextDay.Pressed += () =>
        {
            DayClock.Instance?.AdvanceHours(24f);
            SetStatus("Advanced the in-game day by 24 hours.");
        };
        _adminSection.AddChild(nextDay);

        var mysteryRow = new HBoxContainer();
        var markMystery = MakeButton("Mark mystery seen");
        markMystery.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        markMystery.Pressed += () =>
        {
            DayClock.Instance?.MarkFirstDayMysteryEncounterCompleted();
            SetStatus("First-day mystery is marked seen; the special greeting is pending.");
        };
        mysteryRow.AddChild(markMystery);

        var resetMystery = MakeButton("Reset mystery");
        resetMystery.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        resetMystery.Pressed += () =>
        {
            DayClock.Instance?.RestoreFirstDayMysteryState(false, false);
            SetStatus("First-day mystery condition reset.");
        };
        mysteryRow.AddChild(resetMystery);
        _adminSection.AddChild(mysteryRow);

        var lockButton = MakeButton("Lock admin access");
        lockButton.Pressed += () =>
        {
            _authenticated = false;
            _lockedSection.Visible = true;
            _adminSection.Visible = false;
            _accessInput.Clear();
            _toggleButton.Text = "SUPERUSER";
            SetStatus("Admin access locked.");
        };
        _adminSection.AddChild(lockButton);

        _status = new Label
        {
            Text = "Admin access is locked.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 44)
        };
        _status.AddThemeColorOverride("font_color", new Color("fcd34d"));
        content.AddChild(_status);
    }

    private static void SetTopRight(Control control, float leftOffset, float topOffset, float rightOffset, float bottomOffset)
    {
        control.AnchorLeft = 1f;
        control.AnchorRight = 1f;
        control.AnchorTop = 0f;
        control.AnchorBottom = 0f;
        control.OffsetLeft = leftOffset;
        control.OffsetRight = rightOffset;
        control.OffsetTop = topOffset;
        control.OffsetBottom = bottomOffset;
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        CustomMinimumSize = new Vector2(0, 34),
        FocusMode = Control.FocusModeEnum.All
    };

    private static void AddSectionHeader(VBoxContainer parent, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("93c5fd"));
        parent.AddChild(label);
    }

    private void TogglePanel()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible && !_authenticated)
            _accessInput.GrabFocus();
    }

    private void Unlock()
    {
        if (string.Equals(_accessInput.Text.Trim(), AccessPhrase, System.StringComparison.OrdinalIgnoreCase))
        {
            _authenticated = true;
            _lockedSection.Visible = false;
            _adminSection.Visible = true;
            _toggleButton.Text = "ADMIN PANEL";
            SetStatus("Access granted. Changes affect the current game session.");
        }
        else
        {
            SetStatus("Access denied. Check the phrase and try again.");
        }
    }

    private void SolveCurrentQuestion()
    {
        foreach (Node node in GetTree().GetNodesInGroup("challenge_scene"))
        {
            if (node is ChallengeScene challenge)
            {
                SetStatus(challenge.SuperUserSolveCurrentQuestion()
                    ? "Correct solution generated and submitted."
                    : "No active question can be auto-solved right now.");
                return;
            }
        }
        SetStatus("Open an assessment question first.");
    }

    private void RemoveSanity()
    {
        var sanity = SanityManager.Instance;
        if (sanity == null)
        {
            SetStatus("Sanity manager is unavailable.");
            return;
        }
        int amount = Mathf.RoundToInt((float)_amount.Value);
        sanity.RemoveSanityForAdmin(amount);
        SetStatus($"Removed {amount} sanity.");
    }

    private SanityManager.DebuffType SelectedDebuff =>
        (SanityManager.DebuffType)Mathf.Clamp(_debuffType.Selected, 0, System.Enum.GetValues(typeof(SanityManager.DebuffType)).Length - 1);

    private void AddDebuff()
    {
        var sanity = SanityManager.Instance;
        if (sanity == null)
        {
            SetStatus("Sanity manager is unavailable.");
            return;
        }

        var type = SelectedDebuff;
        int addition = Mathf.RoundToInt((float)_debuffIntensity.Value);
        sanity.SetDebuffLevelForAdmin(type, sanity.GetDebuffLevel(type) + addition);
        SetStatus($"Added {addition} levels of {type}.");
    }

    private void ClearDebuff()
    {
        var sanity = SanityManager.Instance;
        if (sanity == null)
        {
            SetStatus("Sanity manager is unavailable.");
            return;
        }

        var type = SelectedDebuff;
        sanity.ClearDebuffForAdmin(type);
        SetStatus($"Removed {type}.");
    }

    private void GrantKey()
    {
        var progression = RoomKeyProgression.Instance;
        if (progression == null)
        {
            SetStatus("Room key progression service is unavailable.");
            return;
        }

        int selected = Mathf.Clamp(_keyRoom.Selected, 0, KeyRoomIds.Length - 1);
        string roomName = progression.AdminGrantKeyForRoom(KeyRoomIds[selected]);
        if (string.IsNullOrWhiteSpace(roomName))
        {
            SetStatus($"{_keyRoom.GetItemText(selected)} is already unlocked.");
            return;
        }

        if (SaveManager.Instance?.CanSaveCurrentGame == true)
            SaveManager.Instance.RequestAutoSave();
        SetStatus($"Admin key granted: {roomName} unlocked.");
    }

    private void SetStatus(string message)
    {
        if (_status != null)
            _status.Text = message;
    }
}
