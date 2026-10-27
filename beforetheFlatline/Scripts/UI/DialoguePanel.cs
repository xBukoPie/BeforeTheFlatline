using Godot;
using System;

namespace SchoolYardGame;

public partial class DialoguePanel : Control
{
    private Label _nameLabel;
    private TextureRect _portrait;
    private RichTextLabel _textLabel;
    private Button _advanceButton;

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("PanelBackground/NameLabel");
        _portrait = GetNode<TextureRect>("PanelBackground/Portrait");
        _textLabel = GetNode<RichTextLabel>("PanelBackground/TextLabel");
        _advanceButton = GetNode<Button>("PanelBackground/AdvanceButton");
        _advanceButton.Pressed += OnAdvancePressed;
    }

    // /// Set the speaker's name.
    public void SetSpeaker(string name)
    {
        if (_nameLabel != null)
            _nameLabel.Text = name;
    }

    // /// Set the speaker's portrait texture.
    public void SetPortrait(Texture2D texture)
    {
        if (_portrait != null)
            _portrait.Texture = texture;
    }

    // /// Set the dialogue text (BBCode allowed).
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
        EmitSignal(nameof(DialogueAdvanced));
    }

    [Signal]
    public delegate void DialogueAdvancedEventHandler();

}
