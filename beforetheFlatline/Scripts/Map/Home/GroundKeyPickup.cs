using Godot;
using SchoolYardGame;

public partial class GroundKeyPickup : Area2D
{
    private Sprite2D _sprite;
    private Label _prompt;
    private bool _playerInside;
    private bool _collected;
    private float _feedbackRemaining;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;
        EnsureInteractAction();

        _sprite = GetNode<Sprite2D>("Sprite2D");
        _prompt = GetNode<Label>("Prompt");
        _prompt.Visible = false;

        var promptLayer = new CanvasLayer
        {
            Name = "InteractionPromptLayer",
            Layer = 105
        };
        AddChild(promptLayer);
        _prompt.GetParent()?.RemoveChild(_prompt);
        promptLayer.AddChild(_prompt);
        _prompt.AnchorLeft = 0.5f;
        _prompt.AnchorRight = 0.5f;
        _prompt.AnchorTop = 1f;
        _prompt.AnchorBottom = 1f;
        _prompt.OffsetLeft = -280;
        _prompt.OffsetRight = 280;
        _prompt.OffsetTop = -94;
        _prompt.OffsetBottom = -50;
        _prompt.CustomMinimumSize = new Vector2(560, 44);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.VerticalAlignment = VerticalAlignment.Center;
        _prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _prompt.MouseFilter = Control.MouseFilterEnum.Ignore;
        _prompt.AddThemeFontSizeOverride("font_size", 18);
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 4);

        if (RoomKeyProgression.Instance == null || !RoomKeyProgression.Instance.HasPendingKey)
        {
            QueueFree();
            return;
        }

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_collected || !_playerInside || !@event.IsActionPressed("interact"))
            return;

        GetViewport().SetInputAsHandled();

        string roomName = RoomKeyProgression.Instance?.CollectPendingKey() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            _prompt.Text = "The key is no longer here.";
            _prompt.Visible = true;
            _playerInside = false;
            return;
        }

        _collected = true;
        Monitoring = false;
        _sprite.Visible = false;
        _prompt.Text = $"KEY ACQUIRED: {roomName} unlocked!";
        _prompt.Visible = true;
        _feedbackRemaining = 2.5f;

        SaveManager.Instance?.RequestAutoSave();
    }

    public override void _Process(double delta)
    {
        if (!_collected)
            return;

        _feedbackRemaining -= (float)delta;
        if (_feedbackRemaining <= 0f)
            QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_collected)
            return;

        _playerInside = true;
        _prompt.Text = "[E] Pick up key";
        _prompt.Visible = true;
    }

    private void OnBodyExited(Node2D body)
    {
        _playerInside = false;
        if (!_collected)
            _prompt.Visible = false;
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
