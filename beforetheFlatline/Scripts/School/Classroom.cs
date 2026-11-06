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

    private const string ProfessorLessonScenePath = "res://Scenes/School/Rooms/ClassroomVox.tscn";

    private readonly RandomNumberGenerator _rng = new();
    private VisualNovelScene _dialogue;
    private Label _leaveHint;
    private Node2D _player;
    private bool _greetingActive;
    private bool _firstDayGreetingVariant;
    private FirstDayMysteryIntro _firstDayMysteryIntro;

    public override void _Ready()
    {
        if (DayClock.Instance != null)
        {
            DayClock.Instance.Running = false;
            DayClock.Instance.MarkSchoolChallengeStartedToday();
        }

        _dialogue = GetNode<VisualNovelScene>("%GreetingDialogue");
        _leaveHint = GetNode<Label>("%LeaveHint");
        _leaveHint.Visible = false;
        _rng.Randomize();

        FreezePlayer();

        var clock = DayClock.Instance;
        _firstDayGreetingVariant = false;
        _greetingActive = true;
        _dialogue.DialogueFinished += OnGreetingFinished;

        bool showFirstDayIntro = clock != null &&
            clock.DayNumber == 1 &&
            !clock.FirstDayMysteryEncounterSeen;
        _firstDayGreetingVariant = showFirstDayIntro;
        _dialogue.Visible = !showFirstDayIntro;

        if (showFirstDayIntro)
            CallDeferred(nameof(BeginFirstDayMysteryIntro));
        else
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

    private void BeginFirstDayMysteryIntro()
    {
        if (!IsInsideTree() || _firstDayMysteryIntro != null)
            return;

        PackedScene introScene = GD.Load<PackedScene>("res://Scenes/School/FirstDayMysteryIntro.tscn");
        if (introScene == null)
        {
            GD.PushError("Classroom: FirstDayMysteryIntro.tscn could not be loaded.");
            _dialogue.Visible = true;
            StartGreeting();
            return;
        }

        _firstDayMysteryIntro = introScene.Instantiate<FirstDayMysteryIntro>();
        _firstDayMysteryIntro.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _firstDayMysteryIntro.TreeExiting += OnFirstDayMysteryIntroExiting;
        AddChild(_firstDayMysteryIntro);
    }

    private void OnFirstDayMysteryIntroExiting()
    {
        if (_firstDayMysteryIntro != null && GodotObject.IsInstanceValid(_firstDayMysteryIntro))
            _firstDayMysteryIntro.TreeExiting -= OnFirstDayMysteryIntroExiting;

        _firstDayMysteryIntro = null;
        if (!IsInsideTree())
            return;

        _dialogue.Visible = true;
        StartGreeting();
    }

    private void StartGreeting()
    {
        string[] friendNames = { Greeters[0].Name, Greeters[1].Name, Greeters[2].Name };
        string name = SchoolFriendSpawnHistory.GetOrAssignClassroomGreeter(friendNames);

        if (SaveManager.Instance?.CanSaveCurrentGame == true)
            SaveManager.Instance.SaveAutoSave();

        // A friend is present for this greeting only 40% of the time.
        if (string.IsNullOrWhiteSpace(name) || _rng.Randf() >= 0.4f)
        {
            GD.Print("[Classroom] No friend greeting this visit.");
            OnGreetingFinished();
            return;
        }

        (string Name, string PortraitPath) selectedGreeter = Greeters[0];
        foreach (var greeter in Greeters)
        {
            if (string.Equals(greeter.Name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                selectedGreeter = greeter;
                break;
            }
        }

        name = selectedGreeter.Name;
        string portraitPath = selectedGreeter.PortraitPath;
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
        if (!_greetingActive)
            return;

        _greetingActive = false;
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.DialogueFinished -= OnGreetingFinished;
            _dialogue.QueueFree();
            _dialogue = null;
        }

        bool alreadyCleared = DayClock.Instance?.SchoolChallengeCompletedToday == true;
        if (alreadyCleared)
        {
            _leaveHint.Visible = AllowEscapeToLeave;
            return;
        }

        if (string.IsNullOrWhiteSpace(ChallengeScenePath) ||
            !ResourceLoader.Exists(ChallengeScenePath))
        {
            GD.PushWarning($"Classroom: assessment scene was not found: {ChallengeScenePath}");
            _leaveHint.Visible = AllowEscapeToLeave;
            return;
        }

        _leaveHint.Visible = false;
        if (ResourceLoader.Exists(ProfessorLessonScenePath))
        {
            ClassroomVox.SetNextChallengeScenePath(ChallengeScenePath);
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile(ProfessorLessonScenePath);
            else
                GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ProfessorLessonScenePath);
            return;
        }

        GD.PushWarning("Classroom: ClassroomVox.tscn is missing; proceeding directly to the assessment.");
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(ChallengeScenePath);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ChallengeScenePath);
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
