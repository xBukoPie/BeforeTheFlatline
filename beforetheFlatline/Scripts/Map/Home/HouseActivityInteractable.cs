using Godot;
using System;
using System.Threading.Tasks;
using SchoolYardGame;

public partial class HouseActivityInteractable : Area2D
{
    [Export] public string ActivityId { get; set; } = "tv";
    [Export] public string Prompt { get; set; } = "Use";

    private CharacterBody2D _player;
    private ProcessModeEnum _previousPlayerMode = ProcessModeEnum.Inherit;
    private bool _playerFrozen;
    private bool _playerInside;
    private bool _runningActivity;
    private bool _previousTreePaused;
    private bool _previousClockRunning;
    private CanvasLayer _transitionLayer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitoring = true;
        Monitorable = false;

        EnsureInteractAction();
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
        RestorePlayer();

        if (_runningActivity)
            RestoreWorldState();

        if (_transitionLayer != null && GodotObject.IsInstanceValid(_transitionLayer))
            _transitionLayer.QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!_playerInside || _runningActivity)
            return;

        string unavailable = GetUnavailableReason();
        string label = string.IsNullOrWhiteSpace(Prompt) ? "Use" : Prompt.Trim();
        InteractionPromptManager.Instance?.ShowPrompt(
            this,
            string.IsNullOrWhiteSpace(unavailable)
                ? $"[E] {label}"
                : $"[E] {label} (unavailable)",
            58);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || _runningActivity || !@event.IsActionPressed("interact"))
            return;

        if (InteractionPromptManager.Instance != null &&
            !InteractionPromptManager.Instance.IsPrimary(this))
            return;

        GetViewport().SetInputAsHandled();

        string unavailable = GetUnavailableReason();
        if (!string.IsNullOrWhiteSpace(unavailable))
        {
            InteractionPromptManager.Instance?.ShowNotice(this, unavailable, 3f);
            return;
        }

        DayClock clock = DayClock.Instance;
        if (clock == null || !clock.MarkHouseActivityUsed(ActivityId))
        {
            InteractionPromptManager.Instance?.ShowNotice(
                this, "You cannot do that right now.", 2.5f);
            return;
        }

        BeginActivity();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not CharacterBody2D player)
            return;

        _player = player;
        _playerInside = true;
        _runningActivity = false;
        _Process(0);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body != _player)
            return;

        _playerInside = false;
        InteractionPromptManager.Instance?.HidePrompt(this);
        if (!_runningActivity)
            _player = null;
    }

    private string GetUnavailableReason()
    {
        DayClock clock = DayClock.Instance;
        if (clock == null)
            return "The time of day is unavailable right now.";

        string activity = NormalizeActivityId();

        if (!clock.SchoolChallengeStartedToday &&
            clock.Hour >= clock.SchoolOpenHour &&
            clock.Hour < clock.SchoolEntryDeadlineHour &&
            clock.Hour + GetDurationMinutes() / 60f >= clock.SchoolEntryDeadlineHour)
        {
            return "There is not enough time for this before the school attendance deadline. You should head to school first.";
        }

        if (clock.HasUsedHouseActivity(activity))
            return "You have already spent time doing this today.";

        if (!clock.IsHouseActivityRolledAvailable(activity))
        {
            return activity switch
            {
                "tv" => "The television is not working today. Its signal keeps cutting out.",
                "bicycle" => "The bicycle needs repairs. Its chain keeps slipping.",
                "bath" => "The bathroom is unavailable today. The water supply is being checked.",
                _ => "This activity is unavailable today."
            };
        }

        if (activity == "bicycle")
        {
            if (clock.IsNight || clock.Hour + GetDurationMinutes() / 60f >= clock.NightBeginsHour)
                return "It is too dark to ride safely. Come back during daylight.";
        }

        if (activity is not ("tv" or "bicycle" or "bath"))
            return "This activity is not configured.";

        return string.Empty;
    }

    private int GetDurationMinutes() => NormalizeActivityId() switch
    {
        "tv" => 60,
        "bicycle" => 90,
        "bath" => 30,
        _ => 0
    };

    private string NormalizeActivityId() =>
        (ActivityId ?? string.Empty).Trim().ToLowerInvariant();

    private async void BeginActivity()
    {
        if (_runningActivity)
            return;

        _runningActivity = true;
        InteractionPromptManager.Instance?.HidePrompt(this);

        SceneTree tree = GetTree();
        DayClock clock = DayClock.Instance;
        _previousTreePaused = tree.Paused;
        _previousClockRunning = clock?.Running ?? false;

        FreezePlayer();
        if (clock != null)
            clock.Running = false;
        tree.Paused = true;

        string message = string.Empty;
        try
        {
            _transitionLayer = BuildTransitionLayer(out ColorRect black, out Label messageLabel);
            tree.Root.AddChild(_transitionLayer);

            await Fade(black, 1f, 0.85f);
            if (!IsInsideTree())
                return;

            bool lateNight = IsLateNight(clock);
            ApplyActivity(clock, lateNight, out message);
            SaveManager.Instance?.RequestAutoSave();

            messageLabel.Text = message;
            await Fade(messageLabel, 1f, 0.5f);
            await WaitOn(2.4f);
            await FadeBoth(black, messageLabel, 0f, 0.95f);
        }
        catch (Exception ex)
        {
            GD.PushError($"HouseActivityInteractable ({ActivityId}): {ex.Message}");
        }
        finally
        {
            if (_transitionLayer != null && GodotObject.IsInstanceValid(_transitionLayer))
                _transitionLayer.QueueFree();

            RestoreWorldState();
            RestorePlayer();
            _runningActivity = false;

            if (IsInsideTree() && _playerInside)
                _Process(0);
        }
    }

    private static bool IsLateNight(DayClock clock) =>
        clock?.IsNight == true;

    private void ApplyActivity(DayClock clock, bool lateNight, out string message)
    {
        int minutes = GetDurationMinutes();
        clock?.AdvanceMinutes(minutes);

        switch (NormalizeActivityId())
        {
            case "tv":
                message = "You decided to watch and passed some time.";
                if (lateNight)
                {
                    SanityManager.Instance?.ApplyDebuff(
                        "late_night_tv", SanityManager.DebuffType.Dizziness, 8, false);
                    message += " Staying up this late leaves you dizzy and tired.";
                }
                break;

            case "bicycle":
                message = "You went for a ride..";
                break;

            case "bath":
                SanityManager sanity = SanityManager.Instance;
                sanity?.Recover(lateNight ? 7 : 4);
                message = "You took a quick bath.. You feel slightly better.";
                bool clearedDebuff = lateNight && sanity != null &&
                    sanity.ClearRandomActiveDebuff(out _);
                if (clearedDebuff)
                    message += " One lingering effect has eased.";
                break;

            default:
                message = "You spent a little time.";
                break;
        }
    }

    private CanvasLayer BuildTransitionLayer(out ColorRect black, out Label message)
    {
        var layer = new CanvasLayer
        {
            Name = "HouseActivityTransition",
            Layer = 126,
            ProcessMode = ProcessModeEnum.Always
        };

        black = new ColorRect
        {
            Name = "BlackFade",
            Color = Colors.Black,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(black);

        message = new Label
        {
            Name = "ActivityMessage",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        message.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        message.OffsetLeft = 90;
        message.OffsetRight = -90;
        message.AddThemeFontSizeOverride("font_size", 28);
        message.AddThemeColorOverride("font_color", new Color("f1edf5"));
        message.AddThemeColorOverride("font_outline_color", new Color("08070c"));
        message.AddThemeConstantOverride("outline_size", 4);
        black.AddChild(message);

        return layer;
    }

    private async Task Fade(CanvasItem visual, float alpha, double duration)
    {
        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(visual, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task FadeBoth(ColorRect background, Label label, float alpha, double duration)
    {
        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        tween.TweenProperty(background, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(label, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task WaitOn(double seconds)
    {
        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenInterval(seconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void FreezePlayer()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            Node roomManager = GetTree().GetFirstNodeInGroup("room_manager");
            _player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player) || _playerFrozen)
            return;

        _previousPlayerMode = _player.ProcessMode;
        _player.Velocity = Vector2.Zero;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _playerFrozen = true;
    }

    private void RestorePlayer()
    {
        if (!_playerFrozen)
            return;

        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            Node roomManager = GetTree()?.GetFirstNodeInGroup("room_manager");
            _player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");
        }

        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _player.Velocity = Vector2.Zero;
            _player.ProcessMode = _previousPlayerMode == ProcessModeEnum.Disabled
                ? ProcessModeEnum.Inherit
                : _previousPlayerMode;
            _player.SetProcess(true);
            _player.SetPhysicsProcess(true);
        }

        _playerFrozen = false;
    }

    private void RestoreWorldState()
    {
        if (GetTree() == null)
            return;

        GetTree().Paused = _previousTreePaused;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;
    }

    private static void EnsureInteractAction()
    {
        const string action = "interact";
        if (InputMap.HasAction(action))
            return;

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Key.E });
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = JoyButton.A });
    }
}
