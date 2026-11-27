using Godot;
using System;
using System.Collections.Generic;

public partial class FlowchartBlockControl : VBoxContainer
{
    public FlowchartBlockDefinition Definition { get; private set; }
    public Control SymbolControl { get; private set; }
    public event Action ContentChanged;

    public void CollectSymbolControls(List<Control> controls)
    {
        if (controls == null)
            return;

        if (SymbolControl != null)
            controls.Add(SymbolControl);

        CollectNestedSymbols(this, controls);
    }

    private static void CollectNestedSymbols(Node parent, List<Control> controls)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is FlowchartBlockControl nested && nested.SymbolControl != null)
                controls.Add(nested.SymbolControl);

            CollectNestedSymbols(child, controls);
        }
    }

    public void Configure(
        FlowchartBlockDefinition definition,
        Action<Variant, List<FlowchartBlockDefinition>, int> dropHandler,
        bool compact = false)
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
        SymbolControl = symbol;
        symbol.Configure(Definition.Type, Definition.Text, compact);
        symbolCenter.AddChild(symbol);

        var dragHandle = new FlowchartDragHandle();
        dragHandle.Configure(Definition.Id);
        dragHandle.AnchorLeft = 0.5f;
        dragHandle.AnchorRight = 0.5f;
        dragHandle.AnchorTop = 0f;
        dragHandle.AnchorBottom = 1f;
        blockRow.AddChild(dragHandle);
        AlignDragHandleWithSymbol(dragHandle, symbol);

        symbol.TextChanged += value =>
        {
            Definition.Text = value;
            AlignDragHandleWithSymbol(dragHandle, symbol);
            ContentChanged?.Invoke();
        };

        if (!string.Equals(Definition.Type, "Decision", StringComparison.OrdinalIgnoreCase))
            return;

        AddChild(new FlowchartDecisionConnectorControl { Kind = "Split" });

        var branches = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        branches.AddThemeConstantOverride("separation", 0);
        AddBranchColumn(branches, Definition.FalseBranch, dropHandler);
        AddBranchColumn(branches, Definition.TrueBranch, dropHandler);
        AddChild(branches);

        AddChild(new FlowchartDecisionConnectorControl { Kind = "Merge" });
    }

    private static void AlignDragHandleWithSymbol(
        FlowchartDragHandle dragHandle,
        FlowchartSymbolControl symbol)
    {
        if (dragHandle == null || symbol == null)
            return;

        float halfWidth = Mathf.Max(symbol.CustomMinimumSize.X, symbol.Size.X) * 0.5f;
        dragHandle.OffsetLeft = -halfWidth - 34f;
        dragHandle.OffsetRight = -halfWidth;
        dragHandle.OffsetTop = 0f;
        dragHandle.OffsetBottom = 0f;
    }

    private void AddBranchColumn(
        HBoxContainer parent,
        List<FlowchartBlockDefinition> definitions,
        Action<Variant, List<FlowchartBlockDefinition>, int> dropHandler)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        column.AddThemeConstantOverride("separation", 0);

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
