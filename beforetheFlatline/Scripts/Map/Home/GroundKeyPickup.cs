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
    private bool _availabilityInitialized;

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
            (InteractionPromptManager.Instance != null && !InteractionPromptManager.Instance.IsPrimary(this)) ||
            !@event.IsActionPressed("interact"))
            return;

        GetViewport().SetInputAsHandled();

        string roomName = RoomKeyProgression.Instance?.CollectPendingKey() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            InteractionPromptManager.Instance?.ShowNotice(this, "The key is no longer available.");
            _playerInside = false;
            InteractionPromptManager.Instance?.HidePrompt(this);
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
        InteractionPromptManager.Instance?.HidePrompt(this);
        InteractionPromptManager.Instance?.ShowNotice(this, $"KEY ACQUIRED: {roomName} unlocked!", 2.5f);
        _feedbackRemaining = 2.5f;

        SaveManager.Instance?.RequestAutoSave();
    }

    private void RefreshAvailability()
    {
        bool available = !_collected && IsKeyAvailable();
        if (_availabilityInitialized && available == _wasAvailable)
            return;

        _availabilityInitialized = true;
        _wasAvailable = available;
        Monitoring = available;
        _sprite.Visible = available;
        if (_glow != null)
            _glow.Visible = available;

        if (!available)
        {
            _playerInside = false;
            InteractionPromptManager.Instance?.HidePrompt(this);
        }
        else if (_playerInside)
        {
            InteractionPromptManager.Instance?.ShowPrompt(this, $"[E] Pick up {KeyRoomId} key", 90);
        }
    }

    private bool IsKeyAvailable()
    {
        string pending = RoomKeyProgression.Instance?.PendingKeyRoomName ?? string.Empty;
        if (!string.Equals(pending, KeyRoomId, StringComparison.OrdinalIgnoreCase))
            return false;

        int keyIndex = KeyRoomId switch
        {
            "Kitchen" => 0,
            "Laundry" => 1,
            "Bathroom" => 2,
            "MasterBedroom" => 3,
            _ => -1
        };

        // A stale pending-key value must never reveal a later key before its previous memory.
        return keyIndex >= 0 && StoryProgression.Instance?.CanOfferKeyForRoomIndex(keyIndex) == true;
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
        InteractionPromptManager.Instance?.ShowPrompt(this, $"[E] Pick up {KeyRoomId} key", 90);
    }

    private void OnBodyExited(Node2D body)
    {
        _playerInside = false;
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
}
