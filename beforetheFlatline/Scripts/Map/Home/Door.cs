using Godot;
using SchoolYardGame;

// Doorway trigger. Instance Door.tscn in a room, set TargetRoom / TargetSpawn.
// The player (physics layer 2) must stand in the area and press the "interact" action (E).
public partial class Door : Area2D
{
    [Export(PropertyHint.File, "*.tscn")] public string TargetRoom { get; set; } = "";
    [Export] public string TargetSpawn { get; set; } = "";   // name of a Marker2D under the target room's "Spawns" node
    [Export] public string Prompt { get; set; } = "Enter";
    [Export] public Vector2 Size { get; set; } = new Vector2(32, 12);
    [Export] public bool AutoEnter { get; set; } = false;    // true = no key press needed (e.g. walk down stairs)
    [Export] public bool RequiresSchoolHours { get; set; } = false;
    [Export] public string UnlockRoomId { get; set; } = "";
    [Export] public bool TriggersFirstDayMystery { get; set; } = false;
    [Export] public string LockedPrompt { get; set; } = "Locked. Complete tasks to earn the key.";
    [Export] public string ClosedPrompt { get; set; } = "School is closed. Come back after sunrise.";
    [Export] public float ClosedMessageDuration { get; set; } = 2.5f;

    private const bool DebugLog = true;   // prints door activity to the Output panel; set to false once doors work

    private Label _hint;
    private bool _playerInside;
    private bool _mysteryIntroOpening;
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

        _hint = new Label
        {
            Text = $"[E] {Prompt}",
            Visible = false,
            ZIndex = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(120, 0),
            Position = new Vector2(-60, -Mathf.Min(Size.Y / 2f, 20f) - 18f),
        };
        _hint.AddThemeFontSizeOverride("font_size", 8);
        _hint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hint.AddThemeConstantOverride("outline_size", 3);
        AddChild(_hint);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        if (_closedPromptRemaining <= 0f)
            return;

        _closedPromptRemaining = Mathf.Max(0f, _closedPromptRemaining - (float)delta);
        if (_closedPromptRemaining <= 0f && IsInstanceValid(_hint))
        {
            _hint.Text = $"[E] {Prompt}";
            _hint.Visible = _playerInside;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_playerInside && @event.IsActionPressed("interact"))
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
        _hint.Text = $"[E] {Prompt}";
        _hint.Visible = true;
        if (AutoEnter) Use();
    }

    private void OnBodyExited(Node2D body)
    {
        if (DebugLog) GD.Print($"[Door] {Name}: '{body.Name}' left the trigger");
        _playerInside = false;
        _closedPromptRemaining = 0f;
        _hint.Text = $"[E] {Prompt}";
        _hint.Visible = false;
    }

    // Registers the "interact" action (E key, gamepad A/Cross) if the project doesn't define it yet.
    // Safe to call from every door: it does nothing once the action exists.
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
        if (_hint == null || !IsInstanceValid(_hint))
            return;

        _hint.Text = message;
        _hint.Visible = true;
        _closedPromptRemaining = Mathf.Max(0.5f, ClosedMessageDuration);
    }

    /// <summary>Called after the one-time first-day encounter finishes.</summary>
    public void ContinueAfterMysteryIntro()
    {
        _mysteryIntroOpening = false;
        if (GodotObject.IsInstanceValid(this))
            Use();
    }

    private void Use()
    {
        if (!string.IsNullOrWhiteSpace(UnlockRoomId) &&
            (RoomKeyProgression.Instance == null || !RoomKeyProgression.Instance.IsRoomUnlocked(UnlockRoomId)))
        {
            ShowTemporaryPrompt(LockedPrompt);
            return;
        }

        if (RequiresSchoolHours && (DayClock.Instance == null || !DayClock.Instance.IsSchoolOpen))
        {
            ShowTemporaryPrompt(ClosedPrompt);
            return;
        }

        // Find the RoomManager (grouped as "room_manager")
        var manager = GetTree().GetFirstNodeInGroup("room_manager") as RoomManager;
        if (manager == null)
        {
            GD.PushWarning("Door: no RoomManager (group 'room_manager') found.");
            return;
        }

        // The first time the player enters this classroom door, show the mysterious
        // tutorial/lore encounter first. Completion calls back into this door and reuses
        // the normal classroom overlay path below.
        if (TriggersFirstDayMystery &&
            DayClock.Instance?.ShouldShowFirstDayMysteryEncounter == true)
        {
            if (_mysteryIntroOpening)
                return;

            var introScene = GD.Load<PackedScene>("res://Scenes/School/FirstDayMysteryIntro.tscn");
            if (introScene == null)
            {
                GD.PushError("Door: missing FirstDayMysteryIntro.tscn.");
                return;
            }

            var uiRoot = manager.GetNodeOrNull<Node>("UI");
            if (uiRoot == null)
            {
                GD.PushError("Door: cannot show the first-day mystery because the RoomManager has no UI node.");
                return;
            }

            var intro = introScene.Instantiate<FirstDayMysteryIntro>();
            intro.DoorToContinue = this;
            _mysteryIntroOpening = true;
            uiRoot.AddChild(intro);
            return;
        }

        // Load the target scene
        var packed = GD.Load<PackedScene>(TargetRoom);
        if (packed == null)
        {
            GD.PushWarning($"Door: cannot load target scene '{TargetRoom}'.");
            return;
        }
        var instance = packed.Instantiate();

        // If the target scene is a Room (Node2D), use RoomManager's transition logic.
        if (instance is Node2D && instance is not Control)
        {
            // Classic room transition – respects fades, camera, etc.
            manager.GoTo(TargetRoom, TargetSpawn);
            return;
        }

        // Otherwise, treat it as a UI overlay (Control) and add it to the manager's UI layer.
        var uiRoot = manager.GetNodeOrNull<Node>("UI");

        // Don't stack a second copy of the same overlay (e.g. E pressed again while it is open).
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

        // Ensure any lingering fade overlay is cleared.
        var fade = manager.GetNodeOrNull<ColorRect>("UI/Fade");
        if (fade != null)
            fade.Modulate = new Color(0, 0, 0, 0);

        // No greeting is attached here any more: the overlay scene (e.g. Classroom.tscn)
        // starts its own dialogue event in its _Ready.
    }
}