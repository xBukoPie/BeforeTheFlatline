using Godot;
using SchoolYardGame;

/// <summary>Runtime-built settings panel. Each control applies changes immediately and persists them.</summary>
public partial class SettingsMenu : Control
{
    /// <summary>When true, this settings panel is a submenu of the gameplay pause overlay.</summary>
    public bool EmbeddedInPauseMenu { get; set; }

    private Label _volumeValue;
    private Label _brightnessValue;
    private HSlider _volume;
    private HSlider _brightness;
    private CheckButton _fullscreen;

    public override void _Ready()
    {
        BuildUi();
        SyncFromSettings();
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SettingsChanged += SyncFromSettings;
    }

    public override void _ExitTree()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SettingsChanged -= SyncFromSettings;
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("0b1020"), MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(650, 520) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("172033"), BorderColor = new Color("46546e"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 36, ContentMarginRight = 36, ContentMarginTop = 30, ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 18);
        panel.AddChild(stack);

        var title = new Label { Text = "SETTINGS", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        stack.AddChild(title);

        var description = new Label
        {
            Text = "Changes are applied immediately and saved for the next session.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(description);

        var volumeRow = new VBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 5);
        stack.AddChild(volumeRow);
        var volumeHeading = new HBoxContainer();
        volumeHeading.AddChild(new Label { Text = "Master volume", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _volumeValue = new Label { Text = "85%" };
        volumeHeading.AddChild(_volumeValue);
        volumeRow.AddChild(volumeHeading);

        _volume = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, CustomMinimumSize = new Vector2(0, 28) };
        _volume.ValueChanged += value =>
        {
            SettingsManager.Instance?.SetMasterVolume((float)value);
            _volumeValue.Text = $"{Mathf.RoundToInt((float)value * 100)}%";
        };
        volumeRow.AddChild(_volume);

        var brightnessRow = new VBoxContainer();
        brightnessRow.AddThemeConstantOverride("separation", 5);
        stack.AddChild(brightnessRow);
        var brightnessHeading = new HBoxContainer();
        brightnessHeading.AddChild(new Label { Text = "Brightness", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _brightnessValue = new Label { Text = "88%" };
        brightnessHeading.AddChild(_brightnessValue);
        brightnessRow.AddChild(brightnessHeading);

        _brightness = new HSlider { MinValue = 0.65, MaxValue = 1.05, Step = 0.05, CustomMinimumSize = new Vector2(0, 28) };
        _brightness.ValueChanged += value =>
        {
            SettingsManager.Instance?.SetBrightness((float)value);
            _brightnessValue.Text = $"{Mathf.RoundToInt((float)value * 100)}%";
        };
        brightnessRow.AddChild(_brightness);

        _fullscreen = new CheckButton { Text = "Fullscreen" };
        _fullscreen.Toggled += enabled => SettingsManager.Instance?.SetFullscreen(enabled);
        stack.AddChild(_fullscreen);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        stack.AddChild(footer);
        var back = new Button
        {
            Text = EmbeddedInPauseMenu ? "Back to Pause Menu" : "Back to Main Menu",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        back.Pressed += () =>
        {
            if (EmbeddedInPauseMenu)
            {
                PauseMenuController.Instance?.CloseEmbeddedSettings();
                return;
            }

            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        };
        footer.AddChild(back);
        var defaults = new Button { Text = "Defaults", CustomMinimumSize = new Vector2(130, 48) };
        defaults.Pressed += () =>
        {
            SettingsManager.Instance?.SetMasterVolume(0.85f);
            SettingsManager.Instance?.SetBrightness(0.88f);
            SettingsManager.Instance?.SetFullscreen(false);
            SyncFromSettings();
        };
        footer.AddChild(defaults);
    }

    private void SyncFromSettings()
    {
        if (SettingsManager.Instance == null || _volume == null)
            return;

        _volume.SetValueNoSignal(SettingsManager.Instance.MasterVolume);
        _brightness.SetValueNoSignal(SettingsManager.Instance.Brightness);
        _fullscreen.SetPressedNoSignal(SettingsManager.Instance.Fullscreen);
        _volumeValue.Text = $"{Mathf.RoundToInt(SettingsManager.Instance.MasterVolume * 100)}%";
        _brightnessValue.Text = $"{Mathf.RoundToInt(SettingsManager.Instance.Brightness * 100)}%";
    }
}
