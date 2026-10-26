using Godot;
using SchoolYardGame;

public partial class SchoolGreeting : Node2D
{
    [Signal]
    public delegate void SchoolGreetingFinishedEventHandler();

    [Export(PropertyHint.File, "*.tscn")]
    public string ChallengeScenePath { get; set; } = "res://Scenes/Challenges/SequenceChallenge.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string SchoolRoomPath { get; set; } = "res://Scenes/School/School.tscn";

    [Export]
    public string SchoolSpawn { get; set; } = "FromHome";

    private Control _activeGreetingPanel;
    private bool _greetingFinished;

    private readonly (string Name, string PortraitPath)[] _characters =
    {
        ("Mich", "res://Assets/Characters/Mich.png"),
        ("Jepoy", "res://Assets/Characters/Jepoy.png"),
        ("Cess", "res://Assets/Characters/Cess.png")
    };

    private readonly RandomNumberGenerator _rng = new();
    private PackedScene _dialoguePanelScene;

    public override void _Ready()
    {
        // On return from the post-challenge conversation, stay in the school room
        // without restarting the greeting and challenge chain.
        if (DayClock.Instance != null &&
            (DayClock.Instance.ConsumeSkipSchoolGreetingOnNextLoad() ||
             DayClock.Instance.SchoolChallengeStartedToday))
        {
            QueueFree();
            return;
        }

        _rng.Randomize();
        _dialoguePanelScene = GD.Load<PackedScene>("res://Scenes/UI/DialoguePanel.tscn");
        if (_dialoguePanelScene == null)
        {
            GD.PushError("SchoolGreeting: Could not load DialoguePanel.tscn.");
            return;
        }

        ShowRandomGreeting();
    }

    private void ShowRandomGreeting()
    {
        if (_greetingFinished || _activeGreetingPanel != null)
            return;

        int index = _rng.RandiRange(0, _characters.Length - 1);
        var (characterName, portraitPath) = _characters[index];

        string timeGreeting = GameController.Darkness switch
        {
            < 0.33f => "Good morning!",
            < 0.66f => "Good afternoon!",
            _ => "Good evening!"
        };

        var panelInstance = _dialoguePanelScene.Instantiate<Control>();
        _activeGreetingPanel = panelInstance;
        AddChild(panelInstance);

        if (panelInstance is DialoguePanel dialoguePanel)
            dialoguePanel.DialogueAdvanced += OnGreetingAdvanced;
        else
            GD.PushWarning("SchoolGreeting: DialoguePanel root does not use DialoguePanel.cs.");

        var portrait = panelInstance.GetNodeOrNull<TextureRect>("PanelBackground/Portrait");
        if (portrait != null)
            portrait.Texture = GD.Load<Texture2D>(portraitPath);

        var nameLabel = panelInstance.GetNodeOrNull<Label>("PanelBackground/NameLabel");
        if (nameLabel != null)
            nameLabel.Text = characterName;

        var textLabel = panelInstance.GetNodeOrNull<RichTextLabel>("PanelBackground/TextLabel");
        if (textLabel != null)
        {
            textLabel.BbcodeEnabled = true;
            textLabel.Text = $"[center]{characterName} says: {timeGreeting}[/center]";
        }
    }

    private void OnGreetingAdvanced()
    {
        if (_greetingFinished)
            return;

        _greetingFinished = true;
        if (IsInstanceValid(_activeGreetingPanel))
            _activeGreetingPanel.QueueFree();

        DayClock.Instance?.MarkSchoolChallengeStartedToday();
        global::RoomManager.RequestStartRoomOnNextLoad(SchoolRoomPath, SchoolSpawn);
        EmitSignal(SignalName.SchoolGreetingFinished);

        if (!string.IsNullOrWhiteSpace(ChallengeScenePath) &&
            ResourceLoader.Exists(ChallengeScenePath))
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ChallengeScenePath);
        }
        else
        {
            GD.PushError($"SchoolGreeting: Challenge scene not found: {ChallengeScenePath}");
        }
    }
}
