using Godot;
using System;
using System.Threading.Tasks;

public partial class SceneTransition : CanvasLayer
{
    public static SceneTransition Instance { get; private set; }

    [Export] public float DefaultFadeDuration { get; set; } = 0.38f;
    public bool IsBusy => _busy;

    private ColorRect _fade;
    private Control _loadingOverlay;
    private ProgressBar _loadingProgress;
    private Label _loadingStatus;
    private Control _schoolTravelOverlay;
    private ProgressBar _schoolTravelProgress;
    private Label _schoolTravelStatus;
    private Label _schoolTravelDayLabel;
    private Control _activeLoadingOverlay;
    private ProgressBar _activeLoadingProgress;
    private Label _activeLoadingStatus;
    private bool _busy;
    private string _queuedCriticalScenePath;

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
        BuildSchoolTravelOverlay();
        _loadingOverlay.Visible = false;
        _schoolTravelOverlay.Visible = false;
        SelectLoadingOverlay(false, "Preparing your world...");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (_busy || string.IsNullOrWhiteSpace(_queuedCriticalScenePath))
            return;

        string criticalPath = _queuedCriticalScenePath;
        _queuedCriticalScenePath = null;
        ChangeSceneToFile(criticalPath);
    }

    // Critical endings must not be discarded just because a room transition is busy.
    public void RequestCriticalSceneChange(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
        {
            GD.PushError($"SceneTransition: critical scene was not found at '{path}'.");
            return;
        }

        _queuedCriticalScenePath = path;
        if (_busy)
            return;

        string criticalPath = _queuedCriticalScenePath;
        _queuedCriticalScenePath = null;
        ChangeSceneToFile(criticalPath);
    }

    public async void ChangeSceneToFile(string path, float fadeDurationOverride = 0f)
    {
        if (_busy || !string.IsNullOrWhiteSpace(_queuedCriticalScenePath) ||
            string.IsNullOrWhiteSpace(path))
            return;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushError($"SceneTransition: scene was not found at '{path}'.");
            return;
        }

        _busy = true;
        float fadeDuration = fadeDurationOverride > 0f
            ? fadeDurationOverride
            : DefaultFadeDuration;
        _fade.Visible = true;
        _fade.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Stop;
        await FadeTo(1f, fadeDuration);

        Error result = GetTree().ChangeSceneToFile(path);
        if (result != Error.Ok)
        {
            GD.PushError($"SceneTransition: failed to change to '{path}' ({result}).");
            await FadeTo(0f, fadeDuration);
            _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
            _fade.Visible = false;
            _busy = false;
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await FadeTo(0f, fadeDuration);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.Visible = false;
        _busy = false;
    }

    // Loads a scene while showing progress..
    public async Task<bool> LoadSceneWithLoadingScreen(string path)
    {
        if (_busy || !string.IsNullOrWhiteSpace(_queuedCriticalScenePath) ||
            string.IsNullOrWhiteSpace(path))
            return false;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushError($"SceneTransition: scene was not found at '{path}'.");
            return false;
        }

        Error request = ResourceLoader.LoadThreadedRequest(path, "", true);
        if (request != Error.Ok)
        {
            GD.PushError($"SceneTransition: could not start loading '{path}' ({request}).");
            return false;
        }

        SelectLoadingOverlay(false, "Preparing your world...");
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
                return false;
            }

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
            return false;
        }

        _loadingProgress.Value = 100;
        _loadingStatus.Text = "Entering the world...";
        Error changeResult = GetTree().ChangeSceneToPacked(packedScene);
        if (changeResult != Error.Ok)
        {
            GD.PushError($"SceneTransition: failed to activate '{path}' ({changeResult}).");
            await HideLoadingScreen();
            _busy = false;
            return false;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await HideLoadingScreen();
        _busy = false;
        return true;
    }

    public async Task<bool> RunWithLoadingScreen(
        Action transitionAction,
        string statusMessage = "Heading to school...",
        double minimumDisplaySeconds = 1.35,
        bool useSchoolTravelLoadingScreen = false)
    {
        if (_busy || !string.IsNullOrWhiteSpace(_queuedCriticalScenePath) ||
            transitionAction == null)
            return false;

        _busy = true;
        try
        {
            SelectLoadingOverlay(useSchoolTravelLoadingScreen, statusMessage);
            _activeLoadingProgress.Value = 0;
            _activeLoadingStatus.Text = string.IsNullOrWhiteSpace(statusMessage)
                ? "On your way..."
                : statusMessage;
            _activeLoadingOverlay.Visible = false;
            _activeLoadingOverlay.Modulate = new Color(1f, 1f, 1f, 0f);

            _fade.Visible = true;
            _fade.Modulate = new Color(1f, 1f, 1f, 0f);
            _fade.MouseFilter = Control.MouseFilterEnum.Stop;
            await FadeTo(1f, 0.28f);

            _activeLoadingOverlay.Visible = true;
            Tween showLoading = CreateTween();
            showLoading.TweenProperty(_activeLoadingOverlay, "modulate:a", 1f, 0.24f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            await ToSignal(showLoading, Tween.SignalName.Finished);
            _fade.Visible = false;

            ulong startedAt = Time.GetTicksMsec();
            ulong minimumMs = (ulong)(Math.Max(0.2, minimumDisplaySeconds) * 1000.0);
            while (true)
            {
                ulong elapsedMs = Time.GetTicksMsec() - startedAt;
                float progress = Mathf.Clamp((float)elapsedMs / minimumMs, 0f, 1f);
                _activeLoadingProgress.Value = progress * 100f;
                if (elapsedMs >= minimumMs)
                    break;

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            _activeLoadingProgress.Value = 100;
            transitionAction.Invoke();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await HideLoadingScreen();
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"SceneTransition: room loading transition failed: {ex.Message}");
            if (_activeLoadingOverlay != null && _activeLoadingOverlay.Visible)
                await HideLoadingScreen();
            return false;
        }
        finally
        {
            _busy = false;
        }
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

    private void SelectLoadingOverlay(bool schoolTravel, string statusMessage)
    {
        _activeLoadingOverlay = schoolTravel ? _schoolTravelOverlay : _loadingOverlay;
        _activeLoadingProgress = schoolTravel ? _schoolTravelProgress : _loadingProgress;
        _activeLoadingStatus = schoolTravel ? _schoolTravelStatus : _loadingStatus;

        if (schoolTravel && _schoolTravelDayLabel != null)
        {
            float hour = global::SchoolYardGame.DayClock.Instance?.Hour ?? 8f;
            int wholeHour = Mathf.FloorToInt(hour);
            int minute = Mathf.FloorToInt((hour - wholeHour) * 60f);
            string meridiem = wholeHour >= 12 ? "PM" : "AM";
            int displayHour = wholeHour % 12;
            if (displayHour == 0)
                displayHour = 12;

            int day = global::SchoolYardGame.DayClock.Instance?.DayNumber ?? 1;
            _schoolTravelDayLabel.Text = $"DAY {day:00}  •  {displayHour:00}:{minute:00} {meridiem}";
        }

        if (_activeLoadingStatus != null)
            _activeLoadingStatus.Text = string.IsNullOrWhiteSpace(statusMessage)
                ? (schoolTravel ? "The walk to school..." : "On your way...")
                : statusMessage;
    }

    private void BuildSchoolTravelOverlay()
    {
        _schoolTravelOverlay = new Control
        {
            Name = "SchoolTravelLoadingScreen",
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always
        };
        _schoolTravelOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_schoolTravelOverlay);

        var background = new ColorRect
        {
            Name = "CommuteBackground",
            Color = new Color("182c3d"),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _schoolTravelOverlay.AddChild(background);

        var morningSky = new ColorRect
        {
            Name = "MorningSky",
            Color = new Color(0.68f, 0.58f, 0.46f, 0.22f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        morningSky.AnchorRight = 1f;
        morningSky.AnchorBottom = 0.72f;
        _schoolTravelOverlay.AddChild(morningSky);

        var skyline = new Control
        {
            Name = "NeighborhoodSilhouette",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        skyline.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _schoolTravelOverlay.AddChild(skyline);

        int[] buildingWidths = { 92, 126, 76, 118, 88, 136, 94, 110, 84, 124 };
        int[] buildingHeights = { 145, 190, 118, 168, 132, 205, 150, 178, 124, 164 };
        for (int i = 0; i < buildingWidths.Length; i++)
        {
            var building = new ColorRect
            {
                Name = $"Building{i + 1}",
                Color = i % 2 == 0 ? new Color("24394a") : new Color("203345"),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(i * 145f - 24f, 550f - buildingHeights[i]),
                Size = new Vector2(buildingWidths[i], buildingHeights[i])
            };
            skyline.AddChild(building);
        }

        var road = new ColorRect
        {
            Name = "Street",
            Color = new Color("111d29"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        road.AnchorLeft = 0f;
        road.AnchorRight = 1f;
        road.AnchorTop = 0.78f;
        road.AnchorBottom = 1f;
        _schoolTravelOverlay.AddChild(road);

        var laneLine = new ColorRect
        {
            Name = "StreetLine",
            Color = new Color(0.82f, 0.67f, 0.45f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        laneLine.AnchorLeft = 0.04f;
        laneLine.AnchorRight = 0.96f;
        laneLine.AnchorTop = 0.88f;
        laneLine.AnchorBottom = 0.89f;
        _schoolTravelOverlay.AddChild(laneLine);

        var center = new CenterContainer
        {
            Name = "CommuteCardCenter",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _schoolTravelOverlay.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "CommuteCard",
            CustomMinimumSize = new Vector2(760, 410),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.045f, 0.075f, 0.105f, 0.97f),
            BorderColor = new Color("c79a63"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 38,
            ContentMarginRight = 38,
            ContentMarginTop = 30,
            ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer
        {
            Name = "CommuteContent",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 14);
        panel.AddChild(stack);

        _schoolTravelDayLabel = new Label
        {
            Text = "DAY 01  •  08:00 AM",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _schoolTravelDayLabel.AddThemeFontSizeOverride("font_size", 14);
        _schoolTravelDayLabel.AddThemeColorOverride("font_color", new Color("d7b98b"));
        stack.AddChild(_schoolTravelDayLabel);

        var title = new Label
        {
            Text = "THE WALK TO SCHOOL",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 38);
        title.AddThemeColorOverride("font_color", new Color("f1dfc3"));
        title.AddThemeColorOverride("font_outline_color", new Color("0b131c"));
        title.AddThemeConstantOverride("outline_size", 3);
        stack.AddChild(title);

        _schoolTravelStatus = new Label
        {
            Text = "The walk to school...",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _schoolTravelStatus.CustomMinimumSize = new Vector2(0, 34);
        _schoolTravelStatus.AddThemeFontSizeOverride("font_size", 18);
        _schoolTravelStatus.AddThemeColorOverride("font_color", new Color("c7d0d5"));
        stack.AddChild(_schoolTravelStatus);

        var route = new HBoxContainer
        {
            Name = "RouteStops",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        route.AddThemeConstantOverride("separation", 10);
        route.AddChild(CreateTravelRouteStop("01", "HOME"));
        route.AddChild(CreateTravelRouteConnector());
        route.AddChild(CreateTravelRouteStop("02", "NEIGHBORHOOD"));
        route.AddChild(CreateTravelRouteConnector());
        route.AddChild(CreateTravelRouteStop("03", "SCHOOL"));
        stack.AddChild(route);

        _schoolTravelProgress = new ProgressBar
        {
            Name = "CommuteProgress",
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 16),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _schoolTravelProgress.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color("253849"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });
        _schoolTravelProgress.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color("d5a46b"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        });
        stack.AddChild(_schoolTravelProgress);

        var footer = new Label
        {
            Text = "A MOMENT BETWEEN HOME AND THE CLASSROOM",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        footer.AddThemeFontSizeOverride("font_size", 12);
        footer.AddThemeColorOverride("font_color", new Color("8798a5"));
        stack.AddChild(footer);
    }

    private static Control CreateTravelRouteStop(string number, string place)
    {
        var stop = new PanelContainer
        {
            CustomMinimumSize = new Vector2(142, 66),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        stop.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("1b3041"),
            BorderColor = new Color("536b7a"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        });

        var content = new VBoxContainer();
        content.AddChild(new Label
        {
            Text = number,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.AddChild(new Label
        {
            Text = place,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stop.AddChild(content);
        return stop;
    }

    private static Control CreateTravelRouteConnector()
    {
        var connector = new Label
        {
            Text = "→",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(30, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        connector.AddThemeFontSizeOverride("font_size", 24);
        connector.AddThemeColorOverride("font_color", new Color("d5a46b"));
        return connector;
    }

    private async System.Threading.Tasks.Task HideLoadingScreen()
    {
        Control overlay = _activeLoadingOverlay ?? _loadingOverlay;
        var tween = CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 0f, 0.28f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        overlay.Visible = false;
        overlay.Modulate = Colors.White;
        _fade.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.Visible = false;

        _activeLoadingOverlay = _loadingOverlay;
        _activeLoadingProgress = _loadingProgress;
        _activeLoadingStatus = _loadingStatus;
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
