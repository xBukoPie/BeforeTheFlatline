using Godot;
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
    [Export] public bool ShowPlaceholders { get; set; } = true;

    [ExportGroup("Demo")]
    [Export] public bool PlayDemoOnReady { get; set; } = true;
    [Export] public bool LoopDemo { get; set; } = true;

    private const string HintInteract = "Press [Space] to interact";
    private const string HintReplay = "Press [Space] to replay";
    private const float NameTagInset = 40f;
    private static readonly Color DimColor = new(0.55f, 0.55f, 0.6f, 1f);

    private Control _leftSlot, _rightSlot;
    private TextureRect _leftPortraitRect, _rightPortraitRect;
    private Control _leftPlaceholder, _rightPlaceholder;

    private PanelContainer _nameTag, _dialoguePanel, _logPanel;
    private Label _nameLabel, _hint;
    private RichTextLabel _text, _logText;
    private VBoxContainer _choiceBox;
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

    public override void _Ready()
    {
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

        _autoButton = GetNode<Button>("%AutoButton");
        _skipButton = GetNode<Button>("%SkipButton");
        _logButton = GetNode<Button>("%LogButton");
        _optionsButton = GetNode<Button>("%OptionsButton");

        _logPanel = GetNode<PanelContainer>("%LogPanel");
        _logText = GetNode<RichTextLabel>("%LogText");
        _closeLogButton = GetNode<Button>("%CloseLogButton");

        GetNode<Control>("%Background").Visible = ShowBackground;

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

        StartHintPulse();

        if (PlayDemoOnReady)
            CallDeferred(MethodName.StartDemo); // wait one frame so container layout is final
    }

    public override void _ExitTree()
    {
        // Prevent a local dialogue pause from leaving the global status overlay hidden after this scene closes.
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
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
        if (@event.IsActionPressed("ui_cancel"))
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

        if (@event.IsActionPressed("ui_accept"))
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
        _idle = 0;
        _currentSide = line.Side;

        HideChoices();
        _hint.Visible = false;
        _hint.Text = HintInteract;

        string resolvedSpeaker = ResolvePlayerNameTokens(line.Speaker);
        string resolvedText = ResolvePlayerNameTokens(line.Text);
        _nameLabel.Text = resolvedSpeaker;
        _nameTag.ResetSize();
        PlaceNameTag(line.Side, animate: _nameTagPlaced);
        _nameTagPlaced = true;
        HighlightSpeaker(line.Side);

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
        if (_logOpen) return;

        if (!_lineActive)
        {
            if (LoopDemo && _lines != null) StartDialogue(_lines);
            return;
        }

        if (_typing) { CompleteTyping(); return; } // first press fills the line
        if (_choicesShowing) return;               // choices must be picked
        Advance();                                 // second press goes to the next line
    }

    private void ShowChoices(DialogueChoice[] choices)
    {
        _choicesShowing = true;
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
                CustomMinimumSize = new Vector2(0, 100),
                FocusMode = FocusModeEnum.All
            };
            button.AddThemeFontSizeOverride("font_size", 28);
            button.Pressed += () => OnChoicePressed(captured, choices[captured]);
            _choiceBox.AddChild(button);
        }

        _choiceBox.Visible = true;
        _choiceBox.GetChild<Button>(0).GrabFocus(); // keyboard: W/S or arrows + Space
    }

    private void HideChoices()
    {
        _choicesShowing = false;
        _choiceBox.Visible = false;
    }

    private void OnChoicePressed(int choiceIndex, DialogueChoice choice)
    {
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
