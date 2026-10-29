using Godot;
using SchoolYardGame;
using System.Threading.Tasks;

public partial class MainMenu : Control
{
    [Export] public float FadeTime { get; set; } = 0.25f;
    private ColorRect _fade;
    private bool _isStarting = false;
    private static bool _openingSplashPlayed;

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;
        _fade = GetNode<ColorRect>("UI/Fade");
        if (!_openingSplashPlayed)
        {
            _openingSplashPlayed = true;
            PlayOpeningSplash();
        }
    }


    private void PlayOpeningSplash()
    {
        var overlay = new Control
        {
            Name = "VoxTekOpeningSplash",
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.ZIndex = 100;
        AddChild(overlay);

        var background = new ColorRect { Color = new Color("080b14"), MouseFilter = Control.MouseFilterEnum.Stop };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(background);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 22);
        stack.CustomMinimumSize = new Vector2(600, 0);
        center.AddChild(stack);

        var presented = new Label { Text = "VOXTEK INC. PRESENTS", HorizontalAlignment = HorizontalAlignment.Center };
        presented.AddThemeFontSizeOverride("font_size", 18);
        presented.AddThemeColorOverride("font_color", new Color("a8a0c7"));
        stack.AddChild(presented);

        var logo = new PanelContainer { CustomMinimumSize = new Vector2(420, 210), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        logo.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("111827"), BorderColor = new Color("655889"),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 18, CornerRadiusTopRight = 18, CornerRadiusBottomLeft = 18, CornerRadiusBottomRight = 18
        });
        stack.AddChild(logo);

        var logoText = new Label { Text = "VOXTEK\\nLOGO PLACEHOLDER", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        logoText.AddThemeFontSizeOverride("font_size", 24);
        logoText.AddThemeColorOverride("font_color", new Color("d8c6ff"));
        logo.AddChild(logoText);

        var title = new Label { Text = "BEFORE THE FLATLINE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color("e2e8f0"));
        stack.AddChild(title);

        overlay.Modulate = new Color(1f, 1f, 1f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 1f, 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenInterval(1.8f);
        tween.TweenProperty(overlay, "modulate:a", 0f, 0.75f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(() => overlay.QueueFree()));
    }

    private void BuildLoadGameButton()
    {
        var loadButton = new Button
        {
            Name = "LoadGameButton",
            Text = "LOAD GAME",
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -250,
            OffsetRight = -55,
            OffsetTop = -76,
            OffsetBottom = -30,
            CustomMinimumSize = new Vector2(195, 46),
            FocusMode = Control.FocusModeEnum.All
        };
        loadButton.AddThemeFontSizeOverride("font_size", 16);
        loadButton.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        loadButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("252d42"),
            BorderColor = new Color("8272a7"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        loadButton.Pressed += OpenLoadGameMenu;
        AddChild(loadButton);
    }

    private void BuildExitButton()
    {
        var exitButton = new Button
        {
            Name = "ExitButton",
            Text = "EXIT",
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -250,
            OffsetRight = -55,
            OffsetTop = -130,
            OffsetBottom = -84,
            CustomMinimumSize = new Vector2(195, 46),
            FocusMode = Control.FocusModeEnum.All
        };
        exitButton.AddThemeFontSizeOverride("font_size", 16);
        exitButton.AddThemeColorOverride("font_color", new Color("f5f2ff"));
        exitButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("252d42"),
            BorderColor = new Color("8272a7"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        exitButton.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color("343e59"),
            BorderColor = new Color("8272a7"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7
        });
        exitButton.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
        {
            BgColor = new Color("1c2335"),
            BorderColor = new Color("8272a7"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7
        });
        exitButton.Pressed += ExitGame;
        AddChild(exitButton);
    }

    public void ExitGame()
    {
        GetTree().Quit();
    }

    public void OpenLoadGameMenu()
    {
        var packedScene = GD.Load<PackedScene>("res://Scenes/Menu/SaveLoadMenu.tscn");
        if (packedScene == null)
        {
            GD.PushError("MainMenu: save/load menu scene is missing.");
            return;
        }

        var menu = packedScene.Instantiate<SaveLoadMenu>();
        menu.AllowSaving = false;
        menu.EmbeddedInPauseMenu = false;
        AddChild(menu);
    }

    private async Task FadeScreen(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, FadeTime);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async void OnPlayButtonPressed()
    {
        if (_isStarting) return;
        _isStarting = true;
        DayClock.Instance?.ResetForNewRun();
        SanityManager.Instance?.ResetForNewRun();
        StatusEffectsOverlay.Instance?.ResetForNewRun();
        DailyTestSession.Reset();
        ChallengeProgress.Reset();
        RoomKeyProgression.Instance?.ResetForNewRun();

        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        if (SceneTransition.Instance != null)
        {
            bool loaded = await SceneTransition.Instance.LoadSceneWithLoadingScreen("res://Scenes/Home/Areas/World.tscn");
            if (!loaded)
                _isStarting = false;
        }
        else
        {
            await FadeScreen(1f);
            if (GetTree() != null)
            {
                if (DayClock.Instance != null)
                    DayClock.Instance.Running = true;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GetTree().ChangeSceneToFile("res://Scenes/Home/Areas/World.tscn");
            }
        }
    }

    private void OnSettingsButtonPressed()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
        else
            GetTree().ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
    }

}
