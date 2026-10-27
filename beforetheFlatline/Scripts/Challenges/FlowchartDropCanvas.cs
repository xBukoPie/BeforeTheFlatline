using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FlowchartDropCanvas : PanelContainer
{
    public event Action<string, string> BlockDropped;
    private readonly VBoxContainer _blocks = new();
    private readonly List<FlowchartBlockControl> _placedBlocks = new();

    public FlowchartDropCanvas()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0f172a"),
            BorderColor = new Color("475569"),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 10, ContentMarginBottom = 10
        });
        _blocks.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_blocks);
        ShowEmptyMessage();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return false;

        string[] parts = data.AsString().Split('|', 2);
        return parts.Length == 2 && IsBlockType(parts[0]);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return;

        string[] parts = data.AsString().Split('|', 2);
        if (parts.Length == 2 && IsBlockType(parts[0]))
            BlockDropped?.Invoke(parts[0], parts[1]);
    }

    public void AddBlock(string type, string text)
    {
        var definitions = GetDefinitions();
        definitions.Add(new FlowchartBlockDefinition(type, text));
        BuildFromDefinitions(definitions);
    }

    public List<FlowchartBlockDefinition> GetDefinitions() =>
        _placedBlocks.Select(b => new FlowchartBlockDefinition(b.BlockType, b.BlockText.Trim())).ToList();

    public void ClearBlocks() => BuildFromDefinitions(new List<FlowchartBlockDefinition>());

    private void MoveBlock(int index, int delta)
    {
        var definitions = GetDefinitions();
        int target = index + delta;
        if (target < 0 || target >= definitions.Count)
            return;

        (definitions[index], definitions[target]) = (definitions[target], definitions[index]);
        BuildFromDefinitions(definitions);
    }

    private void RemoveBlock(int index)
    {
        var definitions = GetDefinitions();
        if (index < 0 || index >= definitions.Count)
            return;

        definitions.RemoveAt(index);
        BuildFromDefinitions(definitions);
    }

    private void BuildFromDefinitions(IReadOnlyList<FlowchartBlockDefinition> definitions)
    {
        foreach (Node child in _blocks.GetChildren())
        {
            _blocks.RemoveChild(child);
            child.QueueFree();
        }
        _placedBlocks.Clear();

        for (int i = 0; i < definitions.Count; i++)
        {
            int index = i;
            var data = definitions[i];
            var block = new FlowchartBlockControl();
            block.Configure(
                data.Type,
                data.Text,
                index,
                i > 0 ? idx => MoveBlock(idx, -1) : null,
                i < definitions.Count - 1 ? idx => MoveBlock(idx, 1) : null,
                idx => RemoveBlock(idx));
            _placedBlocks.Add(block);
            _blocks.AddChild(block);

            if (i < definitions.Count - 1)
            {
                var connector = new Label
                {
                    Name = "Connector",
                    Text = data.Type == "Decision" ? "      TRUE ↓     /     FALSE ↓" : "                         ↓",
                    CustomMinimumSize = new Vector2(0, 22),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                connector.AddThemeColorOverride("font_color", new Color("7dd3fc"));
                _blocks.AddChild(connector);
            }
        }

        if (definitions.Count == 0)
            ShowEmptyMessage();
    }

    private void ShowEmptyMessage()
    {
        if (_blocks.GetChildCount() == 0)
            _blocks.AddChild(new Label
            {
                Name = "EmptyMessage",
                Text = "Drag shaped blocks here, then define each block's value.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore
            });
    }

    private static bool IsBlockType(string type) =>
        type == "Start" || type == "Input" || type == "Process" ||
        type == "Output" || type == "Decision" || type == "End";
}
