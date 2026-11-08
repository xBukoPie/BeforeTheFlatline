using Godot;
using System;
using System.Collections.Generic;
using SchoolYardGame;

public partial class GuardianInteractable : Node2D
{
    private const string DialogueScenePath = "res://Scenes/UI/VisualNovelScene.tscn";
    private const string ModelScenePath = "res://Scenes/Models/MysteriousPerson.tscn";
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
    private Action _afterConversation;

    public override void _Ready()
    {
        Name = "Guardian";
        AddToGroup("guardian_interactable");
        BuildMysteriousPersonModel();

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
        bool available = stage != 0;

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
        if (!_isAvailable || !_playerInside || _conversationOpen)
            return;

        bool inputMatches = InteractionPromptManager.Instance != null
            ? InteractionPromptManager.Instance.IsInputFor(this, @event)
            : @event.IsActionPressed("interact");
        if (!inputMatches)
            return;

        GetViewport().SetInputAsHandled();
        BeginConversation();
    }

    public override void _ExitTree()
    {
        InteractionPromptManager.Instance?.HidePrompt(this);
        if (_conversationOpen && DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;
        RestorePlayer();
    }

    private void RefreshAvailability()
    {
        _activeStage = GetAvailableStage();
        _isAvailable = _activeStage != 0;
        Visible = _isAvailable;
        _talkArea.Monitoring = _isAvailable;
    }

    private int GetAvailableStage()
    {
        if (DayClock.Instance == null ||
            DayClock.Instance.DayNumber <= 1 ||
            DayClock.Instance.SchoolChallengeCompletedToday)
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

    private void BuildMysteriousPersonModel()
    {
        PackedScene modelScene = GD.Load<PackedScene>(ModelScenePath);
        if (modelScene == null)
        {
            GD.PushError($"Guardian: MysteriousPerson.tscn could not be loaded from '{ModelScenePath}'.");
            return;
        }

        Node2D model = modelScene.Instantiate<Node2D>();
        model.Name = "MysteriousPersonModel";
        AddChild(model);

        AnimatedSprite2D idleSprite = model.GetNodeOrNull<AnimatedSprite2D>("Sprite2D");
        if (idleSprite != null && idleSprite.SpriteFrames != null &&
            idleSprite.SpriteFrames.HasAnimation("Idle"))
            idleSprite.Play("Idle");

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

    public bool TryBeginMandatoryConversation(Action afterConversation)
    {
        int stage = GetAvailableStage();
        if (stage == 0 || _conversationOpen)
            return false;

        _activeStage = stage;
        _isAvailable = true;
        Visible = true;
        if (_talkArea != null && GodotObject.IsInstanceValid(_talkArea))
            _talkArea.Monitoring = true;

        return BeginConversation(true, afterConversation);
    }

    private bool BeginConversation(bool mandatory = false, Action afterConversation = null)
    {
        if (_activeStage == 0 || _conversationOpen)
            return false;

        PackedScene scene = GD.Load<PackedScene>(DialogueScenePath);
        if (scene == null)
        {
            GD.PushError("Guardian: VisualNovelScene.tscn could not be loaded.");
            return false;
        }

        _afterConversation = afterConversation;

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
        _dialogue.ShowBackground = false;
        _dialogue.ShowPlaceholders = false;
        _dialogue.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _dialogueLayer.AddChild(_dialogue);
        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(PortraitPath));
        _dialogue.DialogueFinished += OnDialogueFinished;
        _dialogue.StartDialogue(BuildLines(_activeStage, mandatory));
        return true;
    }

    private static IReadOnlyList<DialogueLine> BuildLines(int stage, bool mandatory)
    {
        // A forced conversation is a separate route: it acknowledges the attempted
        // shortcut and delivers the stage message without replaying normal choices.
        if (mandatory)
            return BuildShortcutLines(stage);

        var lines = new List<DialogueLine>();

        if (stage == 1)
        {
            lines.Add(new DialogueLine("...", "Oh, you're finally here.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "I'm glad you're starting to remember some of your memories.", DialogueSide.Right));
            
            int falseRealityReply = lines.Count + 1;
            int explanationReply = lines.Count + 2;
            int sharedFollowUp = lines.Count + 3;

            lines.Add(new DialogueLine(
                "...",
                "I was starting to think you were going to lose yourself in this false reality.",
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice("False reality?", Goto: falseRealityReply),
                    new DialogueChoice("What are you talking about?", Goto: explanationReply)
                }));
            lines.Add(new DialogueLine("You", "False reality?", DialogueSide.Left, Next: sharedFollowUp));
            lines.Add(new DialogueLine("You", "What are you talking about?", DialogueSide.Left, Next: sharedFollowUp));
            lines.Add(new DialogueLine("...", "Hm? Don't mind me. Class is starting soon, you better get going now.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Don't forget to eat well. It might not seem like it, but a lot of people care about you, including me.", DialogueSide.Right));
        }
        else if (stage == 2)
        {
            lines.Add(new DialogueLine("...", "You're here early.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "You're one step closer to finding the truth.", DialogueSide.Right));

            int responseIndex = lines.Count + 1;
            int farewellIndex = lines.Count + 2;
            lines.Add(new DialogueLine(
                "...",
                "When the time comes, I want you to face it bravely. Okay?",
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice("Okay...", Goto: responseIndex)
                }));
            lines.Add(new DialogueLine("You", "Okay...", DialogueSide.Left, Next: farewellIndex));
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

    private static IReadOnlyList<DialogueLine> BuildShortcutLines(int stage)
    {
        var lines = new List<DialogueLine>();

        if (stage == 1)
        {
            lines.Add(new DialogueLine("...", "Heading straight for class? I won't keep you long.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Those photographs you've found are pieces of your own memories. Don't dismiss what they bring back, even when it feels strange.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "This place may feel familiar, but that doesn't mean everything here is as it seems.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Go on. Just keep looking for the truth, alright?", DialogueSide.Right));
        }
        else if (stage == 2)
        {
            lines.Add(new DialogueLine("...", "Trying to slip past me? Alright, I'll be brief.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "You're closer to the truth now. Don't let the next part scare you into turning away.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "When the time comes, face what you remember. You don't have to understand it all at once.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "That's all. Go on before you're late.", DialogueSide.Right));
        }
        else if (stage == 3)
        {
            lines.Add(new DialogueLine("...", "You're in a hurry... but wait just a moment.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "I can see how heavy this has become. The pressure can make it feel like there's no way forward.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Don't mistake that feeling for the truth. You're almost there. Keep going, and let someone help when you need it.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Now, go. I'll be here.", DialogueSide.Right));
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

        Action continueAction = _afterConversation;
        _afterConversation = null;
        continueAction?.Invoke();
    }

    private void FreezePlayer()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player) ||
            _player.ProcessMode == ProcessModeEnum.Disabled)
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
