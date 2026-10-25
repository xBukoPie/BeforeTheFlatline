using Godot;
using System;

namespace SchoolYardGame;

public partial class DialoguePanel : Control
{
    // UI nodes
    private Label _nameLabel;
    private TextureRect _portrait;
    private RichTextLabel _textLabel;
    private Button _advanceButton;

    public override void _Ready()
    {
        // Grab child nodes by name (they exist in the .tscn)
        _nameLabel = GetNode<Label>("PanelBackground/NameLabel");
        _portrait = GetNode<TextureRect>("PanelBackground/Portrait");
        _textLabel = GetNode<RichTextLabel>("PanelBackground/TextLabel");
        _advanceButton = GetNode<Button>("PanelBackground/AdvanceButton");
        _advanceButton.Pressed += OnAdvancePressed;
    }

    /// <summary>
    /// Set the speaker's name.
    /// </summary>
    public void SetSpeaker(string name)
    {
        if (_nameLabel != null)
            _nameLabel.Text = name;
    }

    /// <summary>
    /// Set the speaker's portrait texture.
    /// </summary>
    public void SetPortrait(Texture2D texture)
    {
        if (_portrait != null)
            _portrait.Texture = texture;
    }

    /// <summary>
    /// Set the dialogue text (BBCode allowed).
    /// </summary>
    public void SetDialogue(string bbcode)
    {
        if (_textLabel != null)
        {
            _textLabel.BbcodeEnabled = true;
            _textLabel.Text = bbcode; // using Text property for compatibility
        }
    }

    private void OnAdvancePressed()
    {
        // Simple placeholder – emit a signal so the game can handle it.
        EmitSignal(nameof(DialogueAdvanced));
    }

    // Declare a custom signal for external listeners.
    [Signal]
    public delegate void DialogueAdvancedEventHandler();

}
