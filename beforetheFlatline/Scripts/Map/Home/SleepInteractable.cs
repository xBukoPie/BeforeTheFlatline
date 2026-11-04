using Godot;
using System.Threading.Tasks;
using SchoolYardGame;

public partial class SleepInteractable : Area2D
{
    private CharacterBody2D _player;
    private ProcessModeEnum _previousPlayerMode = ProcessModeEnum.Inherit;
    private bool _playerFrozen;
    private bool _previousClockRunning;
    private bool _playerInside;
    private bool _sleeping;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitoring = true;
        Monitorable = false;

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
        RestorePlayer();
    }

    public override void _Process(double delta)
    {
        if (!_playerInside || _sleeping)
            return;

        if (DayClock.Instance?.CanSleep == true)
            InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Sleep until morning", 55);
        else
            InteractionPromptManager.Instance?.HidePrompt(this);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || _sleeping || !@event.IsActionPressed("interact"))
            return;

        if (InteractionPromptManager.Instance != null &&
            !InteractionPromptManager.Instance.IsPrimary(this))
            return;

        GetViewport().SetInputAsHandled();

        if (DayClock.Instance?.CanSleep != true)
        {
            InteractionPromptManager.Instance?.ShowNotice(this,
                "You should finish the day before going to sleep.", 2.5f);
            return;
        }

        BeginSleep();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not CharacterBody2D player)
            return;

        _player = player;
        _playerInside = true;
        if (DayClock.Instance?.CanSleep == true)
            InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Sleep until morning", 55);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body != _player)
            return;

        _playerInside = false;
        InteractionPromptManager.Instance?.HidePrompt(this);
        // Keep the player reference until the sleep transition has restored control.
        if (!_sleeping)
            _player = null;
    }

    private async void BeginSleep()
    {
        if (_sleeping)
            return;

        _sleeping = true;
        InteractionPromptManager.Instance?.HidePrompt(this);
        _previousClockRunning = DayClock.Instance?.Running ?? false;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        FreezePlayer();

        CanvasLayer layer = null;
        bool slept = false;
        try
        {
            layer = new CanvasLayer
            {
                Name = "SleepTransition",
                Layer = 126,
                ProcessMode = ProcessModeEnum.Always
            };
            GetTree().Root.AddChild(layer);

            var black = new ColorRect
            {
                Name = "SleepFade",
                Color = Colors.Black,
                Modulate = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(black);

            var message = new Label
            {
                Text = "You let the day slip away...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            message.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            message.AddThemeFontSizeOverride("font_size", 26);
            message.AddThemeColorOverride("font_color", new Color("eee8f7"));
            black.AddChild(message);

            await FadeTo(black, message, 1f, 1.2);
            if (!IsInsideTree())
                return;

            slept = DayClock.Instance?.SleepUntilSchoolMorning() == true;
            if (slept)
            {
                if (DayClock.Instance != null)
                    DayClock.Instance.Running = false;
                message.Text = "A new morning begins. It's time to get ready for school.";
                SaveManager.Instance?.RequestAutoSave();
            }
            else
            {
                message.Text = "You can't sleep just yet.";
            }

            await WaitOn(layer, 0.85);
            await FadeTo(black, message, 0f, 0.9);
        }
        catch (System.Exception ex)
        {
            GD.PushError($"SleepInteractable: sleep transition failed: {ex.Message}");
        }
        finally
        {
            if (layer != null && GodotObject.IsInstanceValid(layer))
                layer.QueueFree();

            if (DayClock.Instance != null)
                DayClock.Instance.Running = slept || _previousClockRunning;

            RestorePlayer();
            _sleeping = false;

            if (IsInsideTree() && _playerInside && DayClock.Instance?.CanSleep == true)
                InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Sleep until morning", 55);
        }
    }

    private async Task FadeTo(ColorRect background, Label label, float alpha, double duration)
    {
        var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        tween.TweenProperty(background, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(label, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task WaitOn(Node owner, double seconds)
    {
        var tween = owner.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
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
}
