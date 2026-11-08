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
    private bool _playerModeCaptured;
    private bool _finishStarted;
    private int _conversationStage;
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
        if (_conversationOpen)
            return;

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

        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnDialogueFinished;

        _afterConversation = null;
        RestorePlayer();

        // If the interaction was interrupted by an unrelated scene exit,
        // never leave the global screen fade locked over the next scene.
        if (_conversationOpen)
            SceneTransition.Instance?.CancelOverlayFade();
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
        if (_conversationOpen)
            return false;

        int stage = GetAvailableStage();
        if (stage == 0)
            return false;

        PackedScene scene = GD.Load<PackedScene>(DialogueScenePath);
        if (scene == null)
        {
            GD.PushError("Guardian: VisualNovelScene.tscn could not be loaded.");
            return false;
        }

        // Reset transient state before every interaction; no previous door
        // continuation or dialogue instance may leak into this conversation.
        _afterConversation = null;
        _dialogue = null;
        _dialogueLayer = null;
        _finishStarted = false;
        _conversationStage = stage;
        _activeStage = stage;
        _afterConversation = afterConversation;
        _conversationOpen = true;

        if (_talkArea != null && GodotObject.IsInstanceValid(_talkArea))
            _talkArea.Monitoring = false;

        InteractionPromptManager.Instance?.HidePrompt(this);
        FreezePlayer();

        _previousClockRunning = DayClock.Instance?.Running ?? false;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        ShowDialogueAfterTransition(
            scene,
            BuildLines(_conversationStage, mandatory, StoryProgression.Instance));
        return true;
    }

    private async void ShowDialogueAfterTransition(
        PackedScene scene, IReadOnlyList<DialogueLine> lines)
    {
        SceneTree tree = GetTree();
        SceneTransition transition = SceneTransition.Instance;
        bool covered = false;

        if (transition != null)
            covered = await transition.FadeToBlackForOverlayAsync(0.26f);

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || !_conversationOpen)
        {
            if (covered && GodotObject.IsInstanceValid(transition))
                await transition.FadeFromBlackForOverlayAsync(0.18f);
            return;
        }

        _dialogueLayer = new CanvasLayer
        {
            Name = "GuardianDialogueLayer",
            Layer = 108
        };
        AddChild(_dialogueLayer);

        _dialogue = scene.Instantiate<VisualNovelScene>();
        _dialogue.ProcessMode = ProcessModeEnum.Always;
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.ShowBackground = false;
        _dialogue.ShowPlaceholders = false;
        _dialogue.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _dialogueLayer.AddChild(_dialogue);
        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(PortraitPath));
        _dialogue.DialogueFinished += OnDialogueFinished;

        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
            !_conversationOpen || _dialogue == null ||
            !GodotObject.IsInstanceValid(_dialogue))
        {
            if (covered && GodotObject.IsInstanceValid(transition))
                await transition.FadeFromBlackForOverlayAsync(0.18f);
            return;
        }

        _dialogue.StartDialogue(lines);

        if (covered && GodotObject.IsInstanceValid(transition))
            await transition.FadeFromBlackForOverlayAsync(0.34f);
    }

    private static IReadOnlyList<DialogueLine> BuildLines(
        int stage, bool mandatory, StoryProgression progression)
    {
        if (mandatory)
            return BuildShortcutLines(stage, progression);

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

    private static IReadOnlyList<DialogueLine> BuildShortcutLines(
        int stage, StoryProgression progression)
    {
        var lines = new List<DialogueLine>();

        bool kitchenRemembered = progression?.HasSeenMemory("Kitchen") == true;
        bool laundryRemembered = progression?.HasSeenMemory("Laundry") == true;
        bool bathroomRemembered = progression?.HasSeenMemory("Bathroom") == true;
        bool bedroomRemembered = progression?.HasSeenMemory("MasterBedroom") == true;

        lines.Add(new DialogueLine(
            "...",
            stage == 1
                ? "Heading straight for class? I won't keep you long."
                : "Trying to slip past me again? I'll keep this brief.",
            DialogueSide.Right));

        if (bedroomRemembered)
        {
            lines.Add(new DialogueLine("...", "You've remembered the bedroom too. You finally reached the place where you wanted to ask for help.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Hold on to that moment. Waking up does not mean you have to carry everything by yourself.", DialogueSide.Right));
        }
        else if (bathroomRemembered)
        {
            lines.Add(new DialogueLine("...", "The bathroom brought back the pressure and the way you kept comparing yourself to everyone around you.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "That memory was painful, but it is not proof that you are failing. You deserve help with what you have been carrying.", DialogueSide.Right));
        }
        else if (laundryRemembered)
        {
            lines.Add(new DialogueLine("...", "You remembered the laundry room too. Even the small, repeated tasks had started to feel exhausting.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "You kept pushing through the numbness and fatigue as if you had to manage everything alone.", DialogueSide.Right));
        }
        else if (kitchenRemembered)
        {
            lines.Add(new DialogueLine("...", "That kitchen photograph brought back how difficult even an ordinary meal had become.", DialogueSide.Right));
            lines.Add(new DialogueLine("...", "Do not dismiss that memory just because the room looked familiar. It is part of what you have been trying to understand.", DialogueSide.Right));
        }
        else
        {
            lines.Add(new DialogueLine("...", "There are memories waiting behind the doors you have opened. Pay attention to what returns, even when it is uncomfortable.", DialogueSide.Right));
        }

        if (stage == 1)
            lines.Add(new DialogueLine("...", "This place may feel familiar, but not everything here is as it seems. Keep looking for the truth.", DialogueSide.Right));
        else if (stage == 2)
            lines.Add(new DialogueLine("...", "You know more than you did before. Do not let the next part frighten you into turning away.", DialogueSide.Right));
        else
            lines.Add(new DialogueLine("...", "You're close now. Do not mistake the feeling of being overwhelmed for a reason to give up. Let someone help when you need it.", DialogueSide.Right));

        lines.Add(new DialogueLine(
            "...",
            stage == 3
                ? "That's all for now. Go on; I'll be here."
                : "I won't hold you here. Go on, and keep what you remembered with you.",
            DialogueSide.Right));

        return lines;
    }

    private async void OnDialogueFinished()
    {
        if (!_conversationOpen || _finishStarted)
            return;

        _finishStarted = true;
        SceneTree tree = GetTree();
        SceneTransition transition = SceneTransition.Instance;
        bool covered = false;

        if (transition != null)
            covered = await transition.FadeToBlackForOverlayAsync(0.28f);

        int completedStage = _conversationStage;
        StoryProgression progression = StoryProgression.Instance;
        bool marked = progression?.CompleteGuardianStage(completedStage) == true;
        if (marked)
            SaveManager.Instance?.RequestAutoSave();
        else if (progression?.HasSeenGuardianStage(completedStage) != true)
            GD.PushWarning($"Guardian: stage {completedStage} could not be completed; its required memory may be missing.");

        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.DialogueFinished -= OnDialogueFinished;
            _dialogue.QueueFree();
        }

        if (_dialogueLayer != null && GodotObject.IsInstanceValid(_dialogueLayer))
            _dialogueLayer.QueueFree();

        _dialogue = null;
        _dialogueLayer = null;
        _conversationOpen = false;

        Action continueAction = _afterConversation;
        _afterConversation = null;

        RefreshAvailability();

        // The door continuation starts RoomManager.GoTo synchronously. Keep the
        // global fade closed until that room transition has completed.
        continueAction?.Invoke();

        RoomManager roomManager = tree.GetFirstNodeInGroup("room_manager") as RoomManager;
        if (continueAction != null && roomManager != null)
        {
            while (roomManager.IsRoomTransitionBusy)
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;

        RestorePlayer();

        if (covered && GodotObject.IsInstanceValid(transition))
            await transition.FadeFromBlackForOverlayAsync(0.36f);

        if (IsInsideTree())
            _finishStarted = false;
    }

    private void FreezePlayer()
    {
        _playerModeCaptured = false;
        if (_player == null || !GodotObject.IsInstanceValid(_player) ||
            _player.ProcessMode == ProcessModeEnum.Disabled)
            return;

        _previousPlayerMode = _player.ProcessMode;
        _playerModeCaptured = true;
        _player.Velocity = Vector2.Zero;
        _player.ProcessMode = ProcessModeEnum.Disabled;
    }

    private void RestorePlayer()
    {
        if (!_playerModeCaptured)
            return;

        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _player.Velocity = Vector2.Zero;
            _player.ProcessMode = _previousPlayerMode;
        }

        _playerModeCaptured = false;
    }
}
