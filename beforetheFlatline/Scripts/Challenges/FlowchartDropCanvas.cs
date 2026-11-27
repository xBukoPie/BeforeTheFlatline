using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FlowchartDropCanvas : PanelContainer
{
    public event Action<string, string> BlockDropped;
    public event Action ContentChanged;

    public int PendingInsertionIndex { get; private set; }

    private const float WorkspaceWidth = 860f;
    private const float MinimumZoom = 0.55f;
    private const float MaximumZoom = 1.65f;

    private readonly List<FlowchartBlockDefinition> _definitions = new();
    private readonly List<FlowchartDropSlotControl> _slots = new();
    private readonly List<Control> _panBlockControls = new();

    private Control _viewport;
    private Control _workspace;
    private VBoxContainer _blocks;
    private float _zoom = 1f;
    private Vector2 _panOffset = Vector2.Zero;
    private bool _isPanning;
    private bool _hasCentered;

    public FlowchartDropCanvas()
    {
        CustomMinimumSize = new Vector2(0, 260);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

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

        _viewport = new Control
        {
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(_viewport);

        _workspace = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(0, 18)
        };
        _viewport.AddChild(_workspace);

        _blocks = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Position = Vector2.Zero,
            CustomMinimumSize = new Vector2(WorkspaceWidth, 0)
        };
        _blocks.AddThemeConstantOverride("separation", 0);
        _workspace.AddChild(_blocks);

        BuildWorkspace();
    }

    public void ClearBlocks()
    {
        _definitions.Clear();
        BuildWorkspace();
        ContentChanged?.Invoke();
    }

    public void SetDefinitions(IReadOnlyList<FlowchartBlockDefinition> definitions)
    {
        _definitions.Clear();
        _definitions.AddRange(FlowchartDefinitionTools.NormalizeLegacy(definitions));
        BuildWorkspace();
        ContentChanged?.Invoke();
    }

    public List<FlowchartBlockDefinition> GetDefinitions() =>
        FlowchartDefinitionTools.DeepClone(_definitions);

    public void AddBlock(string type, string text, int insertIndex = -1)
    {
        if (!IsUserBlockType(type))
            return;

        int index = insertIndex < 0 ? _definitions.Count : Mathf.Clamp(insertIndex, 0, _definitions.Count);
        _definitions.Insert(index, new FlowchartBlockDefinition(type, text));
        BuildWorkspace();
        ContentChanged?.Invoke();
    }

    public void ZoomIn() => SetZoom(_zoom * 1.15f, GetViewportCenter());
    public void ZoomOut() => SetZoom(_zoom / 1.15f, GetViewportCenter());

    public void ResetView()
    {
        _zoom = 1f;
        _panOffset = Vector2.Zero;
        _workspace.Scale = Vector2.One;
        PositionWorkspace();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        IsAcceptedPayload(data);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_slots.Count == 0)
            return;

        Vector2 globalPointer = GetGlobalMousePosition();
        FlowchartDropSlotControl nearest = _slots
            .Where(slot => GodotObject.IsInstanceValid(slot) && slot.IsInsideTree())
            .OrderBy(slot => (slot.GlobalPosition + slot.Size * 0.5f - globalPointer).LengthSquared())
            .FirstOrDefault();

        if (nearest != null)
            ProcessDrop(data, nearest.TargetList, nearest.TargetIndex);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    // A drag that begins on a symbol or terminal is not a canvas pan.
                    if (IsPointerOverPlacedBlock())
                        return;

                    _isPanning = true;
                    AcceptEvent();
                    return;
                }

                if (_isPanning)
                {
                    _isPanning = false;
                    AcceptEvent();
                }
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                SetZoom(_zoom * 1.12f, GetViewportPointer());
                AcceptEvent();
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                SetZoom(_zoom / 1.12f, GetViewportPointer());
                AcceptEvent();
                return;
            }
        }

        if (@event is InputEventMouseMotion motion && _isPanning)
        {
            _panOffset += motion.Relative;
            PositionWorkspace();
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            CallDeferred(nameof(HandleViewportResize));
    }

    private void HandleViewportResize()
    {
        if (!IsInsideTree() || _viewport == null || _workspace == null)
            return;

        if (!_hasCentered)
        {
            _hasCentered = true;
            PositionWorkspace();
        }
    }

    private void BuildWorkspace()
    {
        if (_blocks == null)
            return;

        foreach (Node child in _blocks.GetChildren())
        {
            _blocks.RemoveChild(child);
            child.QueueFree();
        }

        _slots.Clear();
        _panBlockControls.Clear();

        AddTerminal("START");
        AddSlot(_definitions, 0);

        for (int i = 0; i < _definitions.Count; i++)
        {
            var block = new FlowchartBlockControl();
            block.Configure(_definitions[i], ProcessDrop, hasFollowingBlock: i < _definitions.Count - 1);
            block.ContentChanged += () => ContentChanged?.Invoke();
            _panBlockControls.Add(block.SymbolControl);
            _blocks.AddChild(block);
            AddSlot(_definitions, i + 1);
        }

        AddTerminal("END");

        Vector2 minimum = _blocks.GetCombinedMinimumSize();
        minimum.X = Mathf.Max(WorkspaceWidth, minimum.X);
        minimum.Y = Mathf.Max(160f, minimum.Y);
        _blocks.CustomMinimumSize = minimum;
        _blocks.Size = minimum;
        _workspace.CustomMinimumSize = minimum;
        _workspace.Size = minimum;
        _workspace.Scale = Vector2.One * _zoom;
        CallDeferred(nameof(PositionWorkspace));
    }

    private void AddTerminal(string text)
    {
        var center = new CenterContainer
        {
            CustomMinimumSize = new Vector2(WorkspaceWidth, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        var terminal = new FlowchartTerminalControl { TerminalText = text };
        center.AddChild(terminal);
        _panBlockControls.Add(terminal);
        _blocks.AddChild(center);
    }

    private void AddSlot(
        List<FlowchartBlockDefinition> target,
        int index)
    {
        var slot = new FlowchartDropSlotControl();
        slot.Configure(target, index);
        slot.DropReceived += ProcessDrop;
        _slots.Add(slot);
        _blocks.AddChild(slot);
    }

    private void ProcessDrop(
        Variant data,
        List<FlowchartBlockDefinition> targetList,
        int targetIndex)
    {
        if (!IsAcceptedPayload(data) || targetList == null)
            return;

        string[] parts = data.AsString().Split('|', 3);
        int insertIndex = Mathf.Clamp(targetIndex, 0, targetList.Count);

        if (parts.Length == 3 && parts[0] == "ADD")
        {
            if (!IsUserBlockType(parts[1]))
                return;

            targetList.Insert(insertIndex, new FlowchartBlockDefinition(parts[1], parts[2]));
            PendingInsertionIndex = insertIndex;
            BuildWorkspace();
            ContentChanged?.Invoke();
            return;
        }

        if (parts.Length != 2 || parts[0] != "MOVE" || !Guid.TryParse(parts[1], out Guid id))
            return;

        if (!TryFindDefinition(_definitions, id, out List<FlowchartBlockDefinition> sourceList,
                out int sourceIndex, out FlowchartBlockDefinition moving))
            return;

        // A decision cannot be dropped into one of its own descendants.
        if (IsDescendantList(moving, targetList))
            return;

        sourceList.RemoveAt(sourceIndex);
        if (ReferenceEquals(sourceList, targetList) && sourceIndex < insertIndex)
            insertIndex--;

        insertIndex = Mathf.Clamp(insertIndex, 0, targetList.Count);
        targetList.Insert(insertIndex, moving);
        PendingInsertionIndex = insertIndex;
        BuildWorkspace();
        ContentChanged?.Invoke();
    }

    private static bool TryFindDefinition(
        List<FlowchartBlockDefinition> list,
        Guid id,
        out List<FlowchartBlockDefinition> foundList,
        out int foundIndex,
        out FlowchartBlockDefinition found)
    {
        for (int i = 0; i < list.Count; i++)
        {
            FlowchartBlockDefinition current = list[i];
            if (current.Id == id)
            {
                foundList = list;
                foundIndex = i;
                found = current;
                return true;
            }

            if (TryFindDefinition(current.TrueBranch, id, out foundList, out foundIndex, out found) ||
                TryFindDefinition(current.FalseBranch, id, out foundList, out foundIndex, out found))
                return true;
        }

        foundList = null;
        foundIndex = -1;
        found = null;
        return false;
    }

    private static bool IsDescendantList(
        FlowchartBlockDefinition decision,
        List<FlowchartBlockDefinition> candidate)
    {
        if (ReferenceEquals(decision.TrueBranch, candidate) ||
            ReferenceEquals(decision.FalseBranch, candidate))
            return true;

        foreach (FlowchartBlockDefinition child in decision.TrueBranch)
            if (IsDescendantList(child, candidate))
                return true;

        foreach (FlowchartBlockDefinition child in decision.FalseBranch)
            if (IsDescendantList(child, candidate))
                return true;

        return false;
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

    private bool IsPointerOverPlacedBlock()
    {
        Vector2 pointer = GetGlobalMousePosition();
        foreach (Control control in _panBlockControls)
        {
            if (GodotObject.IsInstanceValid(control) &&
                control.IsInsideTree() &&
                control.GetGlobalRect().HasPoint(pointer))
                return true;
        }

        return false;
    }

    private Vector2 GetViewportCenter() => _viewport.Size * 0.5f;

    private Vector2 GetViewportPointer()
    {
        Transform2D inverse = _viewport.GetGlobalTransformWithCanvas().AffineInverse();
        return inverse * GetGlobalMousePosition();
    }

    private void SetZoom(float value, Vector2 anchor)
    {
        float next = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
        if (Mathf.IsEqualApprox(next, _zoom))
            return;

        Vector2 localAnchor = (anchor - _workspace.Position) / _zoom;
        _zoom = next;
        _workspace.Scale = Vector2.One * _zoom;
        _workspace.Position = anchor - localAnchor * _zoom;

        Vector2 center = new Vector2(
            Mathf.Max(0f, (_viewport.Size.X - _workspace.Size.X * _zoom) * 0.5f),
            18f);
        _panOffset = _workspace.Position - center;
    }

    private void PositionWorkspace()
    {
        if (_workspace == null || _viewport == null)
            return;

        _workspace.Scale = Vector2.One * _zoom;
        Vector2 center = new Vector2(
            Mathf.Max(0f, (_viewport.Size.X - _workspace.Size.X * _zoom) * 0.5f),
            18f);
        _workspace.Position = center + _panOffset;
    }
}
