using Godot;
using SchoolYardGame;
using System.Collections.Generic;
public partial class Classroom : Control
{
    [Export] public bool AllowEscapeToLeave { get; set; } = true;

    [Export(PropertyHint.File, "*.tscn")]
    public string ChallengeScenePath { get; set; } = "res://Scenes/Challenges/SequenceChallenge.tscn";

    private static readonly (string Name, string PortraitPath)[] Greeters =
    {
        ("Mich", "res://Assets/Characters/Mich.png"),
        ("Jepoy", "res://Assets/Characters/Jepoy.png"),
        ("Cess", "res://Assets/Characters/Cess.png"),
    };

    private VisualNovelScene _dialogue;
    private Label _leaveHint;
    private Node2D _player;
    private bool _greetingActive;
    private bool _firstDayGreetingVariant;

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        _dialogue = GetNode<VisualNovelScene>("%GreetingDialogue");
        _leaveHint = GetNode<Label>("%LeaveHint");
        _leaveHint.Visible = false;

        FreezePlayer();

        _firstDayGreetingVariant = DayClock.Instance?.ConsumeFirstDaySpecialGreetingPending() ?? false;
        _greetingActive = true;
        _dialogue.DialogueFinished += OnGreetingFinished;
        CallDeferred(MethodName.StartGreeting);
    }

    public override void _ExitTree()
    {
        ReleasePlayer();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (AllowEscapeToLeave && !_greetingActive && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            if (DayClock.Instance != null)
                DayClock.Instance.Running = true;
            QueueFree();
        }
    }

    private void StartGreeting()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var (name, portraitPath) = Greeters[rng.RandiRange(0, Greeters.Length - 1)];

        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(portraitPath));

        string timeGreeting = GameController.Darkness switch
        {
            < 0.33f => "Good morning!",
            < 0.66f => "Good afternoon!",
            _ => "Good evening!",
        };

        _dialogue.StartDialogue(_firstDayGreetingVariant
            ? BuildFirstDayGreeting(name, timeGreeting)
            : BuildGreeting(name, timeGreeting));
    }

    private static List<DialogueLine> BuildFirstDayGreeting(string speaker, string timeGreeting) => new()
    {
        new DialogueLine(
            speaker,
            $"{timeGreeting} Glad you made it to class.",
            DialogueSide.Right,
            Choices: new[]
            {
                new DialogueChoice("Morning as well... I should get ready for class.", Goto: 1),
                new DialogueChoice("Do you know the person who just went to the next classroom?", Goto: 2),
            }),

        new DialogueLine(
            speaker,
            "Same here. I should get inside before the bell rings.",
            DialogueSide.Right,
            Next: 3),

        new DialogueLine(
            speaker,
            "What person? I haven't seen anyone like that. Are you sure you saw someone? Come on, let's get inside before class starts.",
            DialogueSide.Right,
            Next: 3),

    };

    private static List<DialogueLine> BuildGreeting(string speaker, string timeGreeting) => new()
    {
        new DialogueLine(
            speaker,
            $"{timeGreeting} Glad you made it to class.",
            DialogueSide.Right,
            Choices: new[]
            {
                new DialogueChoice("Good to see you", Goto: 1),
                new DialogueChoice("Sorry I'm late", Goto: 2),
            }),

        new DialogueLine(speaker, "Same here. Grab a seat anywhere.", DialogueSide.Right, Next: 3),
        new DialogueLine(speaker, "No worries, class hasn't started yet. Grab a seat.", DialogueSide.Right),
    };

    private void OnGreetingFinished()
    {
        _greetingActive = false;
        _dialogue.QueueFree();

        bool alreadyCleared = ChallengeProgress.IsCompleted(ChallengeProgress.SequenceId);

        if (!alreadyCleared
            && !string.IsNullOrWhiteSpace(ChallengeScenePath)
            && ResourceLoader.Exists(ChallengeScenePath))
        {
            _leaveHint.Visible = false;
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile(ChallengeScenePath);
            else
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ChallengeScenePath);
            return;
        }

        _leaveHint.Visible = AllowEscapeToLeave;
    }

    private void FreezePlayer()
    {
        _player = (GetTree().GetFirstNodeInGroup("room_manager") as Node)?.GetNodeOrNull<Node2D>("Player");
        if (_player == null) return;

        if (_player is CharacterBody2D body)
            body.Velocity = Vector2.Zero;
        _player.ProcessMode = ProcessModeEnum.Disabled;
    }

    private void ReleasePlayer()
    {
        if (_player != null && IsInstanceValid(_player))
            _player.ProcessMode = ProcessModeEnum.Inherit;
        _player = null;
    }
}
