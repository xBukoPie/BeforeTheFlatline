using Godot;
using SchoolYardGame;

/// <summary>
/// Three manual save slots and a separate autosave slot. Can be opened from the title screen
/// (load-only) or from the paused world (save and load).
/// </summary>
public partial class SaveLoadMenu : Control
{
    public bool AllowSaving { get; set; }
    public bool EmbeddedInPauseMenu { get; set; }

    private VBoxContainer _slotList;
    private Label _status;
    private bool _busy;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        RefreshSlots();

        if (AllowSaving && SaveManager.Instance?.CanSaveCurrentGame != true)
            _status.Text = "Manual saves are available while exploring the world.";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!EmbeddedInPauseMenu && @event.IsActionPressed("pauseGame"))
        {
            GetViewport().SetInputAsHandled();
            QueueFree();
        }
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Name = "Background",
            Color = new Color("080b14"),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            Name = "SaveLoadPanel",
            CustomMinimumSize = new Vector2(900, 600),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151b2b"),
            BorderColor = new Color("66598a"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 30,
            ContentMarginRight = 30,
            ContentMarginTop = 24,
            ContentMarginBottom = 24
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        panel.AddChild(stack);

        var title = new Label
        {
            Text = "SAVE / LOAD GAME",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color("d8c6ff"));
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = AllowSaving
                ? "Choose a slot to save your current run or load a previous one."
                : "Select a save slot to continue a previous run.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(subtitle);

        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator_color", new Color("45405d"));
        stack.AddChild(separator);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        stack.AddChild(scroll);

        _slotList = new VBoxContainer();
        _slotList.AddThemeConstantOverride("separation", 10);
        _slotList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_slotList);

        _status = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _status.AddThemeColorOverride("font_color", new Color("f0d38a"));
        _status.CustomMinimumSize = new Vector2(0, 24);
        stack.AddChild(_status);

        var footer = new HBoxContainer();
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        stack.AddChild(footer);

        var back = MakeButton(EmbeddedInPauseMenu ? "Back to Pause Menu" : "Back to Main Menu", false);
        back.CustomMinimumSize = new Vector2(240, 44);
        back.Pressed += CloseMenu;
        footer.AddChild(back);
    }

    private void RefreshSlots()
    {
        if (_slotList == null || !GodotObject.IsInstanceValid(_slotList))
            return;

        foreach (Node child in _slotList.GetChildren())
        {
            _slotList.RemoveChild(child);
            child.QueueFree();
        }

        var saves = SaveManager.Instance;
        for (int slot = 1; slot <= SaveManager.ManualSlotCount; slot++)
            AddManualSlotRow(slot, saves);

        AddAutoSaveRow(saves);
    }

    private void AddManualSlotRow(int slot, SaveManager saves)
    {
        var row = MakeSlotRow();
        var textStack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        textStack.AddChild(MakeSlotTitle($"SAVE SLOT {slot}"));
        string timestamp = saves?.GetSlotTimestamp(slot) ?? "";
        textStack.AddChild(MakeSlotSubtitle(string.IsNullOrWhiteSpace(timestamp) ? "Empty slot" : $"Saved: {timestamp}"));
        row.AddChild(textStack);

        var save = MakeButton("Save", true);
        save.CustomMinimumSize = new Vector2(100, 44);
        save.Disabled = !AllowSaving || saves?.CanSaveCurrentGame != true || _busy;
        int saveSlot = slot;
        save.Pressed += () => OnSavePressed(saveSlot);
        row.AddChild(save);

        var load = MakeButton("Load", false);
        load.CustomMinimumSize = new Vector2(100, 44);
        load.Disabled = saves?.HasSlot(slot) != true || _busy;
        int loadSlot = slot;
        load.Pressed += async () => await OnLoadSlotPressed(loadSlot);
        row.AddChild(load);

        _slotList.AddChild(row);
    }

    private void AddAutoSaveRow(SaveManager saves)
    {
        var row = MakeSlotRow();
        var textStack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        textStack.AddChild(MakeSlotTitle("AUTO SAVE"));
        string timestamp = saves?.GetAutoSaveTimestamp() ?? "";
        textStack.AddChild(MakeSlotSubtitle(string.IsNullOrWhiteSpace(timestamp) ? "No autosave yet" : $"Saved: {timestamp}"));
        row.AddChild(textStack);

        var autoLoad = MakeButton("Load Auto", false);
        autoLoad.CustomMinimumSize = new Vector2(130, 44);
        autoLoad.Disabled = saves?.HasAutoSave != true || _busy;
        autoLoad.Pressed += async () => await OnLoadAutoSavePressed();
        row.AddChild(autoLoad);

        _slotList.AddChild(row);
    }

    private async void OnLoadSlotPressed(int slot)
    {
        if (_busy || SaveManager.Instance == null)
            return;

        _busy = true;
        _status.Text = $"Loading save slot {slot}...";
        bool loaded = await SaveManager.Instance.LoadSlotAsync(slot);
        if (!loaded && GodotObject.IsInstanceValid(this))
        {
            _status.Text = $"Could not load slot {slot}. Check that the saved room files still exist.";
            _busy = false;
            RefreshSlots();
        }
    }

    private async System.Threading.Tasks.Task OnLoadAutoSavePressed()
    {
        if (_busy || SaveManager.Instance == null)
            return;

        _busy = true;
        _status.Text = "Loading autosave...";
        bool loaded = await SaveManager.Instance.LoadAutoSaveAsync();
        if (!loaded && GodotObject.IsInstanceValid(this))
        {
            _status.Text = "Could not load the autosave. Check that the saved room files still exist.";
            _busy = false;
            RefreshSlots();
        }
    }

    private void OnSavePressed(int slot)
    {
        if (_busy)
            return;

        bool saved = SaveManager.Instance?.SaveSlot(slot) == true;
        _status.Text = saved
            ? $"Slot {slot} saved successfully."
            : "Could not save here. Save while exploring the world.";
        RefreshSlots();
    }

    private void CloseMenu()
    {
        if (EmbeddedInPauseMenu)
            PauseMenuController.Instance?.CloseSaveLoadMenu();
        else
            QueueFree();
    }

    private static HBoxContainer MakeSlotRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.CustomMinimumSize = new Vector2(0, 70);
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("1d2538"),
            BorderColor = new Color("39435f"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        return row;
    }

    private static Label MakeSlotTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color("f1edff"));
        return label;
    }

    private static Label MakeSlotSubtitle(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color("aaaec1"));
        return label;
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 44),
            FocusMode = Control.FocusModeEnum.All
        };
        Color background = primary ? new Color("7660a5") : new Color("252d42");
        Color hover = primary ? new Color("8973bb") : new Color("343e59");
        button.AddThemeStyleboxOverride("normal", ButtonStyle(background));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(hover));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(primary ? new Color("5b477f") : new Color("1c2335")));
        button.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        button.AddThemeFontSizeOverride("font_size", 15);
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color color) => new StyleBoxFlat
    {
        BgColor = color,
        BorderColor = new Color("625985"),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 7,
        ContentMarginBottom = 7
    };
}
