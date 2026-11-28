using Godot;
using System;
using System.Collections.Generic;

public partial class FlowchartDropSlotControl : Control
{
    public List<FlowchartBlockDefinition> TargetList { get; private set; }
    public int TargetIndex { get; private set; }

    private bool _hasNextBlock;
    private bool _dropHover;

    public event Action<Variant, List<FlowchartBlockDefinition>, int> DropReceived;

    public FlowchartDropSlotControl()
    {
        MouseExited += ClearDropHover;
    }

    public void Configure(List<FlowchartBlockDefinition> targetList, int targetIndex, bool branchSlot = false)
    {
        TargetList = targetList;
        TargetIndex = targetIndex;
        _hasNextBlock = targetList != null && targetIndex >= 0 && targetIndex < targetList.Count;

        // Make the entire connector gap a forgiving drop target instead of requiring
        // a near-pixel-perfect release on the thin connector line.
        CustomMinimumSize = new Vector2(0, branchSlot ? 48 : 56);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Drag;
        TooltipText = "Drop a flowchart block anywhere in this highlighted gap";
        QueueRedraw();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool accepted = IsAcceptedPayload(data);
        if (_dropHover != accepted)
        {
            _dropHover = accepted;
            QueueRedraw();
        }

        return accepted;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _dropHover = false;
        QueueRedraw();
        DropReceived?.Invoke(data, TargetList, TargetIndex);
    }

    public override void _Draw()
    {
        float x = Size.X * 0.5f;
        float bottom = Size.Y;
        Color color = new("7dd3fc");

        if (_dropHover && Size.X > 8f && Size.Y > 8f)
        {
            var bounds = new Rect2(3f, 2f, Size.X - 6f, Size.Y - 4f);
            DrawRect(bounds, new Color(0.12f, 0.52f, 0.82f, 0.16f), true);
            DrawRect(bounds, new Color(0.38f, 0.78f, 1f, 0.72f), false, 1.5f);
            DrawLine(new Vector2(x, 0f), new Vector2(x, bottom), color, 4f, true);
            return;
        }

        if (_hasNextBlock)
        {
            DrawLine(new Vector2(x, 0f), new Vector2(x, bottom - 7f), color, 2f, true);
            DrawArrowDown(x, bottom - 1f);
        }
        else
        {
            DrawLine(new Vector2(x, 0f), new Vector2(x, bottom), color, 2f, true);
        }
    }

    private void ClearDropHover()
    {
        if (!_dropHover)
            return;

        _dropHover = false;
        QueueRedraw();
    }

    private void DrawArrowDown(float x, float y)
    {
        DrawLine(new Vector2(x, y), new Vector2(x - 5f, y - 7f), new Color("7dd3fc"), 2f, true);
        DrawLine(new Vector2(x, y), new Vector2(x + 5f, y - 7f), new Color("7dd3fc"), 2f, true);
    }

    private static bool IsAcceptedPayload(Variant data)
    {
        if (data.VariantType != Variant.Type.String)
            return false;

        string[] parts = data.AsString().Split('|', 3);
        if (parts.Length == 3 && parts[0] == "ADD")
            return IsUserBlockType(parts[1]);

        return parts.Length == 2 && parts[0] == "MOVE" &&
               Guid.TryParse(parts[1], out _);
    }

    private static bool IsUserBlockType(string type) =>
        string.Equals(type, "Input", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Process", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Output", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Decision", StringComparison.OrdinalIgnoreCase);
}
