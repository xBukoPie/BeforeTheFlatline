using Godot;
using SchoolYardGame;
using System.Collections.Generic;



/// <summary>
/// Root of Classroom.tscn. The classroom is the base scene; when the player enters, a visual-novel
/// greeting (VisualNovelScene) plays on top of it. The player is frozen while the classroom is open
/// and released again when it closes.
///
/// Flow:  School door  ->  Door.Use() overlays this scene on World/UI  ->  _Ready starts the greeting.
/// </summary>
public partial class Classroom : Control
{
    /// <summary>After the greeting, let the player close the classroom with Esc (ui_cancel).</summary>
    [Export] public bool AllowEscapeToLeave { get; set; } = true;

    // Who can greet the player. Same cast SchoolGreeting used.
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
        _leaveHint.Visible = false; // shown in the editor for layout preview only

        FreezePlayer();

        _greetingActive = true;
        _dialogue.DialogueFinished += OnGreetingFinished;
        CallDeferred(MethodName.StartGreeting); // wait a frame so the dialogue UI has its final layout
    }

    public override void _ExitTree()
    {
        // Runs however the classroom goes away (Esc, scene change, QueueFree...).
        ReleasePlayer();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (AllowEscapeToLeave && !_greetingActive && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            QueueFree(); // removes the overlay; the school room underneath is still there
        }
    }

    // ───────────────────────── Greeting event ─────────────────────────

    private void StartGreeting()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var (name, portraitPath) = Greeters[rng.RandiRange(0, Greeters.Length - 1)];

        // Falls back to "no portrait" (invisible, since placeholders are off) if the file is missing.
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(portraitPath));

        string timeGreeting = DayNightCycle.Darkness switch
        {
            < 0.33f => "Good morning!",
            < 0.66f => "Good afternoon!",
            _ => "Good evening!",
        };

        _dialogue.StartDialogue(BuildGreeting(name, timeGreeting));
    }

    private static List<DialogueLine> BuildGreeting(string speaker, string timeGreeting) => new()
    {
        // 0 - the classmate greets you; you pick a reply.
        new DialogueLine(
            speaker,
            $"{timeGreeting} Glad you made it to class.",
            DialogueSide.Right,
            Choices: new[]
            {
                new DialogueChoice("Good to see you", Goto: 1),
                new DialogueChoice("Sorry I'm late", Goto: 2),
            }),

        // 1 - friendly branch; Next = 3 is past the end, so the dialogue finishes here.
        new DialogueLine(speaker, "Same here. Grab a seat anywhere.", DialogueSide.Right, Next: 3),

        // 2 - apologetic branch (also the last line).
        new DialogueLine(speaker, "No worries, class hasn't started yet. Grab a seat.", DialogueSide.Right),
    };

    private void OnGreetingFinished()
    {
        _greetingActive = false;
        _dialogue.QueueFree();
        _leaveHint.Visible = AllowEscapeToLeave;
    }

    // ───────────────────────── Player handling ─────────────────────────

    // Same approach RoomManager.GoTo uses: disabling the player's process mode stops movement/input.
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
