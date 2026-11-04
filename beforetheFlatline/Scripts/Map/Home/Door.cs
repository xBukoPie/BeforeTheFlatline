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
    [Export] public bool ShowLoadingScreen { get; set; } = false;
    [Export] public bool UseSchoolTravelLoadingScreen { get; set; } = false;
    [Export] public string LoadingMessage { get; set; } = "Heading to school...";
    [Export] public string UnlockRoomId { get; set; } = "";
    [Export] public string LockedPrompt { get; set; } = "Locked. Score at least 30 points, then find the key in the Yard.";
    [Export] public string ClosedPrompt { get; set; } = "School is closed. Come back after sunrise.";
    [Export] public float ClosedMessageDuration { get; set; } = 2.5f;

    private const bool DebugLog = true;   // prints door activity to the Output panel; set to false once doors work

    private bool _playerInside;
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
        if (_playerInside && !AutoEnter &&
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
}
