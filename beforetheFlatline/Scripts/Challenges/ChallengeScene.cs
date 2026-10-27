using Godot;
using System;
using System.Collections.Generic;
using SchoolYardGame;

public partial class ChallengeScene : Control
{
    [Export] public string ChallengeId { get; set; } = "sequence";

    private VBoxContainer _main;
    private VBoxContainer _content;
    private Label _feedback;
    private Label _progress;
    private TextEdit _codeEditor;
    private FlowchartDropCanvas _flowCanvas;
    private Button _submitButton;
    private Button _giveUpButton;
    private Button _clearButton;

    private GeneratedChallenge _challenge;
    private string _nextScene = "";
    private int _difficulty = 1;
    private int _day = 1;
    private bool _completed;

    private static readonly (string Type, string DefaultText)[] BlockPalette =
    {
        ("Start", "START"),
        ("Input", "INPUT variable"),
        ("Process", "total = a + b"),
        ("Output", "OUTPUT value"),
        ("Decision", "condition?"),
        ("End", "END")
    };

    public override void _Ready()
    {
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
        banner.AddThemeFontSizeOverride("font_size", 14);
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
        prompt.AddThemeFontSizeOverride("font_size", 16);
        prompt.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        _main.AddChild(prompt);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _main.AddChild(scroll);

        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_content);

        _feedback = new Label
        {
            Text = "Your score will be revealed after the final question.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 34)
        };
        _feedback.AddThemeFontSizeOverride("font_size", 14);
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
        footer.AddChild(_progress);

        _clearButton = new Button { Text = "Clear Entry", CustomMinimumSize = new Vector2(105, 44) };
        _clearButton.Pressed += ResetChallenge;
        footer.AddChild(_clearButton);

        _giveUpButton = new Button { Text = "Give Up & Next", CustomMinimumSize = new Vector2(142, 44) };
        _giveUpButton.Pressed += () => RecordAndContinue(gaveUp: true);
        footer.AddChild(_giveUpButton);

        _submitButton = new Button { Text = "Submit & Next", CustomMinimumSize = new Vector2(148, 44) };
        _submitButton.Pressed += () => RecordAndContinue(gaveUp: false);
        footer.AddChild(_submitButton);
    }

    private void GenerateCurrentTask()
    {
        _day = DayClock.Instance?.DayNumber ?? 1;

        if (ChallengeId.Equals("sequence", StringComparison.OrdinalIgnoreCase))
            DailyTestSession.Begin(_day);
        else
            DailyTestSession.BeginIfNeeded(_day);

        int failureAdjustment = SanityManager.Instance?.DifficultyPenalty ?? 0;
        _difficulty = Mathf.Clamp(_day + failureAdjustment - 1, 1, 5);

        var rng = new Random(unchecked((int)GD.Randi()));
        _challenge = GeneratedChallengeFactory.Generate(ChallengeId, _difficulty, rng);

        var title = _main.GetNode<Label>("ChallengeTitle");
        var prompt = _main.GetNode<Label>("ChallengePrompt");
        title.Text = _challenge.Title;
        prompt.Text = _challenge.Prompt;

        _nextScene = ChallengeId.ToLowerInvariant() switch
        {
            "sequence" => "res://Scenes/Challenges/ConditionChallenge.tscn",
            "condition" => "res://Scenes/Challenges/FlowchartChallenge.tscn",
            "flowchart" => "res://Scenes/Challenges/FinalChallenge.tscn",
            _ => "res://Scenes/School/TestResults.tscn"
        };

        if (_challenge.IsFlowchart)
            RenderFlowchartEditor();
        else
            RenderPseudocodeEditor();

        int questionNumber = ChallengeId.ToLowerInvariant() switch
        {
            "sequence" => 1,
            "condition" => 2,
            "flowchart" => 3,
            _ => 4
        };
        _progress.Text = $"QUESTION {questionNumber}/4   •   DAY {_day}   •   DIFFICULTY {_difficulty}/5";
    }

    private void RenderPseudocodeEditor()
    {
        ClearContent();
        var guide = new Label
        {
            Text = "TYPE YOUR OWN PSEUDOCODE. Choose any valid variable names and use them consistently; the example names in the prompt are optional. Submit or give up to continue; individual answers are not revealed yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
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
        _content.AddChild(_codeEditor);
    }

    private void RenderFlowchartEditor()
    {
        ClearContent();
        var guide = new Label
        {
            Text = "DRAG A SHAPE INTO THE CANVAS. You may choose your own valid variable names; keep them consistent in Input, Process, Decision, and Output blocks. Use ↑ / ↓ to order the connected blocks.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
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
        }

        var canvasTitle = new Label { Text = "CONNECTED FLOWCHART" };
        canvasTitle.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _content.AddChild(canvasTitle);

        _flowCanvas = new FlowchartDropCanvas
        {
            CustomMinimumSize = new Vector2(0, 210),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _flowCanvas.BlockDropped += AddFlowchartBlock;
        _content.AddChild(_flowCanvas);

        var clear = new Button { Text = "Clear flowchart", CustomMinimumSize = new Vector2(130, 36) };
        clear.Pressed += () =>
        {
            _flowCanvas.ClearBlocks();
            _feedback.Text = "Your score will be revealed at the end of the test.";
        };
        _content.AddChild(clear);
    }

    private void AddFlowchartBlock(string type, string defaultText)
    {
        _flowCanvas?.AddBlock(type, defaultText);
    }

    private void RecordAndContinue(bool gaveUp)
    {
        if (_completed || _challenge == null)
            return;

        bool correct = !gaveUp && (_challenge.IsFlowchart ? CheckFlowchartAnswer() : CheckPseudocodeAnswer());
        DailyTestSession.Record(ChallengeId, correct, gaveUp, _difficulty);

        if (correct)
            ChallengeProgress.MarkCompleted(ChallengeId);

        _completed = true;
        _submitButton.Disabled = true;
        _giveUpButton.Disabled = true;
        _clearButton.Disabled = true;

        _feedback.Text = gaveUp
            ? "Question skipped. Moving to the next question; the overall result appears at the end."
            : "Response recorded. Moving to the next question; the overall result appears at the end.";
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
    }
}
