using Godot;
using SchoolYardGame;

public partial class PauseMenuController : CanvasLayer
{
    public static PauseMenuController Instance { get; private set; }

    private Control _overlay;
    private Control _pauseMenuContent;
    private SettingsMenu _settingsMenu;
    private SaveLoadMenu _saveLoadMenu;
    private bool _isPaused;
    private bool _settingsOpen;
    private bool _saveLoadOpen;
    private bool _clockWasRunning;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Layer = 126;
        BuildMenu();
        _overlay.Visible = false;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (!Input.IsActionJustPressed("pauseGame"))
            return;

        if (_isPaused)
        {
            if (_settingsOpen)
                CloseEmbeddedSettings();
            else if (_saveLoadOpen)
                CloseSaveLoadMenu();
            else
                ResumeGame();
            return;
        }

        if (CanPauseCurrentScene())
            PauseGame();
    }

    private bool CanPauseCurrentScene()
    {
        if (SceneTransition.Instance?.IsBusy == true)
            return false;

        var currentScene = GetTree()?.CurrentScene;
        if (currentScene == null)
            return false;

        string path = currentScene.SceneFilePath ?? string.Empty;

        if (path.Contains("/Scenes/Menu/") || path.Contains("/Scenes/Ending/"))
            return false;

        return true;
    }

    private void PauseGame()
    {
        if (_isPaused)
            return;

        _clockWasRunning = DayClock.Instance?.Running ?? false;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        _isPaused = true;
        _settingsOpen = false;
        _pauseMenuContent.Visible = true;
        _overlay.Visible = true;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(true);
        GetTree().Paused = true;
    }

    private void ResumeGame()
    {
        if (!_isPaused)
            return;

        if (_settingsOpen)
            CloseEmbeddedSettings();
        if (_saveLoadOpen)
            CloseSaveLoadMenu();

        _isPaused = false;
        _overlay.Visible = false;
        GetTree().Paused = false;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);

        if (DayClock.Instance != null)
            DayClock.Instance.Running = _clockWasRunning;
    }

    private void OpenSettings()
    {
        if (_settingsOpen)
            return;

        var packedScene = GD.Load<PackedScene>("res://Scenes/Menu/SettingsMenu.tscn");
        if (packedScene == null)
        {
            GD.PushError("PauseMenuController: Could not load res://Scenes/Menu/SettingsMenu.tscn.");
            return;
        }

        _settingsMenu = packedScene.Instantiate<SettingsMenu>();
        _settingsMenu.EmbeddedInPauseMenu = true;
        _settingsMenu.ProcessMode = ProcessModeEnum.Always;
        _settingsMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _pauseMenuContent.Visible = false;
        _overlay.AddChild(_settingsMenu);
        _settingsOpen = true;
    }

    // Close the embedded settings screen without unpausing the game.
    public void CloseEmbeddedSettings()
    {
        if (!_settingsOpen)
            return;

        _settingsOpen = false;
        if (GodotObject.IsInstanceValid(_settingsMenu))
        {
            _overlay.RemoveChild(_settingsMenu);
            _settingsMenu.QueueFree();
        }

        _settingsMenu = null;
        _pauseMenuContent.Visible = true;
    }

    // Open the three manual save slots and separate autosave while keeping gameplay paused.
    private void OpenSaveLoadMenu()
    {
        if (_saveLoadOpen)
            return;

        var packedScene = GD.Load<PackedScene>("res://Scenes/Menu/SaveLoadMenu.tscn");
        if (packedScene == null)
        {
            GD.PushError("PauseMenuController: Could not load res://Scenes/Menu/SaveLoadMenu.tscn.");
            return;
        }

        _saveLoadMenu = packedScene.Instantiate<SaveLoadMenu>();
        _saveLoadMenu.AllowSaving = true;
        _saveLoadMenu.EmbeddedInPauseMenu = true;
        _saveLoadMenu.ProcessMode = ProcessModeEnum.Always;
        _saveLoadMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _pauseMenuContent.Visible = false;
        _overlay.AddChild(_saveLoadMenu);
        _saveLoadOpen = true;
    }

    // Close the save/load submenu without unpausing the game.
    public void CloseSaveLoadMenu()
    {
        if (!_saveLoadOpen)
            return;

        _saveLoadOpen = false;
        if (GodotObject.IsInstanceValid(_saveLoadMenu))
        {
            _overlay.RemoveChild(_saveLoadMenu);
            _saveLoadMenu.QueueFree();
        }

        _saveLoadMenu = null;
        _pauseMenuContent.Visible = true;
    }

    // Clear pause UI before a saved World scene is loaded.
    public void PrepareForExternalLoad()
    {
        if (_settingsOpen)
            CloseEmbeddedSettings();
        if (_saveLoadOpen)
            CloseSaveLoadMenu();

        _isPaused = false;
        _overlay.Visible = false;
        GetTree().Paused = false;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;
    }

    private void ReturnToTitle()
    {
        LeavePauseForSceneChange();
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        else
            GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
    }

    private void LeavePauseForSceneChange()
    {
        if (_settingsOpen)
            CloseEmbeddedSettings();
        if (_saveLoadOpen)
            CloseSaveLoadMenu();

        _isPaused = false;
        _overlay.Visible = false;
        GetTree().Paused = false;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
    }

    private void BuildMenu()
    {
        _overlay = new Control
        {
            Name = "PauseOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always
        };
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);

        _pauseMenuContent = new Control
        {
            Name = "PauseMenuContent",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always
        };
        _pauseMenuContent.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(_pauseMenuContent);

        var dimmer = new ColorRect
        {
            Name = "BackgroundDimmer",
            Color = new Color(0.025f, 0.035f, 0.07f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseMenuContent.AddChild(dimmer);

        var center = new CenterContainer
        {
            Name = "CenterContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseMenuContent.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "PausePanel",
            CustomMinimumSize = new Vector2(500, 430),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151b2b"),
            BorderColor = new Color("7c6aa6"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 36,
            ContentMarginRight = 36,
            ContentMarginTop = 30,
            ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        panel.AddChild(stack);

        var title = new Label
        {
            Text = "PAUSED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color("d8c6ff"));
        stack.AddChild(title);

        var description = new Label
        {
            Text = "Take a breath. The world is on hold.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeFontSizeOverride("font_size", 15);
        description.AddThemeColorOverride("font_color", new Color("c5cada"));
        stack.AddChild(description);

        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator_color", new Color("45405d"));
        stack.AddChild(separator);

        var resume = CreateMenuButton("Resume", true);
        resume.Pressed += ResumeGame;
        stack.AddChild(resume);

        var settings = CreateMenuButton("Settings", false);
        settings.Pressed += OpenSettings;
        stack.AddChild(settings);

        var saveLoad = CreateMenuButton("Save / Load Game", false);
        saveLoad.Pressed += OpenSaveLoadMenu;
        stack.AddChild(saveLoad);

        var titleButton = CreateMenuButton("Return to Main Menu", false);
        titleButton.Pressed += ReturnToTitle;
        stack.AddChild(titleButton);

        var hint = new Label
        {
            Text = "Press ESC to resume",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color("9691a9"));
        stack.AddChild(hint);
    }

    private static Button CreateMenuButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All
        };

        Color normal = primary ? new Color("7660a5") : new Color("252d42");
        Color hover = primary ? new Color("8973bb") : new Color("343e59");
        Color pressed = primary ? new Color("5b477f") : new Color("1c2335");

        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(normal, new Color("8272a7")));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(hover, new Color("b9a6e8")));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(pressed, new Color("b9a6e8")));
        button.AddThemeStyleboxOverride("focus", MakeButtonStyle(normal, new Color("d8c6ff")));
        button.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        button.AddThemeFontSizeOverride("font_size", 17);
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
