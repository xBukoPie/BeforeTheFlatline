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

    private const bool DebugLog = true;   // prints door activity to the Output panel; set to false once doors work

    private Label _hint;
    private bool _playerInside;

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
        _hint.Visible = true;
        if (AutoEnter) Use();
    }

    private void OnBodyExited(Node2D body)
    {
        if (DebugLog) GD.Print($"[Door] {Name}: '{body.Name}' left the trigger");
        _playerInside = false;
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

    private void Use()
    {
        // Find the RoomManager (grouped as "room_manager")
        var manager = GetTree().GetFirstNodeInGroup("room_manager") as RoomManager;
        if (manager == null)
        {
            GD.PushWarning("Door: no RoomManager (group 'room_manager') found.");
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
        if (uiRoot != null)
            uiRoot.AddChild(instance);
        else
            manager.AddChild(instance); // fallback

        // Ensure any lingering fade overlay is cleared.
        var fade = manager.GetNodeOrNull<ColorRect>("UI/Fade");
        if (fade != null)
            fade.Modulate = new Color(0, 0, 0, 0);

        // After a short delay, attach the greeting script to the UI.
        var timer = GetTree().CreateTimer(0.1f);
        timer.Connect("timeout", Callable.From(() =>
        {
            var greeting = new SchoolGreeting();
            instance.AddChild(greeting);
        }));
    }
}