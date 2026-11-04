using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class GuardianInteractable : Node2D
{
    private const string DialogueScenePath = "res://Scenes/UI/VisualNovelScene.tscn";
    private const string PortraitPath = "res://Assets/Characters/Mysterious.png";
    private const int GuardianPromptPriority = 75;

    private CharacterBody2D _player;
    private Area2D _talkArea;
    private bool _playerInside;
    private bool _conversationOpen;
    private bool _isAvailable;
    private int _activeStage;
    private ProcessModeEnum _previousPlayerMode;
    private bool _previousClockRunning;
    private VisualNovelScene _dialogue;
    private CanvasLayer _dialogueLayer;

    public override void _Ready()
    {
        Name = "Guardian";
        BuildAnonymousStudent();

        Node roomManager = GetTree().GetFirstNodeInGroup("room_manager");
        _player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");

        _talkArea = new Area2D
        {
            Name = "TalkArea",
            CollisionLayer = 0,
            CollisionMask = 2,
            Monitoring = false,
            Monitorable = false,
            InputPickable = false,
            Position = new Vector2(0, -8)
        };
        AddChild(_talkArea);
        _talkArea.AddChild(new CollisionShape2D
        {
            Name = "TalkRange",
            Shape = new CircleShape2D { Radius = 32f }
        });

        _talkArea.BodyEntered += OnBodyEntered;
        _talkArea.BodyExited += OnBodyExited;
        RefreshAvailability();
    }

    public override void _Process(double delta)
    {
        int stage = GetAvailableStage();
        bool available = stage > 0;

        if (available != _isAvailable || stage != _activeStage)
        {
            _isAvailable = available;
            _activeStage = stage;
            Visible = available;
            _talkArea.Monitoring = available;

            if (!available)
            {
                _playerInside = false;
                InteractionPromptManager.Instance?.HidePrompt(this);
            }
        }

        if (_isAvailable && _playerInside && !_conversationOpen)
            InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Talk to ...", GuardianPromptPriority);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_isAvailable || !_playerInside || _conversationOpen ||
            !@event.IsActionPressed("interact"))
            return;

        if (InteractionPromptManager.Instance != null &&
            !InteractionPromptManager.Instance.IsPrimary(this))
            return;

        GetViewport().SetInputAsHandled();
        BeginConversation();
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
        RestorePlayer();
    }

    private void RefreshAvailability()
    {
        _activeStage = GetAvailableStage();
        _isAvailable = _activeStage > 0;
        Visible = _isAvailable;
        _talkArea.Monitoring = _isAvailable;
    }

    private int GetAvailableStage()
    {
        if (DayClock.Instance == null || DayClock.Instance.SchoolChallengeCompletedToday)
            return 0;

        if (StoryProgression.Instance == null)
            return 0;

        for (int stage = 1; stage <= 3; stage++)
        {
            if (StoryProgression.Instance.CanShowGuardianStage(stage))
                return stage;
        }

        return 0;
    }

    private void BuildAnonymousStudent()
    {
        // A quiet, faceless silhouette keeps the Guardian's identity unresolved.
        Image image = Image.CreateEmpty(32, 40, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        Color outline = new("11131f");
        Color coat = new("303148");
        Color coatLight = new("4a4964");
        Color shirt = new("77728b");
        Color shadow = new("090b15");

        Fill(image, 8, 3, 23, 9, outline);
        Fill(image, 7, 6, 24, 12, outline);
        Fill(image, 9, 8, 22, 18, shadow);
        Fill(image, 7, 17, 24, 22, outline);
        Fill(image, 5, 21, 26, 34, outline);
        Fill(image, 7, 22, 24, 32, coat);
        Fill(image, 9, 22, 15, 31, coatLight);
        Fill(image, 16, 23, 22, 31, coat);
        Fill(image, 12, 22, 19, 28, shirt);
        Fill(image, 8, 33, 15, 39, outline);
        Fill(image, 17, 33, 24, 39, outline);
        Fill(image, 9, 34, 14, 38, coat);
        Fill(image, 18, 34, 23, 38, coat);

        var sprite = new Sprite2D
        {
            Name = "AnonymousStudentSprite",
            Texture = ImageTexture.CreateFromImage(image),
            Centered = true,
            Scale = new Vector2(1.1f, 1.1f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        AddChild(sprite);

        var nameLabel = new Label
        {
            Name = "Name",
            Text = "...",
            Position = new Vector2(-48, -43),
            Size = new Vector2(96, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 5
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        nameLabel.AddThemeConstantOverride("outline_size", 5);
        AddChild(nameLabel);
    }

    private static void Fill(Image image, int left, int top, int right, int bottom, Color color)
    {
        for (int y = top; y <= bottom; y++)
        for (int x = left; x <= right; x++)
            image.SetPixel(x, y, color);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body != _player || !_isAvailable)
            return;

        _playerInside = true;
        InteractionPromptManager.Instance?.ShowPrompt(this, "[E] Talk to ...", GuardianPromptPriority);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body != _player)
            return;

        _playerInside = false;
        InteractionPromptManager.Instance?.HidePrompt(this);
    }

    private void BeginConversation()
    {
        if (_activeStage <= 0 || _conversationOpen)
            return;

        PackedScene scene = GD.Load<PackedScene>(DialogueScenePath);
        if (scene == null)
        {
            GD.PushError("Guardian: VisualNovelScene.tscn could not be loaded.");
            return;
        }

        _conversationOpen = true;
        InteractionPromptManager.Instance?.HidePrompt(this);
        FreezePlayer();

        _previousClockRunning = DayClock.Instance?.Running ?? false;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        _dialogueLayer = new CanvasLayer
        {
            Name = "GuardianDialogueLayer",
            Layer = 108
        };
        AddChild(_dialogueLayer);

        _dialogue = scene.Instantiate<VisualNovelScene>();
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _dialogueLayer.AddChild(_dialogue);
        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(PortraitPath));
        _dialogue.DialogueFinished += OnDialogueFinished;
        _dialogue.StartDialogue(BuildLines(_activeStage));
    }

    private static IReadOnlyList<DialogueLine> BuildLines(int stage)
    {
        var lines = new List<DialogueLine>();

        if (stage == 1)
        {
            lines.Add(new DialogueLine("...", "Oh, you're finally here.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "I'm glad you're starting to remember some of your memories.", DialogueSide.Right));
            lines.Add(new DialogueLine(
                "...",
                "I was starting to think you were going to lose yourself in this false reality.",
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice("False reality?", Goto: 3),
                    new DialogueChoice("What are you talking about?", Goto: 4)
                }));
            lines.Add(new DialogueLine("You", "False reality?", DialogueSide.Left, Next: 5));
            lines.Add(new DialogueLine("You", "What are you talking about?", DialogueSide.Left, Next: 5));
            lines.Add(new DialogueLine("...", "Hm? Don't mind me. Class is starting soon, you better get going now.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Don't forget to eat well. It might not seem like it, but a lot of people care about you, including me.", DialogueSide.Right));
        }
        else if (stage == 2)
        {
            lines.Add(new DialogueLine("...", "You're here early.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "You're one step closer to finding the truth.", DialogueSide.Right));
            lines.Add(new DialogueLine(
                "...",
                "When the time comes, I want you to face it bravely. Okay?",
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice("Okay...", Goto: 3)
                }));
            lines.Add(new DialogueLine("You", "Okay...", DialogueSide.Left));
            lines.Add(new DialogueLine("...", "Class is starting... *sigh* I'll see you around.", DialogueSide.Right));
        }
        else if (stage == 3)
        {
            lines.Add(new DialogueLine("...", "You don't look so well...", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "The pressure is too overwhelming, isn't it?", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "It feels easier to just swim down and let the currents sweep you away.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "But you're almost there...", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "If you keep going, I promise you'll overcome it and this will all be over soon.", DialogueSide.Right));
        }

        return lines;
    }

    private void OnDialogueFinished()
    {
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnDialogueFinished;

        if (StoryProgression.Instance?.MarkGuardianStageSeen(_activeStage) == true)
            SaveManager.Instance?.RequestAutoSave();

        _dialogueLayer?.QueueFree();
        _dialogue = null;
        _dialogueLayer = null;
        _conversationOpen = false;

        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;

        RestorePlayer();
        RefreshAvailability();
    }

    private void FreezePlayer()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        _previousPlayerMode = _player.ProcessMode;
        _player.Velocity = Vector2.Zero;
        _player.ProcessMode = ProcessModeEnum.Disabled;
    }

    private void RestorePlayer()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _player.Velocity = Vector2.Zero;
            _player.ProcessMode = _previousPlayerMode;
        }
    }
}
