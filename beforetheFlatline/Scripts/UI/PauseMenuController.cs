using Godot;
using SchoolYardGame;

/// <summary>
/// Global pause overlay. Escape toggles the menu while gameplay is active.
/// The SceneTree pause freezes gameplay processing; DayClock.Running is also
/// saved/restored so the in-game clock cannot advance while the menu is open.
/// </summary>
public partial class PauseMenuController : CanvasLayer
{
    private Control _overlay;
    private bool _isPaused;
    private bool _clockWasRunning;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 126;
        BuildMenu();
        _overlay.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Input.IsActionJustPressed("pauseGame"))
            return;

        if (_isPaused)
        {
            ResumeGame();
            return;
        }

        if (CanPauseCurrentScene())
            PauseGame();
    }

    private bool CanPauseCurrentScene()
    {
        var currentScene = GetTree()?.CurrentScene;
        if (currentScene == null)
            return false;

        string path = currentScene.SceneFilePath ?? string.Empty;

        // The pause overlay is for active play, not title/settings/ending screens.
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
        _overlay.Visible = true;
        GetTree().Paused = true;
    }

    private void ResumeGame()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        _overlay.Visible = false;
        GetTree().Paused = false;

        if (DayClock.Instance != null)
            DayClock.Instance.Running = _clockWasRunning;
    }

    private void OpenSettings()
    {
        LeavePauseForSceneChange();
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
        else
            GetTree().ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
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
        _isPaused = false;
        _overlay.Visible = false;
        GetTree().Paused = false;
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

        var dimmer = new ColorRect
        {
            Name = "BackgroundDimmer",
            Color = new Color(0.025f, 0.035f, 0.07f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(dimmer);

        var center = new CenterContainer
        {
            Name = "CenterContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(center);

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

        button.AddThemeStyleboxOverride("normal", MakeButtonStyle(normal, "8272a7"));
        button.AddThemeStyleboxOverride("hover", MakeButtonStyle(hover, "b9a6e8"));
        button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(pressed, "b9a6e8"));
        button.AddThemeStyleboxOverride("focus", MakeButtonStyle(normal, "d8c6ff"));
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
