using Godot;
using SchoolYardGame;
using System;

public partial class GroundKeyPickup : Area2D
{
    [Export] public string KeyRoomId { get; set; } = "Kitchen";

    private Sprite2D _sprite;
    private Sprite2D _glow;
    private Label _prompt;
    private bool _playerInside;
    private bool _collected;
    private float _feedbackRemaining;
    private double _elapsed;
    private bool _wasAvailable;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitorable = false;
        Monitoring = false;
        EnsureInteractAction();

        _sprite = GetNode<Sprite2D>("Sprite2D");
        _prompt = GetNode<Label>("Prompt");
        _prompt.Visible = false;

        string texturePath = GetKeyTexturePath(KeyRoomId);
        Texture2D keyTexture = GD.Load<Texture2D>(texturePath);
        if (keyTexture != null)
            _sprite.Texture = keyTexture;
        else
            GD.PushWarning($"GroundKeyPickup: key sprite could not be loaded: {texturePath}");

        _glow = WorldInteractableGlow.AddGlow(this, new Color("ffd36e"), 2.5f);

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
        _prompt.AddThemeColorOverride("font_color", Colors.White);
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 4);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        RefreshAvailability();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;

        if (_collected)
        {
            _feedbackRemaining -= (float)delta;
            if (_feedbackRemaining <= 0f)
                QueueFree();
            return;
        }

        RefreshAvailability();
        if (_wasAvailable)
            WorldInteractableGlow.Animate(_glow, _elapsed, 0f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_collected || !_playerInside || !IsKeyAvailable() ||
            !@event.IsActionPressed("interact"))
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
        _wasAvailable = false;
        AudioManager.Instance?.PlayObjectObtain();
        AudioManager.Instance?.PlayDoorUnlock();
        Monitoring = false;
        _sprite.Visible = false;
        if (_glow != null)
            _glow.Visible = false;
        _prompt.Text = $"KEY ACQUIRED: {roomName} unlocked!";
        _prompt.Visible = true;
        _feedbackRemaining = 2.5f;

        SaveManager.Instance?.RequestAutoSave();
    }

    private void RefreshAvailability()
    {
        bool available = !_collected && IsKeyAvailable();
        if (available == _wasAvailable)
            return;

        _wasAvailable = available;
        Monitoring = available;
        _sprite.Visible = available;
        if (_glow != null)
            _glow.Visible = available;

        if (!available)
        {
            _playerInside = false;
            _prompt.Visible = false;
        }
    }

    private bool IsKeyAvailable()
    {
        string pending = RoomKeyProgression.Instance?.PendingKeyRoomName ?? string.Empty;
        return string.Equals(pending, KeyRoomId, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetKeyTexturePath(string roomId) => roomId switch
    {
        "Kitchen" => "res://Assets/Objects/Keys/keyKitchen.png",
        "Laundry" => "res://Assets/Objects/Keys/keyLaundry.png",
        "Bathroom" => "res://Assets/Objects/Keys/keyBathroom.png",
        "MasterBedroom" => "res://Assets/Objects/Keys/keyMasterBedroom.png",
        _ => "res://Assets/Objects/Keys/roomKey.png"
    };

    private void OnBodyEntered(Node2D body)
    {
        if (_collected || !IsKeyAvailable())
            return;

        _playerInside = true;
        _prompt.Text = $"[E] Pick up {KeyRoomId} key";
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
