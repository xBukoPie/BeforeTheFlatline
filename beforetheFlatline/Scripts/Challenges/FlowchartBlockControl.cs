using Godot;
using System;
using System.Collections.Generic;

public partial class FlowchartBlockControl : VBoxContainer
{
    public FlowchartBlockDefinition Definition { get; private set; }
    public event Action ContentChanged;

    public void Configure(
        FlowchartBlockDefinition definition,
        Action<Variant, List<FlowchartBlockDefinition>, int> dropHandler,
        bool compact = false,
        bool hasFollowingBlock = false)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeConstantOverride("separation", 0);

        // Center the editable symbol on the connector axis. The drag grip is
        // overlaid on the left and must not change the symbol's horizontal center.
        var blockRow = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 72),
            MouseFilter = MouseFilterEnum.Pass
        };
        AddChild(blockRow);

        var symbolCenter = new CenterContainer
        {
            MouseFilter = MouseFilterEnum.Pass
        };
        symbolCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        blockRow.AddChild(symbolCenter);

        var symbol = new FlowchartSymbolControl();
        symbol.Configure(Definition.Type, Definition.Text, compact);
        symbol.TextChanged += value =>
        {
            Definition.Text = value;
            ContentChanged?.Invoke();
        };
        symbolCenter.AddChild(symbol);

        var dragHandle = new FlowchartDragHandle();
        dragHandle.Configure(Definition.Id);
        dragHandle.Position = Vector2.Zero;
        dragHandle.Size = new Vector2(34, 72);
        blockRow.AddChild(dragHandle);

        if (!string.Equals(Definition.Type, "Decision", StringComparison.OrdinalIgnoreCase))
            return;

        AddChild(new FlowchartDecisionConnectorControl
        {
            Kind = "Split",
            HasTrueBranch = Definition.TrueBranch.Count > 0,
            HasFalseBranch = Definition.FalseBranch.Count > 0
        });

        var branches = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        branches.AddThemeConstantOverride("separation", 12);
        AddBranchColumn(branches, "TRUE", Definition.TrueBranch, dropHandler);
        AddBranchColumn(branches, "FALSE", Definition.FalseBranch, dropHandler);
        AddChild(branches);

        AddChild(new FlowchartDecisionConnectorControl
        {
            Kind = "Merge",
            HasFollowingBlock = hasFollowingBlock
        });
    }

    private void AddBranchColumn(
        HBoxContainer parent,
        string branchName,
        List<FlowchartBlockDefinition> definitions,
        Action<Variant, List<FlowchartBlockDefinition>, int> dropHandler)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        column.AddThemeConstantOverride("separation", 0);

        var heading = new Label
        {
            Text = branchName,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        heading.AddThemeFontSizeOverride("font_size", 11);
        heading.AddThemeColorOverride("font_color",
            branchName == "TRUE" ? new Color("86efac") : new Color("fca5a5"));
        column.AddChild(heading);

        AddSlot(column, definitions, 0, dropHandler);
        for (int i = 0; i < definitions.Count; i++)
        {
            var child = new FlowchartBlockControl();
            child.Configure(definitions[i], dropHandler, compact: true,
                hasFollowingBlock: i < definitions.Count - 1);
            child.ContentChanged += () => ContentChanged?.Invoke();
            column.AddChild(child);
            AddSlot(column, definitions, i + 1, dropHandler);
        }

        parent.AddChild(column);
    }

    private static void AddSlot(
        VBoxContainer parent,
        List<FlowchartBlockDefinition> target,
        int index,
        Action<Variant, List<FlowchartBlockDefinition>, int> dropHandler)
    {
        var slot = new FlowchartDropSlotControl();
        slot.Configure(target, index, branchSlot: true);
        slot.DropReceived += dropHandler;
        parent.AddChild(slot);
    }
}
