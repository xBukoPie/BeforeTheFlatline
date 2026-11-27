using Godot;
using System;
using System.Collections.Generic;

public partial class FlowchartDropSlotControl : Control
{
    public List<FlowchartBlockDefinition> TargetList { get; private set; }
    public int TargetIndex { get; private set; }

    public event Action<Variant, List<FlowchartBlockDefinition>, int> DropReceived;

    public void Configure(
        List<FlowchartBlockDefinition> targetList,
        int targetIndex,
        bool branchSlot = false)
    {
        TargetList = targetList;
        TargetIndex = targetIndex;
        CustomMinimumSize = new Vector2(0, branchSlot ? 24 : 26);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Drag;
        QueueRedraw();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return false;

        string[] parts = data.AsString().Split('|', 3);
        if (parts.Length == 3 && parts[0] == "ADD")
            return IsUserBlockType(parts[1]);

        return parts.Length == 2 && parts[0] == "MOVE" &&
               Guid.TryParse(parts[1], out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        DropReceived?.Invoke(data, TargetList, TargetIndex);
    }

    public override void _Draw()
    {
        float x = Size.X * 0.5f;
        float top = 0f;
        float bottom = Size.Y;
        Color color = new("7dd3fc");

        DrawLine(new Vector2(x, top), new Vector2(x, bottom - 7f), color, 2f, true);
        DrawLine(new Vector2(x, bottom - 1f), new Vector2(x - 5f, bottom - 8f), color, 2f, true);
        DrawLine(new Vector2(x, bottom - 1f), new Vector2(x + 5f, bottom - 8f), color, 2f, true);
    }

    private static bool IsUserBlockType(string type) =>
        string.Equals(type, "Input", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Process", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Output", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Decision", StringComparison.OrdinalIgnoreCase);
}
