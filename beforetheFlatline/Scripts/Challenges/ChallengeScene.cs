using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SchoolYardGame;

/// <summary>
/// Reusable challenge UI. Each .tscn sets ChallengeId to choose its exercise.
/// Pseudocode tasks use explicit move controls; the flowchart task accepts draggable blocks.
/// </summary>
public partial class ChallengeScene : Control
{
    [Export] public string ChallengeId { get; set; } = "sequence";

    private readonly List<string> _items = new();
    private readonly List<string> _initialItems = new();
    private readonly List<string> _flowchart = new();
    private string[] _flowchartPalette = FlowchartBlocks;
    private VBoxContainer _itemList;
    private VBoxContainer _main;
    private Label _feedback;
    private Label _progress;
    private Control _flowCanvas;
    private string _nextScene = "";
    private string _expected = "";

    private static readonly string[] SequenceItems =
    {
        "Eat breakfast", "Wake up", "Leave for school", "Get dressed"
    };
    private static readonly string[] SequenceAnswer =
    {
        "Wake up", "Get dressed", "Eat breakfast", "Leave for school"
    };
    private static readonly string[] ConditionItems =
    {
        "ELSE", "Open the door", "IF keyFound == true", "END IF", "Find the key"
    };
    private static readonly string[] ConditionAnswer =
    {
        "IF keyFound == true", "Open the door", "ELSE", "Find the key", "END IF"
    };
    private static readonly string[] FlowchartAnswer =
    {
        "Start", "Input score", "Decision: score >= 50?", "Output: Pass / Fail", "End"
    };
    private static readonly string[] FlowchartBlocks =
    {
        "Start", "Input score", "Decision: score >= 50?", "Output: Pass / Fail", "End"
    };

    public override void _Ready()
    {
        BuildShell();
        ConfigureChallenge();
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
        margin.AddThemeConstantOverride("margin_left", 42);
        margin.AddThemeConstantOverride("margin_right", 42);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(margin);

        _main = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _main.AddThemeConstantOverride("separation", 14);
        margin.AddChild(_main);

        var top = new HBoxContainer();
        _main.AddChild(top);
        var title = new Label { Text = "ALGORITHM LAB", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        top.AddChild(title);
        var exit = new Button { Text = "Back to Challenges" };
        exit.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Challenges/ChallengeHub.tscn");
        top.AddChild(exit);

        var heading = new Label { Name = "ChallengeTitle", Text = "Challenge", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        heading.AddThemeFontSizeOverride("font_size", 30);
        heading.AddThemeColorOverride("font_color", Colors.White);
        _main.AddChild(heading);

        var prompt = new Label { Name = "ChallengePrompt", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        prompt.AddThemeFontSizeOverride("font_size", 16);
        prompt.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        _main.AddChild(prompt);

        var contentScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _main.AddChild(contentScroll);
        _itemList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _itemList.AddThemeConstantOverride("separation", 8);
        contentScroll.AddChild(_itemList);

        _feedback = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _feedback.AddThemeFontSizeOverride("font_size", 15);
        _main.AddChild(_feedback);

        var footer = new HBoxContainer();
        _main.AddChild(footer);
        _progress = new Label { Text = "", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        footer.AddChild(_progress);
        var check = new Button { Text = "Check Answer", CustomMinimumSize = new Vector2(160, 44) };
        check.Pressed += CheckAnswer;
        footer.AddChild(check);
        var reset = new Button { Text = "Reset", CustomMinimumSize = new Vector2(100, 44) };
        reset.Pressed += ResetChallenge;
        footer.AddChild(reset);
    }

    private void ConfigureChallenge()
    {
        var title = _main.GetNode<Label>("ChallengeTitle");
        var prompt = _main.GetNode<Label>("ChallengePrompt");
        int currentDay = DayClock.Instance?.DayNumber ?? 1;
        int difficulty = Mathf.Clamp(currentDay, 1, 5);
        bool alreadyDone = ChallengeProgress.IsCompleted(ChallengeId);

        switch (ChallengeId.ToLowerInvariant())
        {
            case "sequence":
            {
                var challenge = BuildSequenceChallenge(difficulty);
                title.Text = "01 — Put the routine in order";
                prompt.Text = "Rearrange the pseudocode statements so the routine executes from beginning to end. Use the arrow buttons to move each line.";
                SetPseudocodeChallenge(challenge.Initial, challenge.Answer);
                _nextScene = "res://Scenes/Challenges/ConditionChallenge.tscn";
                _progress.Text = $"Day {currentDay} • Difficulty {difficulty}/5 • Sequence" + (alreadyDone ? "   ✓ completed today" : "");
                break;
            }
            case "condition":
            {
                var challenge = BuildConditionChallenge(difficulty);
                title.Text = "02 — Repair the program logic";
                prompt.Text = difficulty == 1
                    ? "Arrange the IF / ELSE pseudocode. If a key is found, open the door; otherwise, find the key."
                    : "Arrange the statements into a valid algorithm. Pay attention to loop boundaries, counters, and nested conditions.";
                SetPseudocodeChallenge(challenge.Initial, challenge.Answer);
                _nextScene = "res://Scenes/Challenges/FlowchartChallenge.tscn";
                _progress.Text = $"Day {currentDay} • Difficulty {difficulty}/5 • Conditions and loops" + (alreadyDone ? "   ✓ completed today" : "");
                break;
            }
            case "flowchart":
            {
                var challenge = BuildFlowchartChallenge(difficulty);
                title.Text = "03 — Build the flowchart";
                prompt.Text = "Drag every block from the palette into the canvas in the correct execution order. Later days introduce more branches and validation steps.";
                _flowchartPalette = challenge.Palette;
                _expected = string.Join("|", challenge.Answer);
                RenderFlowchartEditor();
                _nextScene = "res://Scenes/Challenges/FinalChallenge.tscn";
                _progress.Text = $"Day {currentDay} • Difficulty {difficulty}/5 • Flowchart" + (alreadyDone ? "   ✓ completed today" : "");
                break;
            }
            default:
            {
                var challenge = BuildFinalChallenge(difficulty);
                title.Text = "04 — Final algorithm";
                prompt.Text = "Arrange the pseudocode so the complete algorithm executes correctly. The task grows more complex as the in-game day advances.";
                SetPseudocodeChallenge(challenge.Initial, challenge.Answer);
                _nextScene = "res://Scenes/School/PostChallengeConversation.tscn";
                _progress.Text = $"Day {currentDay} • Difficulty {difficulty}/5 • Final algorithm" + (alreadyDone ? "   ✓ completed today" : "");
                break;
            }
        }

        if (alreadyDone)
        {
            _feedback.Text = "You already solved this challenge today. You can replay it, but the sanity reward is only given once per day.";
            _feedback.AddThemeColorOverride("font_color", new Color("86efac"));
        }
    }

    private void SetPseudocodeChallenge(string[] initial, string[] answer)
    {
        _initialItems.Clear();
        _initialItems.AddRange(initial);
        _items.Clear();
        _items.AddRange(_initialItems);
        _expected = string.Join("|", answer);
        RenderReorderableItems();
    }

    private static (string[] Initial, string[] Answer) BuildSequenceChallenge(int level)
    {
        return level switch
        {
            1 => (SequenceItems, SequenceAnswer),
            2 => (
                new[] { "Pack school bag", "Wake up", "Check timetable", "Eat breakfast", "Leave for school", "Get dressed", "Check homework" },
                new[] { "Wake up", "Get dressed", "Eat breakfast", "Check homework", "Check timetable", "Pack school bag", "Leave for school" }),
            3 => (
                new[] { "Check timetable", "Leave for school", "Brush teeth", "Eat breakfast", "Wake up", "Pack school bag", "Check homework", "Get dressed" },
                new[] { "Wake up", "Get dressed", "Brush teeth", "Eat breakfast", "Check homework", "Check timetable", "Pack school bag", "Leave for school" }),
            4 => (
                new[] { "Fill water bottle", "Check timetable", "Wake up", "Leave for school", "Eat breakfast", "Pack school bag", "Brush teeth", "Check homework", "Get dressed" },
                new[] { "Wake up", "Get dressed", "Brush teeth", "Eat breakfast", "Check homework", "Check timetable", "Fill water bottle", "Pack school bag", "Leave for school" }),
            _ => (
                new[] { "Pack school bag", "Check weather", "Fill water bottle", "Eat breakfast", "Leave for school", "Check timetable", "Wake up", "Brush teeth", "Check homework", "Get dressed" },
                new[] { "Wake up", "Check weather", "Get dressed", "Brush teeth", "Eat breakfast", "Check homework", "Check timetable", "Fill water bottle", "Pack school bag", "Leave for school" })
        };
    }

    private static (string[] Initial, string[] Answer) BuildConditionChallenge(int level)
    {
        return level switch
        {
            1 => (ConditionItems, ConditionAnswer),
            2 => (
                new[] { "END IF", "Find the key", "ELSE", "attempts = attempts + 1", "Open the door", "WHILE keyFound == false", "Ask for help", "attempts = 0", "IF keyFound == true", "END WHILE" },
                new[] { "attempts = 0", "WHILE keyFound == false", "Find the key", "attempts = attempts + 1", "END WHILE", "IF keyFound == true", "Open the door", "ELSE", "Ask for help", "END IF" }),
            3 => (
                new[] { "END WHILE", "Search the room", "IF keyFound == true", "MAX_ATTEMPTS = 3", "Ask for help", "attempts = attempts + 1", "END IF", "Open the door", "WHILE keyFound == false AND attempts < MAX_ATTEMPTS", "attempts = 0", "ELSE" },
                new[] { "attempts = 0", "MAX_ATTEMPTS = 3", "WHILE keyFound == false AND attempts < MAX_ATTEMPTS", "Search the room", "attempts = attempts + 1", "END WHILE", "IF keyFound == true", "Open the door", "ELSE", "Ask for help", "END IF" }),
            4 => (
                new[] { "Show student ID", "END IF", "attempts = 0", "ELSE", "Open the door", "Search the room", "IF studentID == true", "MAX_ATTEMPTS = 3", "Ask for help", "END WHILE", "IF keyFound == true", "attempts = attempts + 1", "WHILE keyFound == false AND attempts < MAX_ATTEMPTS", "END IF", "ELSE" },
                new[] { "attempts = 0", "MAX_ATTEMPTS = 3", "WHILE keyFound == false AND attempts < MAX_ATTEMPTS", "Search the room", "attempts = attempts + 1", "END WHILE", "IF keyFound == true", "IF studentID == true", "Open the door", "ELSE", "Show student ID", "END IF", "ELSE", "Ask for help", "END IF" }),
            _ => (
                new[] { "END IF", "Wait until morning", "Show student ID", "MAX_ATTEMPTS = 3", "ELSE", "attempts = attempts + 1", "Open the door", "IF schoolOpen == false", "Search the room", "END WHILE", "IF studentID == true", "Ask for help", "END IF", "attempts = 0", "WHILE keyFound == false AND attempts < MAX_ATTEMPTS", "IF keyFound == true", "ELSE" },
                new[] { "attempts = 0", "MAX_ATTEMPTS = 3", "WHILE keyFound == false AND attempts < MAX_ATTEMPTS", "Search the room", "attempts = attempts + 1", "END WHILE", "IF keyFound == true", "IF studentID == true", "Open the door", "ELSE", "Show student ID", "END IF", "ELSE", "Ask for help", "END IF", "IF schoolOpen == false", "Wait until morning", "END IF" })
        };
    }

    private static (string[] Palette, string[] Answer) BuildFlowchartChallenge(int level)
    {
        return level switch
        {
            1 => (FlowchartBlocks, FlowchartAnswer),
            2 => (
                new[] { "Start", "Output: Fail", "Input score", "End", "Decision: score >= 50?", "Output: Pass" },
                new[] { "Start", "Input score", "Decision: score >= 50?", "Output: Pass", "Output: Fail", "End" }),
            3 => (
                new[] { "Output: Fail", "Decision: score >= 50?", "Start", "Output: Excellent", "Input score", "End", "Decision: score >= 75?", "Output: Pass" },
                new[] { "Start", "Input score", "Decision: score >= 75?", "Output: Excellent", "Decision: score >= 50?", "Output: Pass", "Output: Fail", "End" }),
            4 => (
                new[] { "Output: Not eligible", "Decision: score >= 50?", "Input attendance", "Start", "Output: Pass", "Decision: attendance >= 75%?", "End", "Input score", "Output: Excellent", "Decision: score >= 75?", "Output: Fail", "Output: Eligible" },
                new[] { "Start", "Input score", "Input attendance", "Decision: score >= 75?", "Output: Excellent", "Decision: score >= 50?", "Output: Pass", "Output: Fail", "Decision: attendance >= 75%?", "Output: Eligible", "Output: Not eligible", "End" }),
            _ => (
                new[] { "Output: Fail", "Decision: attempts < 3?", "Set attempts = attempts + 1", "Input score", "End", "Output: Invalid score", "Start", "Decision: score valid?", "Decision: score >= 75?", "Output: Excellent", "Set attempts = 0", "Output: Pass", "Output: Retry", "Output: Fail", "Output: Invalid score" },
                new[] { "Start", "Set attempts = 0", "Input score", "Decision: score valid?", "Output: Invalid score", "Set attempts = attempts + 1", "Decision: attempts < 3?", "Output: Retry", "Input score", "Decision: score >= 75?", "Output: Excellent", "Decision: score >= 50?", "Output: Pass", "Output: Fail", "End" })
        };
    }

    private static (string[] Initial, string[] Answer) BuildFinalChallenge(int level)
    {
        return level switch
        {
            1 => (
                new[] { "Output Fail", "Read score", "IF score >= 50", "Output Pass", "ELSE", "END IF" },
                new[] { "Read score", "IF score >= 50", "Output Pass", "ELSE", "Output Fail", "END IF" }),
            2 => (
                new[] { "END WHILE", "Output Fail", "attempts = 0", "IF score >= 50", "Read score", "ELSE", "attempts = attempts + 1", "Output Pass", "WHILE attempts < 2", "END IF" },
                new[] { "attempts = 0", "WHILE attempts < 2", "Read score", "IF score >= 50", "Output Pass", "ELSE", "Output Fail", "END IF", "attempts = attempts + 1", "END WHILE" }),
            3 => (
                new[] { "Output Fail", "Read score", "END IF", "Output Invalid score", "IF score >= 50", "END WHILE", "Read score", "Output Pass", "ELSE", "IF score >= 75", "END IF", "WHILE score < 0 OR score > 100", "Output Excellent", "ELSE", "END IF" },
                new[] { "Read score", "WHILE score < 0 OR score > 100", "Output Invalid score", "Read score", "END WHILE", "IF score >= 75", "Output Excellent", "ELSE", "IF score >= 50", "Output Pass", "ELSE", "Output Fail", "END IF", "END IF" }),
            4 => (
                new[] { "Output Fail", "Read score", "END WHILE", "Output Too many invalid attempts", "IF attempts < 3", "Output Pass", "attempts = 0", "ELSE", "Read score", "END IF", "WHILE (score < 0 OR score > 100) AND attempts < 3", "Output Invalid score", "END IF", "attempts = attempts + 1", "IF score >= 75", "Output Excellent", "IF score >= 50", "ELSE", "END IF" },
                new[] { "Read score", "attempts = 0", "WHILE (score < 0 OR score > 100) AND attempts < 3", "Output Invalid score", "attempts = attempts + 1", "IF attempts < 3", "Read score", "ELSE", "Output Too many invalid attempts", "END IF", "END WHILE", "IF score >= 75", "Output Excellent", "ELSE", "IF score >= 50", "Output Pass", "ELSE", "Output Fail", "END IF", "END IF" }),
            _ => (
                new[] { "average = total / 3", "END IF", "Read scores", "Output Fail", "count = count + 1", "IF average >= 75", "Output Excellent", "total = total + scores[count]", "END WHILE", "IF average >= 50", "total = 0", "count = 0", "Output Pass", "ELSE", "WHILE count < 3", "END IF", "ELSE" },
                new[] { "Read scores", "total = 0", "count = 0", "WHILE count < 3", "total = total + scores[count]", "count = count + 1", "END WHILE", "average = total / 3", "IF average >= 75", "Output Excellent", "ELSE", "IF average >= 50", "Output Pass", "ELSE", "Output Fail", "END IF", "END IF" })
        };
    }

    private void RenderReorderableItems()
    {
        ClearContainer(_itemList);
        for (int i = 0; i < _items.Count; i++)
        {
            int index = i;
            var row = new HBoxContainer();
            _itemList.AddChild(row);
            var label = new Label
            {
                Text = $"{i + 1}.  {_items[i]}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 42),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeColorOverride("font_color", Colors.White);
            row.AddChild(label);
            var up = new Button { Text = "↑", Disabled = i == 0, CustomMinimumSize = new Vector2(44, 40) };
            up.Pressed += () => MoveItem(index, -1);
            row.AddChild(up);
            var down = new Button { Text = "↓", Disabled = i == _items.Count - 1, CustomMinimumSize = new Vector2(44, 40) };
            down.Pressed += () => MoveItem(index, 1);
            row.AddChild(down);
        }
    }

    private void MoveItem(int index, int delta)
    {
        int target = index + delta;
        if (target < 0 || target >= _items.Count) return;
        (_items[index], _items[target]) = (_items[target], _items[index]);
        RenderReorderableItems();
        _feedback.Text = "";
    }

    private void RenderFlowchartEditor()
    {
        ClearContainer(_itemList);
        var paletteTitle = new Label { Text = "BLOCK PALETTE — drag a block into the canvas" };
        paletteTitle.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _itemList.AddChild(paletteTitle);
        var palette = new HFlowContainer();
        _itemList.AddChild(palette);
        foreach (string block in _flowchartPalette)
        {
            var source = new ChallengeDragBlockButton { Text = block, BlockName = block, CustomMinimumSize = new Vector2(150, 48) };
            palette.AddChild(source);
        }
        var canvasTitle = new Label { Text = "FLOWCHART CANVAS — drop blocks here" };
        canvasTitle.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _itemList.AddChild(canvasTitle);

        var canvas = new FlowchartDropCanvas
        {
            CustomMinimumSize = new Vector2(0, 210),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        canvas.BlockDropped += AddFlowchartBlock;
        _itemList.AddChild(canvas);
        _flowCanvas = canvas;

        var clear = new Button { Text = "Clear flowchart", CustomMinimumSize = new Vector2(140, 36) };
        clear.Pressed += () => { _flowchart.Clear(); canvas.ClearBlocks(); _feedback.Text = ""; };
        _itemList.AddChild(clear);
    }

    private void AddFlowchartBlock(string block)
    {
        _flowchart.Add(block);
        if (_flowCanvas is FlowchartDropCanvas canvas) canvas.ShowBlocks(_flowchart);
        _feedback.Text = $"Blocks placed: {_flowchart.Count}. You can drag the same block more than once; duplicates will fail validation.";
    }

    private void CheckAnswer()
    {
        bool correct = ChallengeId.ToLowerInvariant() == "flowchart"
            ? string.Join("|", _flowchart) == _expected
            : string.Join("|", _items) == _expected;

        if (!correct)
        {
            _feedback.Text = "Not quite. Review the order and try again. Check the dependency between each step.";
            _feedback.AddThemeColorOverride("font_color", new Color("fca5a5"));
            return;
        }

        ChallengeProgress.MarkCompleted(ChallengeId);

        bool allDone = ChallengeProgress.AllCompleted;
        _feedback.Text = allDone
            ? $"Correct. ALL CHALLENGES COMPLETE — +{ChallengeProgress.SanityReward} sanity restored. Return to the hub to see your record."
            : (_nextScene.Length > 0
                ? $"Correct. Challenge complete — +{ChallengeProgress.SanityReward} sanity restored. Proceeding to the next exercise."
                : $"Correct. +{ChallengeProgress.SanityReward} sanity restored. Return to the challenge hub to replay any exercise.");
        _feedback.AddThemeColorOverride("font_color", new Color("86efac"));

        // FIXED: search recursively — the buttons live inside the footer HBoxContainer,
        // so the old _main.GetChildren() loop never found them.
        if (FindButtonByText(_main, "Check Answer") is Button checkBtn) checkBtn.Disabled = true;
        if (FindButtonByText(_main, "Reset") is Button resetBtn) resetBtn.Disabled = true;

        if (!string.IsNullOrEmpty(_nextScene))
            CallDeferred(nameof(GoToNextChallenge));
    }

    private void GoToNextChallenge()
    {
        GetTree().ChangeSceneToFile(_nextScene);
    }

    private void ResetChallenge()
    {
        _feedback.Text = "";
        if (FindButtonByText(_main, "Check Answer") is Button checkBtn) checkBtn.Disabled = false;

        if (ChallengeId.ToLowerInvariant() == "flowchart")
        {
            _flowchart.Clear();
            if (_flowCanvas is FlowchartDropCanvas canvas) canvas.ClearBlocks();
        }
        else
        {
            _items.Clear();
            _items.AddRange(_initialItems);
            RenderReorderableItems();
        }
    }

    private static Button FindButtonByText(Node node, string text)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Button button && button.Text == text)
                return button;
            Button found = FindButtonByText(child, text);
            if (found != null) return found;
        }
        return null;
    }

    private static void ClearContainer(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}