using Godot;
using SchoolYardGame;
using System.Threading.Tasks;

public partial class MainMenu : Control
{
    [Export] public float FadeTime { get; set; } = 0.25f;
    private ColorRect _fade;
    private bool _isStarting = false;

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;
        _fade = GetNode<ColorRect>("UI/Fade");
        BuildLoadGameButton();
        BuildExitButton();
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

    private void ExitGame()
    {
        GetTree().Quit();
    }

    private void OpenLoadGameMenu()
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
            bool loaded = await SceneTransition.Instance.LoadSceneWithLoadingScreen("res://Scenes/Home/Rooms/World.tscn");
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
                GetTree().ChangeSceneToFile("res://Scenes/Home/Rooms/World.tscn");
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

    private void OnSupportButtonPressed()
    {
        var dialog = new AcceptDialog
        {
            Title = "Support & Controls",
            DialogText = "BEFORE THE FLATLINE\n\nMove: WASD or arrow keys\nInteract: E\nAdvance time outside school: T\n\nUse Settings to adjust master volume, brightness, and fullscreen."
        };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.Canceled += () => dialog.QueueFree();

        dialog.PopupCentered();
    }
}
