using Godot;
using SchoolYardGame;
using System.Collections.Generic;
/// This script triggers the classroom scene. Freezing the player while inside the classroom until closed.
/// Flow:  School door  ->  Door.Use() overlays this scene on World/UI  ->  _Ready starts the greeting
///        ->  greeting finishes  ->  hands off to the first Algorithm Lab challenge.
public partial class Classroom : Control
{
    [Export] public bool AllowEscapeToLeave { get; set; } = true;

    /// <summary>Challenge scene opened once the greeting dialogue finishes.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ChallengeScenePath { get; set; } = "res://Scenes/Challenges/SequenceChallenge.tscn";

    // Randomized friends greet.
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
        // Time pauses only once the player is inside the classroom/task sequence.
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        _dialogue = GetNode<VisualNovelScene>("%GreetingDialogue");
        _leaveHint = GetNode<Label>("%LeaveHint");
        _leaveHint.Visible = false;

        FreezePlayer();

        // The special choices are only available immediately after the one-time mysterious
        // encounter. Later school days retain the normal randomized greeting.
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

        new DialogueLine(
            speaker,
            "Come on, we should get started.",
            DialogueSide.Right)
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

        // Next = 3 is past the end, so the dialogue finishes here.
        new DialogueLine(speaker, "Same here. Grab a seat anywhere.", DialogueSide.Right, Next: 3),
        new DialogueLine(speaker, "No worries, class hasn't started yet. Grab a seat.", DialogueSide.Right),
    };

    private void OnGreetingFinished()
    {
        _greetingActive = false;
        _dialogue.QueueFree();

        // Class begins: hand off to the first Algorithm Lab challenge.
        // If the player already cleared the sequence challenge, just let them leave instead.
        bool alreadyCleared = ChallengeProgress.IsCompleted(ChallengeProgress.SequenceId);

        if (!alreadyCleared
            && !string.IsNullOrWhiteSpace(ChallengeScenePath)
            && ResourceLoader.Exists(ChallengeScenePath))
        {
            _leaveHint.Visible = false;
            // Deferred so the dialogue cleanup completes before the scene swap.
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile(ChallengeScenePath);
            else
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ChallengeScenePath);
            return;
        }

        _leaveHint.Visible = AllowEscapeToLeave;
    }

    // This freezes the player while inside the classroom scene, preventing movement or interaction until the scene is closed.
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