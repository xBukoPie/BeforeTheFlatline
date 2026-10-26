using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

// This will check the player and triggers the friend greeting based on time.
public partial class SchoolGreeting : Node2D
{
    /// <summary>Emitted when the player advances past the school greeting dialogue.</summary>
    [Signal] public delegate void SchoolGreetingFinishedEventHandler();

    /// <summary>Challenge scene opened after the greeting is dismissed.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string ChallengeScenePath { get; set; } = "res://Scenes/Challenges/SequenceChallenge.tscn";

    private Control _activeGreetingPanel;
    private bool _greetingFinished;
    // List of characters that will greet the player with their portrait paths.
    private readonly (string name, string portraitPath)[] _characters = new[]
    {
        ("Mich", "res://Assets/Characters/Mich.png"),
        ("Jepoy", "res://Assets/Characters/Jepoy.png"),
        ("Cess", "res://Assets/Characters/Cess.png")
    };

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private PackedScene _dialoguePanelScene;

    public override void _Ready()
    {
        _dialoguePanelScene = GD.Load<PackedScene>("res://Scenes/UI/DialoguePanel.tscn");
        ShowRandomGreeting();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body.Name == "Player" || body.IsInGroup("player"))
        {
            ShowRandomGreeting();
        }
    }

    private void ShowRandomGreeting()
    {
        // Randomly select a character from the list
        int index = (int)_rng.RandiRange(0, _characters.Length - 1);
        var (charName, portraitPath) = _characters[index];

        string timeGreeting;
        float darkness = GameController.Darkness;
        if (darkness < 0.33f)
            timeGreeting = "Good morning!";
        else if (darkness < 0.66f)
            timeGreeting = "Good afternoon!";
        else
            timeGreeting = "Good evening!";

        var panelInstance = (Control)_dialoguePanelScene.Instantiate();
        _activeGreetingPanel = panelInstance;
        AddChild(panelInstance);

        if (panelInstance is DialoguePanel dialoguePanel)
            dialoguePanel.DialogueAdvanced += OnGreetingAdvanced;

        var portrait = panelInstance.GetNode<TextureRect>("PanelBackground/Portrait");
        portrait.Texture = GD.Load<Texture2D>(portraitPath);

        var nameLabel = panelInstance.GetNode<Label>("PanelBackground/NameLabel");
        nameLabel.Text = charName;
        var textLabel = panelInstance.GetNode<RichTextLabel>("PanelBackground/TextLabel");
        textLabel.BbcodeEnabled = true;
        textLabel.Text = $"[center]{charName} says: {timeGreeting}[/center]";
    }

    private void OnGreetingAdvanced()
    {
        if (_greetingFinished)
            return;

        _greetingFinished = true;
        if (IsInstanceValid(_activeGreetingPanel))
            _activeGreetingPanel.QueueFree();

        EmitSignal(SignalName.SchoolGreetingFinished);

        // Start the first challenge only after the greeting panel has been dismissed.
        if (!string.IsNullOrWhiteSpace(ChallengeScenePath) && ResourceLoader.Exists(ChallengeScenePath))
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ChallengeScenePath);
        else
            GD.PushError($"SchoolGreeting: Challenge scene not found: {ChallengeScenePath}");
    }
}
