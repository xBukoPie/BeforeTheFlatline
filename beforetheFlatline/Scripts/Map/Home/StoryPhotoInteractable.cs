using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class StoryPhotoInteractable : Area2D
{
    [Export] public string MemoryId { get; set; } = "Kitchen";

    private Sprite2D _sprite;
    private Sprite2D _glow;
    private bool _playerInside;
    private double _elapsed;
    private bool _triggered;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitorable = false;
        Monitoring = true;

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite == null)
        {
            GD.PushError($"StoryPhotoInteractable '{Name}': missing Sprite2D child.");
            SetProcess(false);
            return;
        }

        _glow = WorldInteractableGlow.AddGlow(this, new Color("f8d98a"), 2.4f);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        RefreshAvailability();
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        WorldInteractableGlow.Animate(_glow, _elapsed, 0.7f);

        bool available = StoryProgression.Instance?.CanTriggerMemory(MemoryId) == true;
        if (_triggered || !available)
        {
            if (Visible)
                Visible = false;
            InteractionPromptManager.Instance?.HidePrompt(this);
            Monitoring = false;
            _playerInside = false;
            return;
        }

        if (!Visible)
            Visible = true;
        if (!Monitoring)
            Monitoring = true;

        if (_playerInside)
            InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Examine photograph", 70);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_triggered || !_playerInside ||
            (InteractionPromptManager.Instance != null && !InteractionPromptManager.Instance.IsPrimary(this)) ||
            !@event.IsActionPressed("interact"))
            return;

        GetViewport().SetInputAsHandled();
        BeginMemory();
    }

    private void RefreshAvailability()
    {
        bool available = StoryProgression.Instance?.CanTriggerMemory(MemoryId) == true;
        Visible = available;
        Monitoring = available;
        InteractionPromptManager.Instance?.HidePrompt(this);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_triggered || !Monitoring)
            return;

        _playerInside = true;
        InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Examine photograph", 70);
    }

    private void OnBodyExited(Node2D body)
    {
        _playerInside = false;
        InteractionPromptManager.Instance?.HidePrompt(this);
    }

    private void BeginMemory()
    {
        if (StoryProgression.Instance?.CanTriggerMemory(MemoryId) != true)
            return;

        var manager = GetTree().GetFirstNodeInGroup("room_manager") as RoomManager;
        var uiRoot = manager?.GetNodeOrNull<Node>("UI");
        if (uiRoot == null)
        {
            GD.PushError("StoryPhotoInteractable: RoomManager UI root was not found.");
            return;
        }

        StoryCutsceneOverlay overlay = new();
        bool finalMemory = MemoryId.Equals("MasterBedroom", System.StringComparison.OrdinalIgnoreCase);
        overlay.Configure(GetHeading(), BuildBeats(), () => CompleteMemory(finalMemory), finalMemory);
        _triggered = true;
        _playerInside = false;
        InteractionPromptManager.Instance?.HidePrompt(this);
        uiRoot.AddChild(overlay);
    }

    private string GetHeading() => MemoryId switch
    {
        "Kitchen" => "MEMORY FRAGMENT — THE KITCHEN",
        "Laundry" => "MEMORY FRAGMENT — THE LAUNDRY",
        "Bathroom" => "MEMORY FRAGMENT — THE BATHROOM",
        "MasterBedroom" => "MEMORY FRAGMENT — THE MASTER BEDROOM",
        _ => "MEMORY FRAGMENT"
    };

    private List<StoryCutsceneBeat> BuildBeats()
    {
        const string kitchenImage = "res://Assets/Cutscenes/cutsceneKitcken.jpg";
        const string laundryImage = "res://Assets/Cutscenes/cutsceneLaundry.jpg";
        const string bathroomImage = "res://Assets/Cutscenes/cutsceneBathroomSink.jpg";
        const string toBedroomImage = "res://Assets/Cutscenes/cutsceneToMasterBedroom.jpg";
        const string bedroomImage = "res://Assets/Cutscenes/cutsceneMasterBedroom.jpg";
        const string goodEndingImage = "res://Assets/Cutscenes/cutsceneGoodEnding.jpg";

        return MemoryId switch
        {
            "Kitchen" => new List<StoryCutsceneBeat>
            {
                new(kitchenImage, new DialogueLine(
                    "Memory",
                    "Eat your food before you go to school today. You haven't been eating lately.",
                    DialogueSide.Left)),
                new(kitchenImage, new DialogueLine(
                    "You",
                    "I know. I'm just not hungry. Even sitting here feels harder than it should.",
                    DialogueSide.Right,
                    Choices: new[]
                    {
                        new DialogueChoice("I'll try to eat a little.", Goto: 2),
                        new DialogueChoice("I can't. Even the smell feels wrong.", Goto: 3)
                    })),
                new(kitchenImage, new DialogueLine(
                    "You",
                    "A few bites. I can at least try that much.",
                    DialogueSide.Right,
                    Next: 4)),
                new(kitchenImage, new DialogueLine(
                    "You",
                    "I can't force it. The smell turns my stomach, and I don't know why.",
                    DialogueSide.Right,
                    Next: 4),
                    "nausea"),
                new(kitchenImage, new DialogueLine(
                    "Memory",
                    "The food tastes bland lately. The kitchen is familiar, but something about it feels distant.",
                    DialogueSide.Left),
                    "nausea"),
                new(kitchenImage, new DialogueLine(
                    "You",
                    "That felt familiar... and uncomfortable. Is this part of my memory? I feel dizzy.",
                    DialogueSide.Right),
                    "dizziness"),
                new(kitchenImage, new DialogueLine(
                    "You",
                    "I need a moment to steady myself.",
                    DialogueSide.Right))
            },

            "Laundry" => new List<StoryCutsceneBeat>
            {
                new(laundryImage, new DialogueLine(
                    "You",
                    "I keep watching my uniform spin inside the washing machine.",
                    DialogueSide.Right)),
                new(laundryImage, new DialogueLine(
                    "You",
                    "I don't want to go to school today.",
                    DialogueSide.Right),
                    "dizziness")
            },

            "Bathroom" => new List<StoryCutsceneBeat>
            {
                new(bathroomImage, new DialogueLine(
                    "You",
                    "This is pointless. I just want to disappear.",
                    DialogueSide.Right),
                    "pressure"),
                new(bathroomImage, new DialogueLine(
                    "You",
                    "My friends and classmates seem to do better than me. They make everything look so easy.",
                    DialogueSide.Right)),
                new(bathroomImage, new DialogueLine(
                    "You",
                    "I keep comparing myself to them. No matter what I do, I feel like I'm falling behind.",
                    DialogueSide.Right),
                    "pressure"),
                new(bathroomImage, new DialogueLine(
                    "You",
                    "The pressure and panic come rushing back. My thoughts won't slow down.",
                    DialogueSide.Right),
                    "panic"),
                new(bathroomImage, new DialogueLine(
                    "You",
                    "I feel nauseous and dizzy. I can barely steady myself.",
                    DialogueSide.Right),
                    "nausea"),
                new(bathroomImage, new DialogueLine(
                    "Memory",
                    "The room is quiet again, but the feeling does not disappear with the memory.",
                    DialogueSide.Left))
            },

            "MasterBedroom" => new List<StoryCutsceneBeat>
            {
                new(toBedroomImage, new DialogueLine(
                    "You",
                    "I need to talk to my parents. I need to ask them for help.",
                    DialogueSide.Right)),
                new(toBedroomImage, new DialogueLine(
                    "You",
                    "Every step toward the bedroom makes the pressure heavier. I keep trying to find the right words.",
                    DialogueSide.Right),
                    "pressure"),
                new(toBedroomImage, new DialogueLine(
                    "You",
                    "The bad thoughts keep building. I know I need help, but I don't know how to say it.",
                    DialogueSide.Right),
                    "panic"),
                new(bedroomImage, new DialogueLine(
                    "You",
                    "I finally reach the bedroom, but I collapse before I can say anything.",
                    DialogueSide.Right),
                    "blackout"),
                new("", new DialogueLine(
                    "You",
                    "Everything goes quiet.",
                    DialogueSide.Right),
                    "blackout"),
                new(goodEndingImage, new DialogueLine(
                    "You",
                    "A steady heart-monitor beep fills the room. I slowly open my eyes.",
                    DialogueSide.Right),
                    "relief"),
                new(goodEndingImage, new DialogueLine(
                    "Memory",
                    "My parents are beside me, their eyes full of tears. They thank the heavens that I finally woke up.",
                    DialogueSide.Left),
                    "relief"),
                new(goodEndingImage, new DialogueLine(
                    "You",
                    "We hold each other. I burst into tears. I am not alone.",
                    DialogueSide.Right),
                    "relief")
            },

            _ => new List<StoryCutsceneBeat>
            {
                new(kitchenImage, new DialogueLine(
                    "Memory",
                    "A fragment of a memory returns.",
                    DialogueSide.Left))
            }
        };
    }

    private void CompleteMemory(bool finalMemory)
    {
        if (StoryProgression.Instance?.MarkMemorySeen(MemoryId) != true)
        {
            _triggered = false;
            RefreshAvailability();
            return;
        }

        if (!finalMemory)
        {
            ApplyMemoryEffect();
            string newlyAvailableKey = RoomKeyProgression.Instance?.QueueAssessmentKey() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(newlyAvailableKey))
                GD.Print($"[StoryPhotoInteractable] The {newlyAvailableKey} key is now available in {RoomKeyProgression.Instance.GetPendingKeyLocationName()}.");
        }
        else
        {
            GD.Print("[StoryPhotoInteractable] All four memory fragments completed. Good ending reached.");
        }

        SaveManager.Instance?.RequestAutoSave();

        if (finalMemory)
        {
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
            else
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/Menu/Main.tscn");
        }
    }

    private void ApplyMemoryEffect()
    {
        var sanity = SanityManager.Instance;
        if (sanity == null)
            return;

        switch (MemoryId)
        {
            case "Kitchen":
                sanity.ApplyDebuff("kitchen_memory", SanityManager.DebuffType.Dizziness, 6);
                break;
            case "Laundry":
                sanity.ApplyDebuff("laundry_memory", SanityManager.DebuffType.Dizziness, 5);
                break;
            case "Bathroom":
                sanity.ApplyDebuff("bathroom_memory", SanityManager.DebuffType.Pressure, 7);
                sanity.ApplyDebuff("bathroom_memory", SanityManager.DebuffType.Dizziness, 5);
                break;
        }
    }
}
