using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FlowchartDropCanvas : PanelContainer
{
    public event Action<string, string> BlockDropped;
    public event Action ContentChanged;

    public int PendingInsertionIndex { get; private set; }

    private readonly VBoxContainer _blocks = new();
    private readonly List<FlowchartBlockControl> _placedBlocks = new();

    public FlowchartDropCanvas()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0f172a"),
            BorderColor = new Color("475569"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        });

        _blocks.MouseFilter = MouseFilterEnum.Ignore;
        _blocks.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_blocks);
        ShowEmptyMessage();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return false;

        string[] parts = data.AsString().Split('|', 3);
        if (parts.Length == 3 && parts[0] == "ADD" && IsBlockType(parts[1]))
            return true;

        if (parts.Length == 2 && parts[0] == "MOVE" &&
            int.TryParse(parts[1], out int index) &&
            index >= 0 && index < _placedBlocks.Count)
            return true;

        // Compatibility with an older palette payload.
        return parts.Length == 2 && IsBlockType(parts[0]);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return;

        string[] parts = data.AsString().Split('|', 3);
        HandleDropData(data, GetInsertionIndex(GetGlobalTransformWithCanvas() * atPosition));
    }

    public void AddBlock(string type, string text, int insertIndex = -1)
    {
        var definitions = GetDefinitions();
        int index = insertIndex < 0 ? definitions.Count : Mathf.Clamp(insertIndex, 0, definitions.Count);
        definitions.Insert(index, new FlowchartBlockDefinition(type, text));

        // A Decision immediately receives two editable outgoing branches.
        if (string.Equals(type, "Decision", StringComparison.OrdinalIgnoreCase))
        {
            definitions.Insert(index + 1, new FlowchartBlockDefinition("Output", "TRUE: OUTPUT value"));
            definitions.Insert(index + 2, new FlowchartBlockDefinition("Output", "FALSE: OUTPUT value"));
        }

        BuildFromDefinitions(definitions);
    }

    public List<FlowchartBlockDefinition> GetDefinitions() =>
        _placedBlocks.Select(block =>
            new FlowchartBlockDefinition(block.BlockType, block.BlockText.Trim())).ToList();

    public void ClearBlocks() => BuildFromDefinitions(new List<FlowchartBlockDefinition>());

    public void SetDefinitions(IReadOnlyList<FlowchartBlockDefinition> definitions) =>
        BuildFromDefinitions(definitions ?? Array.Empty<FlowchartBlockDefinition>());

    private void MoveBlock(int fromIndex, int targetIndex)
    {
        var definitions = GetDefinitions();
        if (fromIndex < 0 || fromIndex >= definitions.Count || definitions.Count < 2)
            return;

        int insertion = Mathf.Clamp(targetIndex, 0, definitions.Count);
        FlowchartBlockDefinition moving = definitions[fromIndex];
        definitions.RemoveAt(fromIndex);

        if (fromIndex < insertion)
            insertion--;

        insertion = Mathf.Clamp(insertion, 0, definitions.Count);
        if (insertion == fromIndex)
            return;

        definitions.Insert(insertion, moving);
        BuildFromDefinitions(definitions);
    }

    private int GetInsertionIndex(Vector2 globalDropPosition)
    {
        if (_placedBlocks.Count == 0)
            return 0;

        var ordered = _placedBlocks
            .OrderBy(block => block.GlobalPosition.Y + block.Size.Y * 0.5f)
            .ThenBy(block => block.GlobalPosition.X + block.Size.X * 0.5f)
            .ToList();

        int target = 0;
        foreach (FlowchartBlockControl block in ordered)
        {
            Vector2 center = block.GlobalPosition + block.Size * 0.5f;
            bool before = center.Y < globalDropPosition.Y - 3f ||
                          (Mathf.Abs(center.Y - globalDropPosition.Y) <= 3f &&
                           center.X < globalDropPosition.X);
            if (before)
                target++;
        }

        return Mathf.Clamp(target, 0, _placedBlocks.Count);
    }

    private void BuildFromDefinitions(IReadOnlyList<FlowchartBlockDefinition> definitions)
    {
        foreach (Node child in _blocks.GetChildren())
        {
            _blocks.RemoveChild(child);
            child.QueueFree();
        }
        _placedBlocks.Clear();

        int i = 0;
        while (i < definitions.Count)
        {
            FlowchartBlockDefinition data = definitions[i];

            if (IsDecisionWithOutputBranches(definitions, i))
            {
                AddPlacedBlock(data, i, compact: false);
                AddConnector("Split", i + 1, 52);

                var branches = new HBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                branches.AddThemeConstantOverride("separation", 22);
                AddPlacedBlockToBranch(branches, definitions[i + 1], i + 1, "TRUE");
                AddPlacedBlockToBranch(branches, definitions[i + 2], i + 2, "FALSE");
                _blocks.AddChild(branches);

                // The merge connector also points down to the next shared step.
                AddConnector("Merge", i + 3, 52);
                i += 3;
                continue;
            }

            AddPlacedBlock(data, i, compact: false);
            i++;

            if (i < definitions.Count)
                AddConnector("Down", i, 38);
        }

        if (_placedBlocks.Count == 0)
            ShowEmptyMessage();

        ContentChanged?.Invoke();
    }

    private void AddConnector(string kind, int insertIndex, float height)
    {
        var connector = new FlowchartConnectorControl
        {
            Kind = kind,
            CustomMinimumSize = new Vector2(0, height)
        };
        connector.Dropped += data => HandleDropData(data, insertIndex);
        _blocks.AddChild(connector);
    }

    private void HandleDropData(Variant data, int insertIndex)
    {
        if (data.VariantType != Variant.Type.String)
            return;

        string[] parts = data.AsString().Split('|', 3);
        PendingInsertionIndex = Mathf.Clamp(insertIndex, 0, _placedBlocks.Count);

        if (parts.Length == 2 && parts[0] == "MOVE" &&
            int.TryParse(parts[1], out int fromIndex))
        {
            MoveBlock(fromIndex, PendingInsertionIndex);
            return;
        }

        if (parts.Length == 3 && parts[0] == "ADD" && IsBlockType(parts[1]))
        {
            BlockDropped?.Invoke(parts[1], parts[2]);
            return;
        }

        if (parts.Length == 2 && IsBlockType(parts[0]))
            BlockDropped?.Invoke(parts[0], parts[1]);
    }

    private void AddPlacedBlock(FlowchartBlockDefinition data, int index, bool compact)
    {
        string branch = ExtractBranchLabel(data.Text);
        var block = new FlowchartBlockControl();
        block.Configure(data.Type, data.Text, index, branch, compact);
        block.TextChanged += () => ContentChanged?.Invoke();
        _placedBlocks.Add(block);
        _blocks.AddChild(block);
    }

    private void AddPlacedBlockToBranch(HBoxContainer row, FlowchartBlockDefinition data, int index, string defaultBranch)
    {
        string branch = ExtractBranchLabel(data.Text);
        if (string.IsNullOrEmpty(branch))
            branch = defaultBranch;

        var block = new FlowchartBlockControl
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        block.Configure(data.Type, data.Text, index, branch, compact: true);
        block.TextChanged += () => ContentChanged?.Invoke();
        _placedBlocks.Add(block);
        row.AddChild(block);
    }

    private static bool IsDecisionWithOutputBranches(IReadOnlyList<FlowchartBlockDefinition> definitions, int index)
    {
        if (index + 2 >= definitions.Count ||
            !string.Equals(definitions[index].Type, "Decision", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(definitions[index + 1].Type, "Output", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(definitions[index + 2].Type, "Output", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractBranchLabel(string text)
    {
        string value = (text ?? "").Trim();
        if (value.StartsWith("TRUE:", StringComparison.OrdinalIgnoreCase))
            return "TRUE";
        if (value.StartsWith("FALSE:", StringComparison.OrdinalIgnoreCase))
            return "FALSE";
        return "";
    }

    private void ShowEmptyMessage()
    {
        if (_blocks.GetChildCount() == 0)
        {
            _blocks.AddChild(new Label
            {
                Name = "EmptyMessage",
                Text = "Drag shaped blocks here. Use the grip beside a placed block to move it.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore
            });
        }
    }

    private static bool IsBlockType(string type) =>
        string.Equals(type, "Start", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Input", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Process", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Output", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Decision", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "End", StringComparison.OrdinalIgnoreCase);
}
