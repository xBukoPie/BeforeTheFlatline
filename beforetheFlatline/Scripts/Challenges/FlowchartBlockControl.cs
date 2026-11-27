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
        bool compact = false)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeConstantOverride("separation", 5);

        var blockRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 72)
        };
        blockRow.AddThemeConstantOverride("separation", 8);

        var dragHandle = new FlowchartDragHandle();
        dragHandle.Configure(Definition.Id);
        blockRow.AddChild(dragHandle);

        var symbol = new FlowchartSymbolControl();
        symbol.Configure(Definition.Type, Definition.Text, compact);
        symbol.TextChanged += value =>
        {
            Definition.Text = value;
            ContentChanged?.Invoke();
        };
        blockRow.AddChild(symbol);
        AddChild(blockRow);

        if (!string.Equals(Definition.Type, "Decision", StringComparison.OrdinalIgnoreCase))
            return;

        AddChild(new FlowchartDecisionConnectorControl { Kind = "Split" });

        var branches = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        branches.AddThemeConstantOverride("separation", 12);
        branches.MouseFilter = MouseFilterEnum.Ignore;

        AddBranchColumn(branches, "TRUE", Definition.TrueBranch, dropHandler);
        AddBranchColumn(branches, "FALSE", Definition.FalseBranch, dropHandler);
        AddChild(branches);

        AddChild(new FlowchartDecisionConnectorControl { Kind = "Merge" });
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
            CustomMinimumSize = new Vector2(0, 0),
            MouseFilter = MouseFilterEnum.Pass
        };
        column.AddThemeConstantOverride("separation", 0);

        var heading = new Label
        {
            Text = branchName,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        heading.AddThemeFontSizeOverride("font_size", 11);
        heading.AddThemeColorOverride("font_color",
            branchName == "TRUE" ? new Color("86efac") : new Color("fca5a5"));
        column.AddChild(heading);

        AddSlot(column, definitions, 0, dropHandler);
        for (int i = 0; i < definitions.Count; i++)
        {
            var child = new FlowchartBlockControl();
            child.Configure(definitions[i], dropHandler, compact: true);
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
