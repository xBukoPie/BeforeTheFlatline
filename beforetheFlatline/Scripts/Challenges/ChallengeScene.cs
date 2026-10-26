using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reusable challenge UI. Each .tscn sets ChallengeId to choose its exercise.
/// Pseudocode tasks use explicit move controls; the flowchart task accepts draggable blocks.
/// </summary>
public partial class ChallengeScene : Control
{
    [Export] public string ChallengeId { get; set; } = "sequence";

    private readonly List<string> _items = new();
    private readonly List<string> _flowchart = new();
    private VBoxContainer _itemList;
    private VBoxContainer _main;
    private Label _feedback;
    private Label _progress;
    private Control _flowCanvas;
    private string _nextScene = "";
    private string _expected = "";

    private static readonly string[] SequenceItems =
    {
        "Wake up", "Get dressed", "Eat breakfast", "Leave for school"
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
        var title = GetNode<Label>("MarginContainer/ChallengeTitle");
        var prompt = GetNode<Label>("MarginContainer/ChallengePrompt");
        // The shell is assembled dynamically, so resolve the labels by walking the main container.
        title = _main.GetNode<Label>("ChallengeTitle");
        prompt = _main.GetNode<Label>("ChallengePrompt");

        switch (ChallengeId.ToLowerInvariant())
        {
            case "sequence":
                title.Text = "01 — Put the morning routine in order";
                prompt.Text = "Rearrange the pseudocode statements so the routine executes from beginning to end. Use the arrow buttons to move each line.";
                _items.AddRange(new[] { "Eat breakfast", "Wake up", "Leave for school", "Get dressed" });
                _expected = string.Join("|", SequenceAnswer);
                _nextScene = "res://Scenes/Challenges/ConditionChallenge.tscn";
                RenderReorderableItems();
                _progress.Text = "Concept: sequence";
                break;
            case "condition":
                title.Text = "02 — Repair the door logic";
                prompt.Text = "Arrange the IF / ELSE pseudocode. If a key is found, open the door; otherwise, find the key.";
                _items.AddRange(ConditionItems);
                _expected = string.Join("|", ConditionAnswer);
                _nextScene = "res://Scenes/Challenges/FlowchartChallenge.tscn";
                RenderReorderableItems();
                _progress.Text = "Concept: selection (IF / ELSE)";
                break;
            case "flowchart":
                title.Text = "03 — Build the grading flowchart";
                prompt.Text = "Drag each block from the palette into the canvas in execution order: start, input score, decision, output, end.";
                RenderFlowchartEditor();
                _expected = string.Join("|", FlowchartAnswer);
                _nextScene = "res://Scenes/Challenges/FinalChallenge.tscn";
                _progress.Text = "Concept: flowchart structure";
                break;
            default:
                title.Text = "04 — Final algorithm";
                prompt.Text = "Arrange these statements into a valid algorithm: read a score, test whether it is at least 50, then report Pass or Fail.";
                _items.AddRange(new[] { "Output Fail", "Read score", "IF score >= 50", "Output Pass", "ELSE", "END IF" });
                _expected = "Read score|IF score >= 50|Output Pass|ELSE|Output Fail|END IF";
                _nextScene = "";
                RenderReorderableItems();
                _progress.Text = "Final challenge";
                break;
        }
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
        foreach (string block in FlowchartBlocks)
        {
            var source = new DragBlockButton { Text = block, BlockName = block, CustomMinimumSize = new Vector2(150, 48) };
            palette.AddChild(source);
        }
        var canvasTitle = new Label { Text = "FLOWCHART CANVAS — drop blocks here" };
        canvasTitle.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        _itemList.AddChild(canvasTitle);

        var canvas = new DropCanvas
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
        if (_flowCanvas is DropCanvas canvas) canvas.ShowBlocks(_flowchart);
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

        _feedback.Text = _nextScene.Length > 0
            ? "Correct. Challenge complete — proceeding to the next exercise."
            : "Correct. You completed the algorithm path. Return to the challenge hub to replay any exercise.";
        _feedback.AddThemeColorOverride("font_color", new Color("86efac"));
        foreach (var child in _main.GetChildren())
            if (child is Button button && button.Text == "Check Answer") button.Disabled = true;

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
        foreach (var child in _main.GetChildren())
            if (child is Button button && button.Text == "Check Answer") button.Disabled = false;

        if (ChallengeId.ToLowerInvariant() == "flowchart")
        {
            _flowchart.Clear();
            if (_flowCanvas is DropCanvas canvas) canvas.ClearBlocks();
        }
        else
        {
            _items.Clear();
            if (ChallengeId.ToLowerInvariant() == "sequence")
                _items.AddRange(new[] { "Eat breakfast", "Wake up", "Leave for school", "Get dressed" });
            else if (ChallengeId.ToLowerInvariant() == "condition")
                _items.AddRange(ConditionItems);
            else
                _items.AddRange(new[] { "Output Fail", "Read score", "IF score >= 50", "Output Pass", "ELSE", "END IF" });
            RenderReorderableItems();
        }
    }

    private static void ClearContainer(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private partial class DragBlockButton : Button
    {
        public string BlockName { get; set; } = "";

        public override Variant _GetDragData(Vector2 atPosition)
        {
            var preview = new Label { Text = BlockName };
            preview.AddThemeColorOverride("font_color", Colors.White);
            preview.AddThemeColorOverride("font_outline_color", Colors.Black);
            preview.AddThemeConstantOverride("outline_size", 4);
            SetDragPreview(preview);
            return BlockName;
        }
    }

    private partial class DropCanvas : PanelContainer
    {
        public event Action<string> BlockDropped;
        private readonly VBoxContainer _blocks = new();

        public DropCanvas()
        {
            AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("1e293b"), BorderColor = new Color("475569"), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 12, ContentMarginBottom = 12 });
            AddChild(_blocks);
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.String && FlowchartBlocks.Contains(data.AsString());
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType == Variant.Type.String)
                BlockDropped?.Invoke(data.AsString());
        }

        public void ShowBlocks(IEnumerable<string> blocks)
        {
            ClearBlocks();
            int index = 1;
            foreach (string block in blocks)
            {
                var label = new Label
                {
                    Text = $"{index}.  {block}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CustomMinimumSize = new Vector2(0, 34)
                };
                label.AddThemeColorOverride("font_color", Colors.White);
                label.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("334155"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
                _blocks.AddChild(label);
                index++;
            }
            if (!blocks.Any())
                _blocks.AddChild(new Label { Text = "Drop blocks here", HorizontalAlignment = HorizontalAlignment.Center });
        }

        public void ClearBlocks()
        {
            foreach (Node child in _blocks.GetChildren())
            {
                _blocks.RemoveChild(child);
                child.QueueFree();
            }
            _blocks.AddChild(new Label { Text = "Drop blocks here", HorizontalAlignment = HorizontalAlignment.Center });
        }
    }
}
