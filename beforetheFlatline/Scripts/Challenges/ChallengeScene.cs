using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SchoolYardGame;

/// <summary>
/// Runtime-generated challenge. Pseudocode tasks require typed solutions; flowchart tasks
/// require dragging special symbols onto the canvas and defining their text.
/// </summary>
public partial class ChallengeScene : Control
{
    [Export] public string ChallengeId { get; set; } = "sequence";

    private VBoxContainer _main;
    private VBoxContainer _content;
    private Label _feedback;
    private Label _progress;
    private TextEdit _codeEditor;
    private FlowchartDropCanvas _flowCanvas;
    private Button _checkButton;
    private Button _resetButton;
    private Button _continueButton;

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
        // Clock must remain stopped through school challenge tasks and their transition UI.
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
            MouseFilter = MouseFilterEnum.Ignore
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

        var banner = new Label
        {
            Text = "ALGORITHM LAB",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        banner.AddThemeFontSizeOverride("font_size", 14);
        banner.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        top.AddChild(banner);

        var exit = new Button { Text = "Back to Challenges" };
        exit.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Challenges/ChallengeHub.tscn");
        top.AddChild(exit);

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
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 34)
        };
        _feedback.AddThemeFontSizeOverride("font_size", 15);
        _main.AddChild(_feedback);

        var footer = new HBoxContainer();
        _main.AddChild(footer);

        _progress = new Label
        {
            Text = "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        footer.AddChild(_progress);

        _checkButton = new Button { Text = "Check Answer", CustomMinimumSize = new Vector2(150, 44) };
        _checkButton.Pressed += CheckAnswer;
        footer.AddChild(_checkButton);

        _resetButton = new Button { Text = "Reset", CustomMinimumSize = new Vector2(90, 44) };
        _resetButton.Pressed += ResetChallenge;
        footer.AddChild(_resetButton);

        _continueButton = new Button { Text = "Continue", CustomMinimumSize = new Vector2(130, 44), Visible = false };
        _continueButton.Pressed += ContinueToNextScene;
        footer.AddChild(_continueButton);
    }

    private void GenerateCurrentTask()
    {
        _day = DayClock.Instance?.DayNumber ?? 1;
        _difficulty = Mathf.Clamp(_day, 1, 5);
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
            _ => "res://Scenes/School/PostChallengeConversation.tscn"
        };

        if (_challenge.IsFlowchart)
            RenderFlowchartEditor();
        else
            RenderPseudocodeEditor();

        _progress.Text = $"Day {_day}  •  Difficulty {_difficulty}/5  •  {_challenge.Concept}";
    }

    private void RenderPseudocodeEditor()
    {
        ClearContent();
        var guide = new Label
        {
            Text = "TYPE YOUR SOLUTION BELOW. Use one instruction per line. The reference solution is not shown.",
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
            Text = "DRAG A SHAPE INTO THE CANVAS. Edit its definition, then use ↑ / ↓ to order the connected blocks.",
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
            MouseFilter = MouseFilterEnum.Stop
        };
        _flowCanvas.BlockDropped += AddFlowchartBlock;
        _content.AddChild(_flowCanvas);

        var clear = new Button { Text = "Clear flowchart", CustomMinimumSize = new Vector2(130, 36) };
        clear.Pressed += () =>
        {
            _flowCanvas.ClearBlocks();
            _feedback.Text = "";
        };
        _content.AddChild(clear);
    }

    private void AddFlowchartBlock(string type, string defaultText)
    {
        _flowCanvas?.AddBlock(type, defaultText);
        _feedback.Text = "";
    }

    private void CheckAnswer()
    {
        if (_completed || _challenge == null)
            return;

        bool correct = _challenge.IsFlowchart
            ? CheckFlowchartAnswer()
            : CheckPseudocodeAnswer();

        if (!correct)
        {
            _feedback.Text = _challenge.IsFlowchart
                ? "Not quite. Check the symbol types, each block's definition, and the order of the connected blocks."
                : "Not quite. Check your input/output steps, operators, condition boundaries, and loop order, then try again.";
            _feedback.AddThemeColorOverride("font_color", new Color("fca5a5"));
            return;
        }

        _completed = true;
        _checkButton.Disabled = true;
        _resetButton.Disabled = true;
        _continueButton.Visible = true;

        bool newDailyCategory = ChallengeProgress.MarkCompleted(ChallengeId);
        var reward = RoomKeyProgression.Instance?.AwardTaskCompletion(_difficulty);

        var messages = new List<string> { "Correct! Task completed." };
        if (reward != null && reward.KeysEarned > 0)
        {
            string keyWord = reward.KeysEarned == 1 ? "key" : "keys";
            string unlocked = string.Join(", ", reward.RoomsUnlocked);
            messages.Add($"You received {reward.KeysEarned} room {keyWord}! Unlocked: {unlocked}.");
        }
        else
        {
            messages.Add("Keep completing tasks to earn keys for new rooms.");
        }

        if (newDailyCategory)
            messages.Add("Daily challenge milestone recorded.");

        _feedback.Text = string.Join("\n", messages);
        _feedback.AddThemeColorOverride("font_color", new Color("86efac"));
        _continueButton.Text = ChallengeId.ToLowerInvariant() == "final" ? "Continue" : "Next Task";
    }

    private bool CheckPseudocodeAnswer()
    {
        if (_codeEditor == null)
            return false;

        var entered = _codeEditor.Text
            .Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeStatement)
            .Where(line => line.Length > 0)
            .ToArray();

        var expected = _challenge.PseudocodeSolution.Select(NormalizeStatement).ToArray();
        return entered.SequenceEqual(expected);
    }

    private bool CheckFlowchartAnswer()
    {
        if (_flowCanvas == null)
            return false;

        List<FlowchartBlockDefinition> actual = _flowCanvas.GetDefinitions();
        var expected = _challenge.FlowchartSolution;
        if (actual.Count != expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (!string.Equals(actual[i].Type, expected[i].Type, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(NormalizeStatement(actual[i].Text), NormalizeStatement(expected[i].Text), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string NormalizeStatement(string source)
    {
        string line = (source ?? "").Split("//", StringSplitOptions.None)[0].Trim().ToLowerInvariant();
        line = line.Replace("←", "=").Replace("×", "*").Replace("÷", "/");
        line = line.TrimEnd(';').Trim();

        if (line == "begin" || line == "begin program") line = "start";
        else if (line == "stop" || line == "stop program" || line == "finish") line = "end";

        if (line.StartsWith("read ")) line = "input " + line.Substring(5);
        else if (line.StartsWith("print ")) line = "output " + line.Substring(6);
        else if (line.StartsWith("display ")) line = "output " + line.Substring(8);

        line = Regex.Replace(line, @"\s+", "");
        if (line.EndsWith("then", StringComparison.Ordinal))
            line = line.Substring(0, line.Length - 4);
        return line;
    }

    private void ContinueToNextScene()
    {
        if (!_completed || string.IsNullOrWhiteSpace(_nextScene))
            return;

        if (!ResourceLoader.Exists(_nextScene))
        {
            _feedback.Text = $"Next scene was not found: {_nextScene}";
            _feedback.AddThemeColorOverride("font_color", new Color("fca5a5"));
            return;
        }

        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, _nextScene);
    }

    private void ResetChallenge()
    {
        if (_completed)
            return;

        _feedback.Text = "";
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
