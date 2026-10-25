using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Detects when the player enters the school yard and shows a random greeting
/// using the DialoguePanel. The greeting changes based on time of day.
/// </summary>
public partial class SchoolGreeting : Node2D
{
    // List of character data (name and portrait path)
    private readonly (string name, string portraitPath)[] _characters = new[]
    {
        ("Mich", "res://Assets/Characters/Mich.png"),
        ("Jepoy", "res://Assets/Characters/Jepoy.png"),
        ("Cess", "res://Assets/Characters/Cess.png")
    };

    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

    // Reference to the DialoguePanel scene
    private PackedScene _dialoguePanelScene;

    public override void _Ready()
    {
        // Load the DialoguePanel scene once
        _dialoguePanelScene = GD.Load<PackedScene>("res://Scenes/UI/DialoguePanel.tscn");
        // Trigger greeting immediately when the scene is instantiated
        ShowRandomGreeting();
    }

    private void OnBodyEntered(Node2D body)
    {
        // Assuming the player node is named "Player" or has a "player" group.
        if (body.Name == "Player" || body.IsInGroup("player"))
        {
            ShowRandomGreeting();
        }
    }

    private void ShowRandomGreeting()
    {
        // Pick a random character
        int index = (int)_rng.RandiRange(0, _characters.Length - 1);
        var (charName, portraitPath) = _characters[index];

        // Determine time‑based greeting using DayNightCycle.Darkness (0 = day, 1 = night)
        string timeGreeting;
        float darkness = DayNightCycle.Darkness; // static property
        if (darkness < 0.33f)
            timeGreeting = "Good morning!";
        else if (darkness < 0.66f)
            timeGreeting = "Good afternoon!";
        else
            timeGreeting = "Good evening!";

        // Instantiate the dialogue panel
        var panelInstance = (Control)_dialoguePanelScene.Instantiate();
        AddChild(panelInstance);

        // Set the portrait texture
        var portrait = panelInstance.GetNode<TextureRect>("PanelBackground/Portrait");
        portrait.Texture = GD.Load<Texture2D>(portraitPath);

        // Set speaker name and dialogue text
        var nameLabel = panelInstance.GetNode<Label>("PanelBackground/NameLabel");
        nameLabel.Text = charName;
        var textLabel = panelInstance.GetNode<RichTextLabel>("PanelBackground/TextLabel");
        textLabel.BbcodeEnabled = true;
        textLabel.Text = $"[center]{charName} says: {timeGreeting}[/center]";
    }
}