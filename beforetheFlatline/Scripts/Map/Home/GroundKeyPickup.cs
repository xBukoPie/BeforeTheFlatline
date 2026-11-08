using Godot;
using SchoolYardGame;
using System;

public partial class GroundKeyPickup : Area2D
{
    [Export] public string KeyRoomId { get; set; } = "Kitchen";

    private Sprite2D _sprite;
    private Sprite2D _glow;
    private CharacterBody2D _player;
    private bool _playerInside;
    private bool _collected;
    private float _feedbackRemaining;
    private double _elapsed;
    private bool _wasAvailable;
    private bool _availabilityInitialized;

    public override void _Ready()
    {
        Visible = false;
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitorable = false;
        Monitoring = false;
        EnsureInteractAction();

        Node roomManager = GetTree().GetFirstNodeInGroup("room_manager");
        _player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");

        _sprite = GetNode<Sprite2D>("Sprite2D");
        _sprite.ZIndex = 0;
        _sprite.ZAsRelative = true;
        ZAsRelative = false;

        string texturePath = GetKeyTexturePath(KeyRoomId);
        Texture2D keyTexture = GD.Load<Texture2D>(texturePath);
        if (keyTexture != null)
            _sprite.Texture = keyTexture;
        else
            GD.PushWarning($"GroundKeyPickup: key sprite could not be loaded: {texturePath}");

        _glow = WorldInteractableGlow.AddGlow(this, new Color("ffd36e"), 4.0f);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        RefreshAvailability();
        UpdateDepthSorting();
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
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

        UpdateDepthSorting();
        RefreshAvailability();
        if (_wasAvailable)
            WorldInteractableGlow.Animate(_glow, _elapsed, 0f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_collected || !_playerInside || !IsKeyAvailable())
            return;

        bool inputMatches = InteractionPromptManager.Instance != null
            ? InteractionPromptManager.Instance.IsInputFor(this, @event)
            : @event.IsActionPressed("interact");
        if (!inputMatches)
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
        InteractionPromptManager.Instance?.ShowNotice(
            this,
            $"KEY ACQUIRED: {roomName} unlocked!\n{GetPickupThought(roomName)}",
            4.0f);
        _feedbackRemaining = 4.0f;

        SaveManager.Instance?.RequestAutoSave();
    }

    private void RefreshAvailability()
    {
        bool available = !_collected && IsKeyAvailable();
        if (_availabilityInitialized && available == _wasAvailable)
            return;

        _availabilityInitialized = true;
        _wasAvailable = available;
        Visible = available;
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

    private void UpdateDepthSorting()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            Node roomManager = GetTree().GetFirstNodeInGroup("room_manager");
            _player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");
        }

        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        // Compare world-space ground positions because the player and room are separate branches.
        ZAsRelative = false;
        ZIndex = GlobalPosition.Y >= _player.GlobalPosition.Y ? 1 : -1;
    }

    private static string GetPickupThought(string roomName) => roomName switch
    {
        "Kitchen" => "I should look for a locked door. This key might open one of the rooms.",
        "Laundry" => "This must open another door. I should search the house for a locked room.",
        "Bathroom" => "I should keep looking for a locked door. This key has to belong somewhere.",
        "MasterBedroom" => "This should open the master bedroom. I need to find its door.",
        _ => "I should look around for a door this key can unlock."
    };

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

        // A stale pending-key value must never reveal a later key before its prior keys and memory.
        if (keyIndex < 0 || RoomKeyProgression.Instance == null ||
            RoomKeyProgression.Instance.TotalKeysEarned < keyIndex)
            return false;

        if (keyIndex == 3 && StoryProgression.Instance?.HasSeenMemory("Bathroom") != true)
            return false;

        return StoryProgression.Instance?.CanOfferKeyForRoomIndex(keyIndex) == true;
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
