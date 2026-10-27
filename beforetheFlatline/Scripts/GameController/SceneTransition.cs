using Godot;

/// <summary>
/// Autoloaded scene transition controller. Supports short fades and threaded scene loading
/// behind a progress screen so large gameplay scenes do not freeze the main menu.
/// </summary>
public partial class SceneTransition : CanvasLayer
{
    public static SceneTransition Instance { get; private set; }

    [Export] public float DefaultFadeDuration { get; set; } = 0.38f;
    public bool IsBusy => _busy;

    private ColorRect _fade;
    private Control _loadingOverlay;
    private ProgressBar _loadingProgress;
    private Label _loadingStatus;
    private bool _busy;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        Layer = 127;
        ProcessMode = ProcessModeEnum.Always;

        _fade = new ColorRect
        {
            Name = "SceneFade",
            Color = Colors.Black,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fade);

        BuildLoadingOverlay();
        _loadingOverlay.Visible = false;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public async void ChangeSceneToFile(string path)
    {
        if (_busy || string.IsNullOrWhiteSpace(path))
            return;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushError($"SceneTransition: scene was not found at '{path}'.");
            return;
        }

        _busy = true;
        _fade.Visible = true;
        _fade.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Stop;
        await FadeTo(1f, DefaultFadeDuration);

        Error result = GetTree().ChangeSceneToFile(path);
        if (result != Error.Ok)
        {
            GD.PushError($"SceneTransition: failed to change to '{path}' ({result}).");
            await FadeTo(0f, DefaultFadeDuration);
            _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
            _fade.Visible = false;
            _busy = false;
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await FadeTo(0f, DefaultFadeDuration);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.Visible = false;
        _busy = false;
    }

    /// <summary>
    /// Shows a progress screen while the scene is loaded on worker threads, then swaps scenes
    /// only after the resource is ready. Intended for starting a new run from the title screen.
    /// </summary>
    public async void LoadSceneWithLoadingScreen(string path)
    {
        if (_busy || string.IsNullOrWhiteSpace(path))
            return;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushError($"SceneTransition: scene was not found at '{path}'.");
            return;
        }

        Error request = ResourceLoader.LoadThreadedRequest(path, "", true);
        if (request != Error.Ok)
        {
            GD.PushError($"SceneTransition: could not start loading '{path}' ({request}).");
            return;
        }

        _busy = true;
        _loadingProgress.Value = 0;
        _loadingStatus.Text = "Preparing your world...";
        _loadingOverlay.Modulate = new Color(1f, 1f, 1f, 0f);

        _fade.Visible = true;
        _fade.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Stop;
        await FadeTo(1f, 0.28f);

        _loadingOverlay.Visible = true;
        var showLoading = CreateTween();
        showLoading.TweenProperty(_loadingOverlay, "modulate:a", 1f, 0.22f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(showLoading, Tween.SignalName.Finished);
        _fade.Visible = false;

        ulong loadingStartedAt = Time.GetTicksMsec();
        ResourceLoader.ThreadLoadStatus status;
        while (true)
        {
            var progress = new Godot.Collections.Array();
            status = ResourceLoader.LoadThreadedGetStatus(path, progress);

            if (progress.Count > 0)
            {
                float percent = Mathf.Clamp((float)progress[0] * 100f, 0f, 100f);
                _loadingProgress.Value = percent;
                _loadingStatus.Text = $"Loading your world... {Mathf.RoundToInt(percent)}%";
            }

            if (status == ResourceLoader.ThreadLoadStatus.Failed ||
                status == ResourceLoader.ThreadLoadStatus.InvalidResource)
            {
                GD.PushError($"SceneTransition: threaded loading failed for '{path}' ({status}).");
                await HideLoadingScreen();
                _busy = false;
                return;
            }

            // Prevent a flash when the scene is already cached or loads nearly instantly.
            bool minimumDisplayTimePassed = Time.GetTicksMsec() - loadingStartedAt >= 650;
            if (status == ResourceLoader.ThreadLoadStatus.Loaded && minimumDisplayTimePassed)
                break;

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var packedScene = ResourceLoader.LoadThreadedGet(path) as PackedScene;
        if (packedScene == null)
        {
            GD.PushError($"SceneTransition: '{path}' did not load as a PackedScene.");
            await HideLoadingScreen();
            _busy = false;
            return;
        }

        _loadingProgress.Value = 100;
        _loadingStatus.Text = "Entering the world...";
        Error changeResult = GetTree().ChangeSceneToPacked(packedScene);
        if (changeResult != Error.Ok)
        {
            GD.PushError($"SceneTransition: failed to activate '{path}' ({changeResult}).");
            await HideLoadingScreen();
            _busy = false;
            return;
        }

        // Keep the loading screen up while the new scene's _Ready methods initialize.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await HideLoadingScreen();
        _busy = false;
    }

    private void BuildLoadingOverlay()
    {
        _loadingOverlay = new Control
        {
            Name = "LoadingScreen",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always
        };
        _loadingOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_loadingOverlay);

        var background = new ColorRect
        {
            Name = "Background",
            Color = new Color("080b14"),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _loadingOverlay.AddChild(background);

        var center = new CenterContainer
        {
            Name = "CenterContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _loadingOverlay.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "LoadingPanel",
            CustomMinimumSize = new Vector2(560, 220),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151b2b"),
            BorderColor = new Color("66598a"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 38,
            ContentMarginRight = 38,
            ContentMarginTop = 34,
            ContentMarginBottom = 34
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 16);
        panel.AddChild(stack);

        var title = new Label
        {
            Text = "BEFORE THE FLATLINE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("d8c6ff"));
        stack.AddChild(title);

        _loadingStatus = new Label
        {
            Text = "Preparing your world...",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _loadingStatus.AddThemeFontSizeOverride("font_size", 14);
        _loadingStatus.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(_loadingStatus);

        _loadingProgress = new ProgressBar
        {
            Name = "LoadingProgress",
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 18),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _loadingProgress.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color("292336"),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        _loadingProgress.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color("9d83d4"),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        stack.AddChild(_loadingProgress);
    }

    private async System.Threading.Tasks.Task HideLoadingScreen()
    {
        var tween = CreateTween();
        tween.TweenProperty(_loadingOverlay, "modulate:a", 0f, 0.28f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        _loadingOverlay.Visible = false;
        _loadingOverlay.Modulate = Colors.White;
        _fade.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.Visible = false;
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha, float duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
