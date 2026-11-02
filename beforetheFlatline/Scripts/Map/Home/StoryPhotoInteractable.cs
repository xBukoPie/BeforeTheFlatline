using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class StoryPhotoInteractable : Area2D
{
    [Export] public string MemoryId { get; set; } = "Kitchen";

    private Sprite2D _sprite;
    private Sprite2D _glow;
    private Label _prompt;
    private CanvasLayer _promptLayer;
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

        _promptLayer = new CanvasLayer
        {
            Name = "InteractionPromptLayer",
            Layer = 105
        };
        AddChild(_promptLayer);

        _prompt = new Label
        {
            Text = "[E] Examine photograph",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(640, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _prompt.AnchorLeft = 0.5f;
        _prompt.AnchorRight = 0.5f;
        _prompt.AnchorTop = 1f;
        _prompt.AnchorBottom = 1f;
        _prompt.OffsetLeft = -320;
        _prompt.OffsetRight = 320;
        _prompt.OffsetTop = -106;
        _prompt.OffsetBottom = -58;
        _prompt.AddThemeFontSizeOverride("font_size", 18);
        _prompt.AddThemeColorOverride("font_color", Colors.White);
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 4);
        _promptLayer.AddChild(_prompt);

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        RefreshAvailability();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        WorldInteractableGlow.Animate(_glow, _elapsed, 0.7f, 2.4f);

        bool available = StoryProgression.Instance?.CanTriggerMemory(MemoryId) == true;
        if (_triggered || !available)
        {
            if (Visible)
                Visible = false;
            _prompt.Visible = false;
            Monitoring = false;
            _playerInside = false;
            return;
        }

        if (!Visible)
            Visible = true;
        if (!Monitoring)
            Monitoring = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_triggered || !_playerInside || !@event.IsActionPressed("interact"))
            return;

        GetViewport().SetInputAsHandled();
        BeginMemory();
    }

    private void RefreshAvailability()
    {
        bool available = StoryProgression.Instance?.CanTriggerMemory(MemoryId) == true;
        Visible = available;
        Monitoring = available;
        if (_prompt != null)
            _prompt.Visible = false;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_triggered || !Monitoring)
            return;

        _playerInside = true;
        _prompt.Text = "[E] Examine photograph";
        _prompt.Visible = true;
    }

    private void OnBodyExited(Node2D body)
    {
        _playerInside = false;
        if (_prompt != null)
            _prompt.Visible = false;
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
        overlay.Configure(GetHeading(), BuildSlides(), () => CompleteMemory(finalMemory), finalMemory);
        _triggered = true;
        _playerInside = false;
        _prompt.Visible = false;
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

    private List<StoryCutsceneSlide> BuildSlides()
    {
        const string kitchenImage = "res://Assets/Cutscenes/cutsceneKitcken.jpg";
        const string laundryImage = "res://Assets/Cutscenes/cutsceneLaundry.jpg";
        const string bathroomImage = "res://Assets/Cutscenes/cutsceneBathroomSink.jpg";
        const string toBedroomImage = "res://Assets/Cutscenes/cutsceneToMasterBedroom.jpg";
        const string bedroomImage = "res://Assets/Cutscenes/cutsceneMasterBedroom.jpg";
        const string goodEndingImage = "res://Assets/Cutscenes/cutsceneGoodEnding.jpg";

        return MemoryId switch
        {
            "Kitchen" => new List<StoryCutsceneSlide>
            {
                new(kitchenImage, "Eat your food before you go to school today. You haven't been eating lately."),
                new(kitchenImage, "The food tastes bland lately. I know I should eat, but I don't have an appetite."),
                new(kitchenImage, "That felt familiar... and uncomfortable. Is this part of my memory? I feel dizzy.")
            },
            "Laundry" => new List<StoryCutsceneSlide>
            {
                new(laundryImage, "My uniform keeps spinning inside the washing machine. I don't want to go to school today."),
                new(laundryImage, "The memory fades. I feel dizzy.")
            },
            "Bathroom" => new List<StoryCutsceneSlide>
            {
                new(bathroomImage, "This is pointless. I just want to disappear."),
                new(bathroomImage, "My friends and classmates seem to do better than me. I keep comparing myself to them."),
                new(bathroomImage, "The pressure and panic come rushing back. I feel nauseous and dizzy.")
            },
            "MasterBedroom" => new List<StoryCutsceneSlide>
            {
                new(toBedroomImage, "I need to talk to my parents. I need to ask them for help."),
                new(toBedroomImage, "Every step toward the bedroom makes the pressure heavier. The bad thoughts keep building."),
                new(bedroomImage, "I finally reach the bedroom, but I collapse before I can say anything."),
                new(goodEndingImage, "A steady heart-monitor beep fills the room. I slowly open my eyes."),
                new(goodEndingImage, "My parents are beside me, their eyes full of tears. They thank the heavens that I finally woke up."),
                new(goodEndingImage, "We hold each other. I burst into tears. I am not alone.")
            },
            _ => new List<StoryCutsceneSlide>
            {
                new(kitchenImage, "A fragment of a memory returns.")
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
