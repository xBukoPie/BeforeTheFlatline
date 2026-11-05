using Godot;
using SchoolYardGame;

public partial class Door : Area2D
{
    [Export(PropertyHint.File, "*.tscn")] public string TargetRoom { get; set; } = "";
    [Export] public string TargetSpawn { get; set; } = "";   // name of a Marker2D under the target room's "Spawns" node
    [Export] public string Prompt { get; set; } = "Enter";
    [Export] public Vector2 Size { get; set; } = new Vector2(32, 12);
    [Export] public bool AutoEnter { get; set; } = false;    // true = no key press needed (e.g. walk down stairs)
    [Export] public bool RequiresSchoolHours { get; set; } = false;
    [Export] public bool MarksSchoolAttendance { get; set; } = false;
    [Export] public bool WrongRoom { get; set; } = false;
    [Export] public bool ShowLoadingScreen { get; set; } = false;
    [Export] public bool UseSchoolTravelLoadingScreen { get; set; } = false;
    [Export] public string LoadingMessage { get; set; } = "Heading to school...";
    [Export] public string UnlockRoomId { get; set; } = "";
    [Export] public string LockedPrompt { get; set; } = "Locked. Score at least 30 points, then find the key in the Yard.";
    [Export] public string ClosedPrompt { get; set; } = "School is closed. Come back after sunrise.";
    [Export] public float ClosedMessageDuration { get; set; } = 2.5f;

    private const bool DebugLog = true;   // prints door activity to the Output panel; set to false once doors work

    private bool _playerInside;
    private bool _wrongRoomFeedbackRunning;
    private float _closedPromptRemaining;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;      // Player lives on layer 2
        Monitorable = false;

        EnsureInteractAction();

        if (GetNodeOrNull<CollisionShape2D>("Shape") == null)
        {
            AddChild(new CollisionShape2D
            {
                Name = "Shape",
                Shape = new RectangleShape2D { Size = Size }
            });
        }

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
    }

    public override void _Process(double delta)
    {
        if (_closedPromptRemaining <= 0f)
            return;

        _closedPromptRemaining = Mathf.Max(0f, _closedPromptRemaining - (float)delta);
        if (_closedPromptRemaining <= 0f && _playerInside && !AutoEnter)
            InteractionPromptManager.Instance?.ShowPrompt(this, $"[E] {Prompt}", 30);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_playerInside && !AutoEnter && !_wrongRoomFeedbackRunning &&
            (InteractionPromptManager.Instance == null || InteractionPromptManager.Instance.IsPrimary(this)) &&
            @event.IsActionPressed("interact"))
        {
            if (DebugLog) GD.Print($"[Door] {Name}: interact pressed -> {TargetRoom} @ {TargetSpawn}");
            GetViewport().SetInputAsHandled();
            Use();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (DebugLog) GD.Print($"[Door] {Name}: '{body.Name}' entered the trigger");
        _playerInside = true;
        _closedPromptRemaining = 0f;
        if (AutoEnter)
            Use();
        else
            InteractionPromptManager.Instance?.ShowPrompt(this, $"[E] {Prompt}", 30);
    }

    private void OnBodyExited(Node2D body)
    {
        if (DebugLog) GD.Print($"[Door] {Name}: '{body.Name}' left the trigger");
        _playerInside = false;
        _closedPromptRemaining = 0f;
        InteractionPromptManager.Instance?.HidePrompt(this);
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

    private void ShowTemporaryPrompt(string message)
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
        InteractionPromptManager.Instance?.ShowNotice(this, message, ClosedMessageDuration);
        _closedPromptRemaining = Mathf.Max(0.5f, ClosedMessageDuration);
    }

    private void Use()
    {
        if (!string.IsNullOrWhiteSpace(UnlockRoomId) &&
            (RoomKeyProgression.Instance == null || !RoomKeyProgression.Instance.IsRoomUnlocked(UnlockRoomId)))
        {
            AudioManager.Instance?.PlayLockedDoor();
            ShowTemporaryPrompt(LockedPrompt);
            return;
        }

        if (RequiresSchoolHours && (DayClock.Instance == null || !DayClock.Instance.IsSchoolOpen))
        {
            ShowTemporaryPrompt(ClosedPrompt);
            return;
        }

        if (WrongRoom || Name.ToString().StartsWith("wrongRoom", StringComparison.OrdinalIgnoreCase))
        {
            ShowWrongRoomFeedback();
            return;
        }

        var manager = GetTree().GetFirstNodeInGroup("room_manager") as RoomManager;
        if (manager == null)
        {
            GD.PushWarning("Door: no RoomManager (group 'room_manager') found.");
            return;
        }

        var packed = GD.Load<PackedScene>(TargetRoom);
        if (packed == null)
        {
            GD.PushWarning($"Door: cannot load target scene '{TargetRoom}'.");
            return;
        }
        var instance = packed.Instantiate();

        if (MarksSchoolAttendance && DayClock.Instance != null)
        {
            DayClock.Instance.MarkSchoolChallengeStartedToday();
            SaveManager.Instance?.RequestAutoSave();
        }

        if (instance is Node2D && instance is not Control)
        {
            instance.QueueFree();
            manager.GoTo(TargetRoom, TargetSpawn, ShowLoadingScreen, LoadingMessage, UseSchoolTravelLoadingScreen);
            return;
        }

        var uiRoot = manager.GetNodeOrNull<Node>("UI");

        foreach (var child in (uiRoot ?? manager).GetChildren())
        {
            if (child.SceneFilePath == instance.SceneFilePath)
            {
                instance.QueueFree();
                return;
            }
        }

        if (uiRoot != null)
            uiRoot.AddChild(instance);
        else
            manager.AddChild(instance); // fallback

        var fade = manager.GetNodeOrNull<ColorRect>("UI/Fade");
        if (fade != null)
            fade.Modulate = new Color(0, 0, 0, 0);

    }

    private async void ShowWrongRoomFeedback()
    {
        if (_wrongRoomFeedbackRunning)
            return;

        _wrongRoomFeedbackRunning = true;
        InteractionPromptManager.Instance?.HidePrompt(this);

        SceneTree tree = GetTree();
        DayClock clock = DayClock.Instance;
        bool previousClockRunning = clock?.Running ?? false;

        Node roomManager = tree.GetFirstNodeInGroup("room_manager");
        CharacterBody2D player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");
        ProcessModeEnum previousPlayerMode = player?.ProcessMode ?? ProcessModeEnum.Inherit;

        int attempts = clock?.RegisterWrongRoomAttempt() ?? 1;
        SanityManager.Instance?.LoseSanity(1);

        string message = "That was the wrong room... -1 Sanity due to Humiliation.";
        if (attempts >= 2)
            message += "\nMaybe you should try the stairs.";

        CanvasLayer layer = new()
        {
            Name = "WrongRoomTransition",
            Layer = 126,
            ProcessMode = ProcessModeEnum.Always
        };

        var black = new ColorRect
        {
            Name = "BlackFade",
            Color = Colors.Black,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(black);

        var label = new Label
        {
            Name = "WrongRoomMessage",
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        label.OffsetLeft = 100;
        label.OffsetRight = -100;
        label.AddThemeFontSizeOverride("font_size", 27);
        label.AddThemeColorOverride("font_color", new Color("f1edf5"));
        label.AddThemeColorOverride("font_outline_color", new Color("08070c"));
        label.AddThemeConstantOverride("outline_size", 4);
        black.AddChild(label);

        tree.Root.AddChild(layer);

        if (clock != null)
            clock.Running = false;
        if (player != null && GodotObject.IsInstanceValid(player))
        {
            player.Velocity = Vector2.Zero;
            player.ProcessMode = ProcessModeEnum.Disabled;
        }

        try
        {
            SaveManager.Instance?.RequestAutoSave();

            Tween fadeIn = layer.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
            fadeIn.TweenProperty(black, "modulate:a", 1f, 0.62f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            fadeIn.TweenProperty(label, "modulate:a", 1f, 0.62f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            await ToSignal(fadeIn, Tween.SignalName.Finished);

            Tween hold = layer.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
            hold.TweenInterval(2.1f);
            await ToSignal(hold, Tween.SignalName.Finished);

            Tween fadeOut = layer.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
            fadeOut.TweenProperty(black, "modulate:a", 0f, 0.82f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            fadeOut.TweenProperty(label, "modulate:a", 0f, 0.5f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            await ToSignal(fadeOut, Tween.SignalName.Finished);
        }
        catch (System.Exception ex)
        {
            GD.PushError($"Door '{Name}': wrong-room feedback failed: {ex.Message}");
        }
        finally
        {
            if (layer != null && GodotObject.IsInstanceValid(layer))
                layer.QueueFree();

            if (clock != null && GodotObject.IsInstanceValid(clock))
                clock.Running = previousClockRunning;

            if (player != null && GodotObject.IsInstanceValid(player))
            {
                player.Velocity = Vector2.Zero;
                player.ProcessMode = previousPlayerMode == ProcessModeEnum.Disabled
                    ? ProcessModeEnum.Inherit
                    : previousPlayerMode;
                player.SetProcess(true);
                player.SetPhysicsProcess(true);
            }

            _wrongRoomFeedbackRunning = false;
            if (IsInsideTree() && _playerInside)
                InteractionPromptManager.Instance?.ShowPrompt(this, $"[E] {Prompt}", 30);
        }
    }
}
