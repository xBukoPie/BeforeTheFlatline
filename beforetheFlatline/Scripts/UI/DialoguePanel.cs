using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

public partial class DialoguePanel : Control
{
    private Label _nameLabel;
    private TextureRect _portrait;
    private RichTextLabel _textLabel;
    private Button _advanceButton;
    private CanvasLayer _foregroundCanvas;

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("PanelBackground/NameLabel");
        _portrait = GetNode<TextureRect>("PanelBackground/Portrait");
        _textLabel = GetNode<RichTextLabel>("PanelBackground/TextLabel");
        _advanceButton = GetNode<Button>("PanelBackground/AdvanceButton");
        _advanceButton.Pressed += OnAdvancePressed;

        RaisePanelAboveVignette();
        VisibilityChanged += OnVisibilityChanged;
        OnVisibilityChanged();
    }

    public override void _ExitTree()
    {
        VisibilityChanged -= OnVisibilityChanged;
        StatusEffectsOverlay.Instance?.SetCinematicVignette(this, false);
    }

    private void RaisePanelAboveVignette()
    {
        var originalChildren = new List<Node>();
        foreach (Node child in GetChildren())
            originalChildren.Add(child);

        _foregroundCanvas = new CanvasLayer
        {
            Name = "DialoguePanelForegroundLayer",
            Layer = 110
        };
        AddChild(_foregroundCanvas);

        var foregroundRoot = new Control
        {
            Name = "DialoguePanelForegroundRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = Theme
        };
        foregroundRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _foregroundCanvas.AddChild(foregroundRoot);

        foreach (Node child in originalChildren)
        {
            RemoveChild(child);
            foregroundRoot.AddChild(child);
        }
    }

    private void OnVisibilityChanged()
    {
        bool visible = IsVisibleInTree();
        if (_foregroundCanvas != null && GodotObject.IsInstanceValid(_foregroundCanvas))
            _foregroundCanvas.Visible = visible;

        StatusEffectsOverlay.Instance?.SetCinematicVignette(this, visible);
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
