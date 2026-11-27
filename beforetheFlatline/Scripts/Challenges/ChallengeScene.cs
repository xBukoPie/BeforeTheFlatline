using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SchoolYardGame;

public partial class ChallengeScene : Control
{
    [Export] public string ChallengeId { get; set; } = "sequence";

    private VBoxContainer _main;
    private VBoxContainer _content;
    private ScrollContainer _contentScroll;
    private Label _feedback;
    private Label _progress;
    private TextEdit _codeEditor;
    private FlowchartDropCanvas _flowCanvas;
    private Button _submitButton;
    private Button _clearButton;
    private readonly Dictionary<string, Button> _paletteButtons = new(StringComparer.OrdinalIgnoreCase);

    private GeneratedChallenge _challenge;
    private string _activeChallengeId = "";
    private string _nextScene = "";
    private int _difficulty = 1;
    private int _day = 1;
    private bool _completed;

    private static readonly (string Type, string DefaultText)[] BlockPalette =
    {
        ("Input", "variable"),
        ("Process", "total = a + b"),
        ("Output", "result"),
        ("Decision", "condition")
    };

    public override void _Ready()
    {
        AddToGroup("challenge_scene");
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildShell();
        GenerateCurrentTask();
    }

    private void BuildShell()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect
        {
            Color = new Color("111827"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 38);
        margin.AddThemeConstantOverride("margin_right", 38);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        _main = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _main.AddThemeConstantOverride("separation", 10);
        margin.AddChild(_main);

        var top = new HBoxContainer();
        _main.AddChild(top);

        var banner = new Label { Text = "ALGORITHM LAB", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        banner.AddThemeFontSizeOverride("font_size", 16);
        banner.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        top.AddChild(banner);

        var leave = new Button { Text = "Leave Test" };
        leave.Pressed += () =>
        {
            if (DayClock.Instance != null)
                DayClock.Instance.Running = false;
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Challenges/ChallengeHub.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Challenges/ChallengeHub.tscn");
        };
        top.AddChild(leave);

        var title = new Label
        {
            Name = "ChallengeTitle",
            Text = "Generated challenge",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", Colors.White);
        _main.AddChild(title);

        var prompt = new Label
        {
            Name = "ChallengePrompt",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 62)
        };
        prompt.AddThemeFontSizeOverride("font_size", 18);
        prompt.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        _main.AddChild(prompt);

        _contentScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _main.AddChild(_contentScroll);

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 8);
        _contentScroll.AddChild(_content);

        _feedback = new Label
        {
            Text = "Your score appears after the final question.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 34)
        };
        _feedback.AddThemeFontSizeOverride("font_size", 16);
        _feedback.AddThemeColorOverride("font_color", new Color("94a3b8"));
        _main.AddChild(_feedback);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        _main.AddChild(footer);

        _progress = new Label
        {
            Text = "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _progress.AddThemeFontSizeOverride("font_size", 16);
        footer.AddChild(_progress);

        _clearButton = new Button { Text = "Clear Entry", CustomMinimumSize = new Vector2(105, 44) };
        _clearButton.Pressed += ResetChallenge;
        footer.AddChild(_clearButton);

        _submitButton = new Button { Text = "Give Up & Next", CustomMinimumSize = new Vector2(230, 44) };
        _submitButton.Pressed += OnSubmitPressed;
        footer.AddChild(_submitButton);
    }

    private void GenerateCurrentTask()
    {
        _day = DayClock.Instance?.DayNumber ?? 1;
        DailyTestSession.BeginIfNeeded(_day);

        _activeChallengeId = DailyTestSession.CurrentQuestionId;
        if (string.IsNullOrWhiteSpace(_activeChallengeId))
        {
            GD.PushError("ChallengeScene: the daily question order is empty. Resetting the assessment session.");
            DailyTestSession.Begin(_day);
            _activeChallengeId = DailyTestSession.CurrentQuestionId;
        }

        int failureAdjustment = SanityManager.Instance?.DifficultyPenalty ?? 0;
        _difficulty = Mathf.Clamp(_day + failureAdjustment - 1, 1, 5);

        var rng = new Random(unchecked((int)GD.Randi()));
        _challenge = GeneratedChallengeFactory.Generate(_activeChallengeId, _difficulty, rng);

        var title = _main.GetNode<Label>("ChallengeTitle");
        var prompt = _main.GetNode<Label>("ChallengePrompt");
        title.Text = _challenge.Title;
        prompt.Text = _challenge.Prompt;

        string nextQuestionId = DailyTestSession.NextQuestionId;
        _nextScene = string.IsNullOrWhiteSpace(nextQuestionId)
            ? "res://Scenes/School/TestResults.tscn"
            : GetChallengeScenePath(nextQuestionId);

        if (_challenge.IsFlowchart)
            RenderFlowchartEditor();
        else
            RenderPseudocodeEditor();

        int questionNumber = DailyTestSession.AttemptCount + 1;
        _progress.Text = $"QUESTION {questionNumber}/{DailyTestSession.QuestionCount}   •   DAY {_day}   •   DIFFICULTY {_difficulty}/5";
        UpdateSubmitButton();
        CallDeferred(nameof(BeginFirstDayQuestionGuide));
    }

    private void BeginFirstDayQuestionGuide()
    {
        if (_challenge == null)
            return;

        FirstDayTutorialManager.Instance?.ShowQuestionTutorial(
            _activeChallengeId,
            _challenge.IsFlowchart,
            _codeEditor,
            _flowCanvas,
            _paletteButtons,
            _submitButton);
    }

    private static string GetChallengeScenePath(string challengeId) =>
        challengeId.ToLowerInvariant() switch
        {
            "sequence" => "res://Scenes/Challenges/SequenceChallenge.tscn",
            "condition" => "res://Scenes/Challenges/ConditionChallenge.tscn",
            "flowchart" => "res://Scenes/Challenges/FlowchartChallenge.tscn",
            "final" => "res://Scenes/Challenges/FinalChallenge.tscn",
            _ => "res://Scenes/School/TestResults.tscn"
        };

    private void RenderPseudocodeEditor()
    {
        if (_contentScroll != null)
            _contentScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        ClearContent();
        _paletteButtons.Clear();
        var guide = new Label
        {
            Text = "Write pseudocode that solves the problem. Use any variable names you like.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        guide.AddThemeFontSizeOverride("font_size", 18);
        guide.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _content.AddChild(guide);

        _codeEditor = new TextEdit
        {
            Name = "PseudocodeEditor",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 300),
            PlaceholderText = "START\nINPUT value\n...\nEND",
            ScrollFitContentHeight = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        _codeEditor.AddThemeFontSizeOverride("font_size", 17);
        _codeEditor.AddThemeColorOverride("font_color", Colors.White);
        _codeEditor.AddThemeColorOverride("background_color", new Color("0f172a"));
        _codeEditor.TextChanged += UpdateSubmitButton;
        _content.AddChild(_codeEditor);
    }

    private void RenderFlowchartEditor()
    {
        if (_contentScroll != null)
            _contentScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        ClearContent();
        _paletteButtons.Clear();

        var guide = new Label
        {
            Text = "Build a connected flowchart. Start and End are built in. Drag blocks onto connectors; drag a placed block outside the workspace to delete it."
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        guide.AddThemeFontSizeOverride("font_size", 18);
        guide.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _content.AddChild(guide);

        var palette = new HFlowContainer();
        palette.AddThemeConstantOverride("h_separation", 8);
        palette.AddThemeConstantOverride("v_separation", 8);
        _content.AddChild(palette);

        foreach (var item in BlockPalette)
        {
            var button = new ChallengeDragBlockButton
            {
                Text = $"{item.Type}  +",
                BlockType = item.Type,
                BlockName = item.DefaultText,
                CustomMinimumSize = new Vector2(118, 42),
                TooltipText = $"Drag to add {item.Type.ToLowerInvariant()} block"
            };
            palette.AddChild(button);
            _paletteButtons[item.Type] = button;
        }

        var canvasTitle = new Label { Text = "FLOWCHART WORKSPACE" };
        canvasTitle.AddThemeFontSizeOverride("font_size", 16);
        canvasTitle.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _content.AddChild(canvasTitle);

        var viewToolbar = new HBoxContainer();
        viewToolbar.AddThemeConstantOverride("separation", 8);
        _content.AddChild(viewToolbar);

        var panHint = new Label
        {
            Text = "Drag empty space to pan • Scroll wheel to zoom • Drag a block outside to delete"
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        panHint.AddThemeColorOverride("font_color", new Color("94a3b8"));
        viewToolbar.AddChild(panHint);

        var zoomOut = new Button { Text = "−", CustomMinimumSize = new Vector2(42, 34) };
        zoomOut.Pressed += () => _flowCanvas?.ZoomOut();
        viewToolbar.AddChild(zoomOut);

        var zoomReset = new Button { Text = "Reset View", CustomMinimumSize = new Vector2(92, 34) };
        zoomReset.Pressed += () => _flowCanvas?.ResetView();
        viewToolbar.AddChild(zoomReset);

        var zoomIn = new Button { Text = "+", CustomMinimumSize = new Vector2(42, 34) };
        zoomIn.Pressed += () => _flowCanvas?.ZoomIn();
        viewToolbar.AddChild(zoomIn);

        _flowCanvas = new FlowchartDropCanvas
        {
            CustomMinimumSize = new Vector2(0, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        _flowCanvas.ContentChanged += UpdateSubmitButton;
        _content.AddChild(_flowCanvas);

        var clear = new Button { Text = "Clear flowchart", CustomMinimumSize = new Vector2(130, 36) };
        clear.Pressed += () =>
        {
            _flowCanvas.ClearBlocks();
            _feedback.Text = "User-added blocks cleared. The built-in Start and End nodes remain.";
        };
        _content.AddChild(clear);
    }

    private void OnSubmitPressed()
    {
        if (_completed || _challenge == null)
            return;

        bool hasInput = HasAnyInput();
        bool complete = hasInput && HasCompleteAnswer();
        RecordAndContinue(gaveUp: !hasInput, incomplete: hasInput && !complete);
    }

    private void RecordAndContinue(bool gaveUp, bool incomplete = false)
    {
        if (_completed || _challenge == null)
            return;

        bool correct = !gaveUp && !incomplete &&
            (_challenge.IsFlowchart ? CheckFlowchartAnswer() : CheckPseudocodeAnswer());
        DailyTestSession.Record(_activeChallengeId, correct, gaveUp, _difficulty);

        if (correct)
            AwardCorrectChallengeProgress();

        _completed = true;
        _submitButton.Disabled = true;
        _clearButton.Disabled = true;

        _feedback.Text = gaveUp
            ? "Skipped. Moving to the next question."
            : incomplete
                ? "Incomplete answer submitted. Moving to the next question."
                : "Submitted. Moving to the next question.";
        _feedback.AddThemeColorOverride("font_color", new Color("cbd5e1"));

        if (!ResourceLoader.Exists(_nextScene))
        {
            _feedback.Text = $"Next scene was not found: {_nextScene}";
            _feedback.AddThemeColorOverride("font_color", new Color("fca5a5"));
            return;
        }

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(_nextScene);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, _nextScene);
    }

    private void AwardCorrectChallengeProgress()
    {
        // Only the first correct completion of this challenge family for the day
        // contributes hidden story points.
        if (!ChallengeProgress.MarkCompleted(_activeChallengeId))
            return;

        var reward = RoomKeyProgression.Instance?.AwardTaskCompletion(_difficulty);
        if (reward == null)
            return;

        DailyTestSession.AddPointsReceived(reward.PointsAdded);
        GD.Print($"[ChallengeScene] First correct completion earned {reward.PointsAdded} hidden story points. Total: {reward.TotalPoints}.");
    }

    public bool SuperUserCompleteAssessment()
    {
        if (_completed || _challenge == null || !DailyTestSession.IsActive)
            return false;

        bool hasInput = HasAnyInput();
        bool complete = hasInput && HasCompleteAnswer();
        bool correct = complete &&
            (_challenge.IsFlowchart ? CheckFlowchartAnswer() : CheckPseudocodeAnswer());

        DailyTestSession.Record(_activeChallengeId, correct, !hasInput, _difficulty);
        if (correct)
            AwardCorrectChallengeProgress();

        // Record all outstanding questions as skipped. The shortcut therefore
        // produces a valid 4-question results summary without inventing scores.
        while (DailyTestSession.AttemptCount < DailyTestSession.QuestionCount)
        {
            string nextId = DailyTestSession.CurrentQuestionId;
            if (string.IsNullOrWhiteSpace(nextId))
                break;

            DailyTestSession.Record(nextId, false, true, _difficulty);
        }

        _completed = true;
        if (_submitButton != null) _submitButton.Disabled = true;
        if (_clearButton != null) _clearButton.Disabled = true;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        const string resultsPath = "res://Scenes/School/TestResults.tscn";
        if (!ResourceLoader.Exists(resultsPath))
            return false;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(resultsPath);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, resultsPath);

        return true;
    }

    public bool SuperUserSolveCurrentQuestion()
    {
        if (_completed || _challenge == null)
            return false;

        if (_challenge.IsFlowchart)
        {
            if (_flowCanvas == null)
                return false;
            _flowCanvas.SetDefinitions(_challenge.FlowchartSolution);
        }
        else
        {
            if (_codeEditor == null || _challenge.PseudocodeSolution.Length == 0)
                return false;
            _codeEditor.Text = string.Join("\n", _challenge.PseudocodeSolution);
        }

        UpdateSubmitButton();
        _feedback.Text = "SUPERUSER: solution filled in. Review it, then press Submit & Next when ready.";
        _feedback.AddThemeColorOverride("font_color", new Color("86efac"));
        return true;
    }

    private bool CheckPseudocodeAnswer()
    {
        return _codeEditor != null &&
               AlgorithmLogicEvaluator.EvaluatePseudocode(_codeEditor.Text, _challenge);
    }

    private bool CheckFlowchartAnswer()
    {
        return _flowCanvas != null &&
               AlgorithmLogicEvaluator.EvaluateFlowchart(_flowCanvas.GetDefinitions(), _challenge);
    }

    private void ResetChallenge()
    {
        if (_completed)
            return;

        _feedback.Text = "Entry cleared. Submit or give up when you're ready to continue.";
        if (_challenge.IsFlowchart)
            _flowCanvas?.ClearBlocks();
        else if (_codeEditor != null)
            _codeEditor.Text = "";

        UpdateSubmitButton();
    }

    private bool HasAnyInput()
    {
        if (_challenge?.IsFlowchart == true)
            return _flowCanvas != null && _flowCanvas.GetDefinitions().Count > 0;

        return !string.IsNullOrWhiteSpace(_codeEditor?.Text);
    }

    private bool HasCompleteAnswer()
    {
        if (_challenge?.IsFlowchart == true)
            return _flowCanvas != null &&
                   FlowchartDefinitionTools.IsComplete(_flowCanvas.GetDefinitions());

        if (_codeEditor == null)
            return false;

        string[] lines = _codeEditor.Text
            .Replace("\r", "")
            .Split('\n');
        var meaningful = new List<string>();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("#"))
                continue;
            meaningful.Add(line);
        }

        if (meaningful.Count < 4)
            return false;

        string first = meaningful[0].ToUpperInvariant();
        string last = meaningful[meaningful.Count - 1].ToUpperInvariant();
        if (first != "START" && first != "BEGIN" && first != "BEGIN PROGRAM")
            return false;
        if (last != "END" && last != "STOP" && last != "FINISH" && last != "STOP PROGRAM")
            return false;

        bool hasInputLine = false;
        bool hasOutputLine = false;
        int ifDepth = 0;
        int loopDepth = 0;
        int forDepth = 0;

        foreach (string raw in meaningful)
        {
            string line = raw.ToUpperInvariant();
            hasInputLine |= line.StartsWith("INPUT ") || line.StartsWith("READ ") ||
                            line.StartsWith("GET ") || line.StartsWith("ACCEPT ");
            hasOutputLine |= line.StartsWith("OUTPUT ") || line.StartsWith("PRINT ") ||
                             line.StartsWith("DISPLAY ") || line.StartsWith("WRITE ");

            if (line.StartsWith("IF ") || line.StartsWith("IF("))
                ifDepth++;
            else if (line == "END IF" || line == "ENDIF")
                ifDepth--;

            if (line.StartsWith("WHILE ") || line.StartsWith("WHILE("))
                loopDepth++;
            else if (line == "END WHILE" || line == "ENDWHILE")
                loopDepth--;

            if (line.StartsWith("FOR "))
                forDepth++;
            else if (line == "NEXT" || line.StartsWith("NEXT ") ||
                     line == "END FOR" || line == "ENDFOR")
                forDepth--;

            if (ifDepth < 0 || loopDepth < 0 || forDepth < 0)
                return false;
        }

        return hasInputLine && hasOutputLine &&
               ifDepth == 0 && loopDepth == 0 && forDepth == 0;
    }

    private void UpdateSubmitButton()
    {
        if (_submitButton == null || _completed)
            return;

        bool hasInput = HasAnyInput();
        bool complete = hasInput && HasCompleteAnswer();

        _submitButton.Text = !hasInput
            ? "Give Up & Next"
            : complete
                ? "Submit & Next"
                : "Submit Incomplete Answer & Next";

        float width = !hasInput ? 170f : complete ? 180f : 280f;
        _submitButton.CustomMinimumSize = new Vector2(width, 44f);
    }

    private void ClearContent()
    {
        foreach (Node child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
        _codeEditor = null;
        _flowCanvas = null;
        _paletteButtons.Clear();
    }
}
