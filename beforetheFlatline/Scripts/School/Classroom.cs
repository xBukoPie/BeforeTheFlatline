using Godot;
using SchoolYardGame;
using System.Collections.Generic;
/// This script triggers the classroom scene. Freezing the player while inside the classroom until closed.
/// Flow:  School door  ->  Door.Use() overlays this scene on World/UI  ->  _Ready starts the greeting.
public partial class Classroom : Control
{
    /// <summary>Emitted once the classroom greeting dialogue has fully finished.</summary>
    [Signal] public delegate void ClassroomGreetingFinishedEventHandler();
    [Export] public bool AllowEscapeToLeave { get; set; } = true;

    // Randomized freinds greet.
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

    public override void _Ready()
    {
        _dialogue = GetNode<VisualNovelScene>("%GreetingDialogue");
        _leaveHint = GetNode<Label>("%LeaveHint");
        _leaveHint.Visible = false;

        FreezePlayer();

        _greetingActive = true;
        _dialogue.DialogueFinished += OnGreetingFinished;
        CallDeferred(MethodName.StartGreeting);
    }

    public override void _ExitTree()
    {
        // This is self-explanatory.
        ReleasePlayer();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (AllowEscapeToLeave && !_greetingActive && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
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

        _dialogue.StartDialogue(BuildGreeting(name, timeGreeting));
    }

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
        _leaveHint.Visible = AllowEscapeToLeave;
        EmitSignal(SignalName.ClassroomGreetingFinished);
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
