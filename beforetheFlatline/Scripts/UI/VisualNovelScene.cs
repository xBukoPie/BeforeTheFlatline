using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SchoolYardGame;

public partial class VisualNovelScene : Control
{

    [Signal] public delegate void DialogueFinishedEventHandler();
    [Signal] public delegate void ChoiceSelectedEventHandler(int choiceIndex);
    [Signal] public delegate void MenuRequestedEventHandler();

    [ExportGroup("Text")]
    [Export] public float CharsPerSecond { get; set; } = 45f;
    [Export] public float AutoDelay { get; set; } = 1.4f;
    [Export] public float SkipDelay { get; set; } = 0.08f;

    [ExportGroup("Characters")]
    [Export] public Texture2D LeftPortrait { get; set; }
    [Export] public Texture2D RightPortrait { get; set; }

    [ExportGroup("Display")]
    [Export] public bool ShowBackground { get; set; } = true;
    [Export] public bool ShowPlaceholders { get; set; } = false;
    [Export] public bool ShowCharacterSlots { get; set; } = true;
    [Export] public bool AllowPauseMenu { get; set; } = true;
    [Export] public bool CompactChoiceMode { get; set; } = false;

    [ExportGroup("Demo")]
    [Export] public bool PlayDemoOnReady { get; set; } = true;
    [Export] public bool LoopDemo { get; set; } = true;

    private const string HintInteract = "Press [Space] or [E] to interact";
    private const string HintReplay = "Press [Space] to replay";
    private const float NameTagInset = 40f;
    private static readonly Color DimColor = new(0.55f, 0.55f, 0.6f, 1f);

    private Control _leftSlot, _rightSlot;
    private TextureRect _leftPortraitRect, _rightPortraitRect;
    private Control _leftPlaceholder, _rightPlaceholder;

    private PanelContainer _nameTag, _dialoguePanel, _logPanel, _sideButtonsBackdrop;
    private Control _sideButtons;
    private Label _nameLabel, _hint;
    private RichTextLabel _text, _logText;
    private VBoxContainer _choiceBox;
    private PanelContainer _choiceBackdrop;
    private Button _autoButton, _skipButton, _logButton, _optionsButton, _closeLogButton;

    private IReadOnlyList<DialogueLine> _lines;
    private int _index;
    private bool _lineActive;
    private bool _typing;
    private bool _choicesShowing;
    private bool _logOpen;
    private bool _auto;
    private bool _skip;
    private bool _nameTagPlaced;
    private double _idle;
    private DialogueSide _currentSide = DialogueSide.Right;
    private Tween _typeTween, _nameTagTween;
    private Control _pauseMenu;
    private bool _clockWasRunningBeforePause;
    private Control _uiRoot;
    private Control _dimOverlay;
    private CanvasLayer _foregroundCanvas;
    private bool _memoryTransitionBlocked;

    public Control DialogueInterfaceRoot => _uiRoot;

    public void SetMemoryTransitionBlocked(bool blocked) =>
        _memoryTransitionBlocked = blocked;

    // Raised whenever the active dialogue line changes; story cutscenes use this for synced visual cues.
    public event Action<int, DialogueLine> LinePresented;

    public override void _Ready()
    {
        // Memory dialogue is displayed while the world tree is paused.
        // Explicit Always mode keeps typewriter tweens and dialogue processing alive.
        if (!AllowPauseMenu)
            ProcessMode = ProcessModeEnum.Always;

        _leftSlot = GetNode<Control>("%LeftCharacter");
        _rightSlot = GetNode<Control>("%RightCharacter");
        _leftPortraitRect = GetNode<TextureRect>("%LeftPortraitRect");
        _rightPortraitRect = GetNode<TextureRect>("%RightPortraitRect");
        _leftPlaceholder = GetNode<Control>("%LeftPlaceholder");
        _rightPlaceholder = GetNode<Control>("%RightPlaceholder");

        _nameTag = GetNode<PanelContainer>("%NameTag");
        _nameLabel = GetNode<Label>("%NameLabel");
        _dialoguePanel = GetNode<PanelContainer>("%DialoguePanel");
        _text = GetNode<RichTextLabel>("%DialogueText");
        _hint = GetNode<Label>("%ContinueHint");
        _choiceBox = GetNode<VBoxContainer>("%ChoiceContainer");
        _uiRoot = GetNode<Control>("UI");
        _dimOverlay = GetNode<Control>("Dim");
        ConfigureChoiceLayout();

        _autoButton = GetNode<Button>("%AutoButton");
        _skipButton = GetNode<Button>("%SkipButton");
        _logButton = GetNode<Button>("%LogButton");
        _optionsButton = GetNode<Button>("%OptionsButton");
        _sideButtons = _uiRoot.GetNode<Control>("SideButtons");

        _logPanel = GetNode<PanelContainer>("%LogPanel");
        _logText = GetNode<RichTextLabel>("%LogText");
        _closeLogButton = GetNode<Button>("%CloseLogButton");

        if (CompactChoiceMode)
            ApplyMemoryDialogueTheme();

        GetNode<Control>("%Background").Visible = ShowBackground;
        // The dim overlay belongs to the scene background too. Leaving it visible
        // would darken the cutscene artwork even when its background is disabled.
        _dimOverlay.Visible = ShowBackground;
        _leftSlot.Visible = ShowCharacterSlots;
        _rightSlot.Visible = ShowCharacterSlots;
        _optionsButton.Visible = AllowPauseMenu;

        _dialoguePanel.GuiInput += OnDialoguePanelGuiInput;
        _dialoguePanel.Resized += OnDialoguePanelResized;
        _autoButton.Toggled += OnAutoToggled;
        _skipButton.Toggled += OnSkipToggled;
        _logButton.Pressed += OpenLog;
        _closeLogButton.Pressed += CloseLog;
        _optionsButton.Pressed += OnOptionsPressed;

        SetPortrait(DialogueSide.Left, LeftPortrait);
        SetPortrait(DialogueSide.Right, RightPortrait);

        _choiceBox.Visible = false;
        _logPanel.Visible = false;
        _hint.Visible = false;
        _text.Text = string.Empty;
        _nameLabel.Text = string.Empty;

        RaiseVisualsAboveVignette();
        VisibilityChanged += OnVisibilityChanged;
        OnVisibilityChanged();

        StartHintPulse();

        if (PlayDemoOnReady)
            CallDeferred(MethodName.StartDemo); // wait one frame so container layout is final
    }

    public override void _ExitTree()
    {
        VisibilityChanged -= OnVisibilityChanged;
        // Prevent a local dialogue pause from leaving the global status overlay hidden after this scene closes.
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
        StatusEffectsOverlay.Instance?.SetCinematicVignette(this, false);
    }

    private void RaiseVisualsAboveVignette()
    {
        _foregroundCanvas = new CanvasLayer
        {
            Name = "DialogueForegroundLayer",
            Layer = 110
        };
        AddChild(_foregroundCanvas);

        var foregroundRoot = new Control
        {
            Name = "DialogueForegroundRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = Theme
        };
        foregroundRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _foregroundCanvas.AddChild(foregroundRoot);

        // Keep the scene background and character art below the vignette.
        _uiRoot.GetParent().RemoveChild(_uiRoot);
        foregroundRoot.AddChild(_uiRoot);
    }

    private void OnVisibilityChanged()
    {
        bool visible = IsVisibleInTree();
        if (_foregroundCanvas != null && GodotObject.IsInstanceValid(_foregroundCanvas))
            _foregroundCanvas.Visible = visible;

        StatusEffectsOverlay.Instance?.SetCinematicVignette(this, visible);
    }

    public void SetPresentationVisible(bool visible)
    {
        Visible = visible;
        OnVisibilityChanged();
    }

    public override void _Process(double delta)
    {
        if (!_lineActive || _logOpen || (_pauseMenu != null && _pauseMenu.Visible))
            return;

        if (_choicesShowing)
        {
            if (_auto)
            {
                _idle += delta;
                if (_idle >= AutoDelay)
                {
                    var choices = _lines[_index].Choices;
                    if (choices is { Length: > 0 })
                        OnChoicePressed(0, choices[0]);
                }
            }
            return;
        }

        if (_typing)
            return;

        if (_auto)
        {
            _idle += delta;
            if (_idle >= AutoDelay)
                Advance();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && AllowPauseMenu)
        {
            if (_pauseMenu != null && _pauseMenu.Visible)
                ClosePauseMenu();
            else if (_logOpen)
                CloseLog();
            else
                OpenPauseMenu();

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("interact"))
        {
            HandleAdvanceInput();
            GetViewport().SetInputAsHandled();
        }
    }

    // Begin playing a list of lines from the start.
    public void StartDialogue(IReadOnlyList<DialogueLine> lines)
    {
        _lines = lines;
        _logText.Clear();
        ShowLine(0);
    }

    // Set (or clear with null) a character portrait.
    public void SetPortrait(DialogueSide side, Texture2D texture)
    {
        var rect = side == DialogueSide.Left ? _leftPortraitRect : _rightPortraitRect;
        var placeholder = side == DialogueSide.Left ? _leftPlaceholder : _rightPlaceholder;
        rect.Texture = texture;
        placeholder.Visible = texture == null && ShowPlaceholders;
    }

    private void ShowLine(int index)
    {
        if (_lines == null || index < 0 || index >= _lines.Count)
        {
            FinishDialogue();
            return;
        }

        _index = index;
        var line = _lines[index];
        _lineActive = true;
        LinePresented?.Invoke(index, line);
        _idle = 0;
        _currentSide = line.Side;

        HideChoices();
        _hint.Visible = false;
        _hint.Text = HintInteract;

        string resolvedSpeaker = ResolvePlayerNameTokens(line.Speaker);
        string resolvedText = ResolvePlayerNameTokens(line.Text);
        bool hideMemoryPointOfView = CompactChoiceMode &&
            (string.Equals(line.Speaker, "Memory", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(line.Speaker, "You", StringComparison.OrdinalIgnoreCase));

        _nameTag.Visible = !hideMemoryPointOfView;
        _nameLabel.Text = hideMemoryPointOfView ? string.Empty : resolvedSpeaker;
        if (!hideMemoryPointOfView)
        {
            _nameTag.ResetSize();
            PlaceNameTag(line.Side, animate: _nameTagPlaced);
        }
        _nameTagPlaced = true;
        HighlightSpeaker(line.Side);

        if (hideMemoryPointOfView)
            AppendLog($"{Escape(resolvedText)}\n\n");
        else
            AppendLog($"[b]{Escape(resolvedSpeaker)}[/b]\n{Escape(resolvedText)}\n\n");

        KillTween(ref _typeTween);
        _text.Text = resolvedText;
        _text.VisibleCharacters = 0;
        int total = _text.GetTotalCharacterCount();
        _typing = true;

        if (total <= 0)
        {
            FinishTyping();
            return;
        }

        float duration = total / Mathf.Max(CharsPerSecond, 1f);
        _typeTween = CreateTween();
        _typeTween.TweenProperty(_text, "visible_characters", total, duration);
        _typeTween.Finished += FinishTyping;
    }

    // Called when the text has fully appeared (naturally or after being completed early).
    private void FinishTyping()
    {
        _typing = false;
        _text.VisibleCharacters = -1;
        _idle = 0;

        var line = _lines[_index];
        if (line.Choices is { Length: > 0 })
            ShowChoices(line.Choices);
        else
            _hint.Visible = true;
    }

    private void CompleteTyping()
    {
        KillTween(ref _typeTween); // Kill() doesn't emit Finished, so we call FinishTyping ourselves.
        FinishTyping();
    }

    private void Advance()
    {
        var line = _lines[_index];
        ShowLine(line.Next >= 0 ? line.Next : _index + 1);
    }

    private void FinishDialogue()
    {
        _lineActive = false;
        _typing = false;
        HideChoices();

        _skip = false;
        _skipButton.SetPressedNoSignal(false);

        _hint.Text = HintReplay;
        _hint.Visible = LoopDemo;

        EmitSignal(SignalName.DialogueFinished);
    }

    // Space / Enter / click on the dialogue panel all land here.
    private void HandleAdvanceInput()
    {
        if (_memoryTransitionBlocked || _logOpen) return;

        if (!_lineActive)
        {
            if (LoopDemo && _lines != null) StartDialogue(_lines);
            return;
        }

        if (_typing) { CompleteTyping(); return; } // first press fills the line
        if (_choicesShowing) return;               // choices must be picked
        Advance();                                 // second press goes to the next line
    }

    private void ApplyMemoryDialogueTheme()
    {
        // Give the recollection dialogue more horizontal room and keep its controls
        // in a separate compact rail instead of letting them compete with the text.
        _dialoguePanel.AnchorLeft = 0f;
        _dialoguePanel.AnchorRight = 1f;
        _dialoguePanel.AnchorTop = 1f;
        _dialoguePanel.AnchorBottom = 1f;
        _dialoguePanel.OffsetLeft = 24f;
        _dialoguePanel.OffsetTop = -250f;
        _dialoguePanel.OffsetRight = -170f;
        _dialoguePanel.OffsetBottom = -24f;

        _dialoguePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.025f, 0.045f, 0.86f),
            BorderColor = new Color("77708f", 0.52f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 16,
            ContentMarginBottom = 16
        });

        _nameTag.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.025f, 0.045f, 0.9f),
            BorderColor = new Color("8c829f", 0.58f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        });

        _nameLabel.AddThemeColorOverride("font_color", new Color("ded8e8"));
        _nameLabel.AddThemeFontSizeOverride("font_size", 19);
        _text.AddThemeColorOverride("default_color", new Color("eeeaf2"));
        _text.AddThemeFontSizeOverride("normal_font_size", 22);
        _hint.AddThemeColorOverride("font_color", new Color("cbc4d7"));
        _hint.AddThemeFontSizeOverride("font_size", 14);

        _sideButtons.AnchorLeft = 1f;
        _sideButtons.AnchorRight = 1f;
        _sideButtons.AnchorTop = 1f;
        _sideButtons.AnchorBottom = 1f;
        _sideButtons.OffsetLeft = -150f;
        _sideButtons.OffsetTop = -250f;
        _sideButtons.OffsetRight = -24f;
        _sideButtons.OffsetBottom = -24f;
        _sideButtons.AddThemeConstantOverride("separation", 6);
        _sideButtons.ZIndex = 2;

        _sideButtonsBackdrop = new PanelContainer
        {
            Name = "MemorySideButtonsBackdrop",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1
        };
        _sideButtonsBackdrop.AnchorLeft = 1f;
        _sideButtonsBackdrop.AnchorRight = 1f;
        _sideButtonsBackdrop.AnchorTop = 1f;
        _sideButtonsBackdrop.AnchorBottom = 1f;
        _sideButtonsBackdrop.OffsetLeft = -160f;
        _sideButtonsBackdrop.OffsetTop = -260f;
        _sideButtonsBackdrop.OffsetRight = -14f;
        _sideButtonsBackdrop.OffsetBottom = -14f;
        _sideButtonsBackdrop.AddThemeStyleboxOverride("panel", CreateChoiceButtonStyle(
            new Color(0.025f, 0.025f, 0.045f, 0.82f),
            new Color("77708f", 0.56f)));
        _uiRoot.AddChild(_sideButtonsBackdrop);
        _uiRoot.MoveChild(_sideButtonsBackdrop, _sideButtons.GetIndex());

        foreach (Button button in new[] { _autoButton, _skipButton, _logButton, _optionsButton, _closeLogButton })
        {
            button.CustomMinimumSize = new Vector2(112f, 40f);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.AddThemeFontSizeOverride("font_size", 13);
            button.AddThemeColorOverride("font_color", new Color("e5dfed"));
            button.AddThemeColorOverride("font_focus_color", new Color("e5dfed"));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.AddThemeColorOverride("font_outline_color", new Color("100c18"));
            button.AddThemeConstantOverride("outline_size", 1);
            button.AddThemeStyleboxOverride("normal", CreateChoiceButtonStyle(
                new Color(0.035f, 0.035f, 0.06f, 0.72f), new Color("77708f", 0.5f)));
            button.AddThemeStyleboxOverride("hover", CreateChoiceButtonStyle(
                new Color(0.13f, 0.12f, 0.19f, 0.9f), new Color("9e91b5", 0.8f)));
            button.AddThemeStyleboxOverride("pressed", CreateChoiceButtonStyle(
                new Color(0.20f, 0.17f, 0.27f, 0.95f), new Color("b5a6ce", 0.9f)));
            button.AddThemeStyleboxOverride("focus", CreateChoiceButtonStyle(
                new Color(0.12f, 0.09f, 0.18f, 0.86f), new Color("b5a6ce", 0.9f)));
        }
    }

    private void ConfigureChoiceLayout()
    {
        if (!CompactChoiceMode)
            return;

        _choiceBox.AnchorLeft = 1f;
        _choiceBox.AnchorRight = 1f;
        _choiceBox.AnchorTop = 0.40f;
        _choiceBox.AnchorBottom = 0.40f;
        _choiceBox.OffsetLeft = -386f;
        _choiceBox.OffsetRight = -26f;
        _choiceBox.OffsetTop = -114f;
        _choiceBox.OffsetBottom = 114f;
        _choiceBox.AddThemeConstantOverride("separation", 6);
        _choiceBox.ZIndex = 2;
        _choiceBox.CustomMinimumSize = new Vector2(360f, 0f);

        _choiceBackdrop = new PanelContainer
        {
            Name = "CompactChoiceBackdrop",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZIndex = 1
        };
        _choiceBackdrop.AnchorLeft = 1f;
        _choiceBackdrop.AnchorRight = 1f;
        _choiceBackdrop.AnchorTop = 0.40f;
        _choiceBackdrop.AnchorBottom = 0.40f;
        _choiceBackdrop.OffsetLeft = -398f;
        _choiceBackdrop.OffsetRight = -14f;
        _choiceBackdrop.OffsetTop = -126f;
        _choiceBackdrop.OffsetBottom = 126f;
        _choiceBackdrop.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.028f, 0.055f, 0.70f),
            BorderColor = new Color(0.71f, 0.64f, 0.85f, 0.78f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });

        _uiRoot.AddChild(_choiceBackdrop);
        _uiRoot.MoveChild(_choiceBackdrop, _choiceBox.GetIndex());
    }

    private void ShowChoices(DialogueChoice[] choices)
    {
        _choicesShowing = true;
        if (_choiceBackdrop != null)
            _choiceBackdrop.Visible = true;
        _hint.Visible = false;

        _idle = 0;

        foreach (var child in _choiceBox.GetChildren())
        {
            _choiceBox.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < choices.Length; i++)
        {
            int captured = i;
            var button = new Button
            {
                Text = ResolvePlayerNameTokens(choices[i].Label),
                CustomMinimumSize = new Vector2(0, CompactChoiceMode ? 44 : 100),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All
            };
            button.AddThemeFontSizeOverride("font_size", CompactChoiceMode ? 17 : 30);
            if (CompactChoiceMode)
            {
                Color choiceText = new("f3edf9");
                button.AddThemeColorOverride("font_color", choiceText);
                button.AddThemeColorOverride("font_focus_color", choiceText);
                button.AddThemeColorOverride("font_hover_color", Colors.White);
                button.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
                button.AddThemeColorOverride("font_pressed_color", Colors.White);
                button.AddThemeColorOverride("font_disabled_color", new Color("a99fb7"));
                button.AddThemeColorOverride("font_outline_color", new Color("100c18"));
                button.AddThemeConstantOverride("outline_size", 1);
                button.AddThemeStyleboxOverride("normal", CreateChoiceButtonStyle(
                    new Color(0.07f, 0.055f, 0.10f, 0.66f), new Color(0.68f, 0.61f, 0.80f, 0.58f)));
                button.AddThemeStyleboxOverride("hover", CreateChoiceButtonStyle(
                    new Color(0.20f, 0.15f, 0.28f, 0.82f), new Color(0.82f, 0.75f, 0.94f, 0.92f)));
                button.AddThemeStyleboxOverride("pressed", CreateChoiceButtonStyle(
                    new Color(0.27f, 0.20f, 0.37f, 0.90f), new Color(0.90f, 0.83f, 1f, 1f)));
                button.AddThemeStyleboxOverride("focus", CreateChoiceButtonStyle(
                    new Color(0.12f, 0.09f, 0.18f, 0.86f), new Color(0.95f, 0.88f, 1f, 1f)));
            }
            button.Pressed += () => OnChoicePressed(captured, choices[captured]);
            _choiceBox.AddChild(button);
        }

        _choiceBox.Visible = true;
        _choiceBox.GetChild<Button>(0).GrabFocus(); // keyboard: W/S or arrows + Space
    }

    private static StyleBoxFlat CreateChoiceButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
    }

    private void HideChoices()
    {
        _choicesShowing = false;
        _choiceBox.Visible = false;
        if (_choiceBackdrop != null)
            _choiceBackdrop.Visible = false;
    }

    private void OnChoicePressed(int choiceIndex, DialogueChoice choice)
    {
        if (_memoryTransitionBlocked)
            return;

        EmitSignal(SignalName.ChoiceSelected, choiceIndex);
        AppendLog($"[color=#7a3f12]▶ {Escape(ResolvePlayerNameTokens(choice.Label))}[/color]\n\n");
        HideChoices();
        ShowLine(choice.Goto >= 0 ? choice.Goto : _index + 1);
    }

    private static string ResolvePlayerNameTokens(string text)
    {
        string playerName = SettingsManager.Instance?.PlayerName?.Trim();
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        return Regex.Replace(
            text ?? string.Empty,
            @"\{player(?:name)?\}",
            _ => playerName,
            RegexOptions.IgnoreCase);
    }

    // Slides the name tag above the left or right end of the dialogue panel.
    private void PlaceNameTag(DialogueSide side, bool animate)
    {
        var panelPos = _dialoguePanel.Position;
        var panelSize = _dialoguePanel.Size;
        var tagSize = _nameTag.Size;

        float x = side == DialogueSide.Left
            ? panelPos.X + NameTagInset
            : panelPos.X + panelSize.X - tagSize.X - NameTagInset;
        float y = panelPos.Y - tagSize.Y + 10f; // overlaps the panel border slightly
        var target = new Vector2(x, y);

        KillTween(ref _nameTagTween);
        if (!animate)
        {
            _nameTag.Position = target;
            return;
        }

        _nameTagTween = CreateTween();
        _nameTagTween.TweenProperty(_nameTag, "position", target, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void OnDialoguePanelResized() => PlaceNameTag(_currentSide, animate: false);

    // Speaker is full brightness, the listener is dimmed.
    private void HighlightSpeaker(DialogueSide side)
    {
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_leftSlot, "modulate", side == DialogueSide.Left ? Colors.White : DimColor, 0.2);
        tween.TweenProperty(_rightSlot, "modulate", side == DialogueSide.Right ? Colors.White : DimColor, 0.2);
    }

    private void StartHintPulse()
    {
        var pulse = CreateTween().SetLoops(); // loops forever
        pulse.TweenProperty(_hint, "modulate:a", 0.65f, 0.8).From(0.2f);
        pulse.TweenProperty(_hint, "modulate:a", 0.2f, 0.8);
    }

    private void OnAutoToggled(bool on)
    {
        _auto = on;
        _idle = 0;
    }

    private void OnSkipToggled(bool on)
    {
        _skip = false;
        _idle = 0;
        if (on)
            SkipEntireDialogue();
    }

    private void OpenLog()
    {
        if (_logOpen) return;
        _logOpen = true;
        _logPanel.Visible = true;
        if (_typeTween != null && _typeTween.IsValid()) _typeTween.Pause();
        _closeLogButton.GrabFocus();
    }

    private void CloseLog()
    {
        if (!_logOpen) return;
        _logOpen = false;
        _logPanel.Visible = false;
        if (_typeTween != null && _typeTween.IsValid()) _typeTween.Play();
        GetViewport().GuiReleaseFocus();
        if (_choicesShowing && _choiceBox.GetChildCount() > 0)
            _choiceBox.GetChild<Button>(0).GrabFocus();
    }

    private void OnOptionsPressed()
    {
        if (!AllowPauseMenu)
            return;

        OpenPauseMenu();
        EmitSignal(SignalName.MenuRequested);
    }

    private void OpenPauseMenu()
    {
        if (_pauseMenu != null && _pauseMenu.Visible)
            return;

        _clockWasRunningBeforePause = DayClock.Instance?.Running ?? false;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(true);

        if (_pauseMenu != null)
        {
            _pauseMenu.Visible = true;
            return;
        }

        _pauseMenu = new Control
        {
            Name = "DialoguePauseMenu",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _pauseMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_pauseMenu);

        var dim = new ColorRect
        {
            Color = new Color(0.025f, 0.02f, 0.045f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseMenu.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseMenu.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 470) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("171421"),
            BorderColor = new Color("74638f"),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 30, ContentMarginRight = 30,
            ContentMarginTop = 26, ContentMarginBottom = 26
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        panel.AddChild(stack);

        var heading = new Label
        {
            Text = "PAUSED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        heading.AddThemeFontSizeOverride("font_size", 28);
        heading.AddThemeColorOverride("font_color", new Color("e6d9ff"));
        stack.AddChild(heading);

        var brightnessLabel = new Label { Text = "Brightness" };
        brightnessLabel.AddThemeColorOverride("font_color", new Color("d4cbdc"));
        stack.AddChild(brightnessLabel);
        var brightness = new HSlider
        {
            MinValue = 0.65,
            MaxValue = 1.05,
            Step = 0.05,
            Value = SettingsManager.Instance?.Brightness ?? 0.88,
            CustomMinimumSize = new Vector2(420, 22)
        };
        brightness.ValueChanged += value => SettingsManager.Instance?.SetBrightness((float)value);
        stack.AddChild(brightness);

        var volumeLabel = new Label { Text = "Master volume" };
        volumeLabel.AddThemeColorOverride("font_color", new Color("d4cbdc"));
        stack.AddChild(volumeLabel);
        var volume = new HSlider
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Step = 0.05,
            Value = SettingsManager.Instance?.MasterVolume ?? 0.85,
            CustomMinimumSize = new Vector2(420, 22)
        };
        volume.ValueChanged += value => SettingsManager.Instance?.SetMasterVolume((float)value);
        stack.AddChild(volume);

        var resume = new Button { Text = "Resume", CustomMinimumSize = new Vector2(0, 44) };
        resume.Pressed += ClosePauseMenu;
        stack.AddChild(resume);

        var title = new Button { Text = "Return to Title", CustomMinimumSize = new Vector2(0, 44) };
        title.Pressed += () =>
        {
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Menu/Main.tscn");
        };
        stack.AddChild(title);

        _pauseMenu.Visible = true;
        resume.GrabFocus();
    }

    private void ClosePauseMenu()
    {
        if (_pauseMenu != null)
            _pauseMenu.Visible = false;

        if (DayClock.Instance != null)
            DayClock.Instance.Running = _clockWasRunningBeforePause;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
    }

    private void SkipEntireDialogue()
    {
        if (!_lineActive || _lines == null || _lines.Count == 0)
            return;

        var visited = new HashSet<int>();
        int cursor = _index;
        for (int step = 0; step < _lines.Count + 1; step++)
        {
            if (cursor < 0 || cursor >= _lines.Count || !visited.Add(cursor))
                break;

            var line = _lines[cursor];
            if (line.Choices is { Length: > 0 })
                cursor = line.Choices[0].Goto >= 0 ? line.Choices[0].Goto : cursor + 1;
            else
                cursor = line.Next >= 0 ? line.Next : cursor + 1;
        }

        KillTween(ref _typeTween);
        _typing = false;
        _text.VisibleCharacters = -1;
        HideChoices();
        FinishDialogue();
    }

    private void OnDialoguePanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            HandleAdvanceInput();
            GetViewport().SetInputAsHandled();
        }
    }

    private void AppendLog(string bbcode) => _logText.AppendText(bbcode);

    // Stops "[" in plain strings (names, choice labels) from being read as BBCode.
    private static string Escape(string s) => s.Replace("[", "[lb]");

    private static void KillTween(ref Tween tween)
    {
        if (tween != null && tween.IsValid()) tween.Kill();
        tween = null;
    }

    private void StartDemo() => StartDialogue(BuildDemoLines());

    private static List<DialogueLine> BuildDemoLines() => new()
    {
        new DialogueLine(
            "Erin",
            "Hey, could you maybe keep my ring in your wallet for now? I don't want it to fall off just because my sister has decided that this is the day I go crazy.",
            DialogueSide.Right,
            Choices: new[]
            {
                new DialogueChoice("Sure thing", Goto: 1),
                new DialogueChoice("If I have to", Goto: 2)
            }),

        new DialogueLine("Erin", "Thanks, you're a lifesaver!", DialogueSide.Right, Next: 3),

        new DialogueLine("Erin", "Ha, very funny. Just... please don't lose it.", DialogueSide.Right),

        new DialogueLine("You", "Don't worry. It's safe with me.", DialogueSide.Left),
        new DialogueLine("Erin", "Okay. Let's go before my sister changes her mind.", DialogueSide.Right),
    };
}
