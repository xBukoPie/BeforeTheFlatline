using Godot;
using System;

public partial class FlowchartBlockControl : HBoxContainer
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string InitialText { get; set; } = "value = 0";

    private FlowchartSymbolControl _symbol;
    private FlowchartDragHandle _dragHandle;
    private string _branchLabel = "";

    public event Action TextChanged;

    public string BlockText
    {
        get
        {
            string text = _symbol?.BlockText ?? InitialText;
            return string.IsNullOrWhiteSpace(_branchLabel) ? text : $"{_branchLabel}: {text}";
        }
    }

    public void Configure(string type, string text, int index, string branchLabel = "", bool compact = false)
    {
        BlockType = type ?? "Process";
        string normalizedText = text ?? "";
        _branchLabel = NormalizeBranchLabel(branchLabel, ref normalizedText);
        InitialText = normalizedText;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(0, compact ? 94 : 90);
        AddThemeConstantOverride("separation", 8);

        _dragHandle = new FlowchartDragHandle();
        _dragHandle.Configure(index);
        AddChild(_dragHandle);

        _symbol = new FlowchartSymbolControl();
        _symbol.Configure(BlockType, InitialText, compact);
        _symbol.TextChanged += () => TextChanged?.Invoke();
        AddChild(_symbol);
    }

    public void SetDragIndex(int index) => _dragHandle?.Configure(index);

    private static string NormalizeBranchLabel(string requestedLabel, ref string text)
    {
        string label = (requestedLabel ?? "").Trim().ToUpperInvariant();
        if (label != "TRUE" && label != "FALSE")
            label = "";

        string trimmed = (text ?? "").Trim();
        if (trimmed.StartsWith("TRUE:", StringComparison.OrdinalIgnoreCase))
        {
            label = "TRUE";
            trimmed = trimmed.Substring(5).Trim();
        }
        else if (trimmed.StartsWith("FALSE:", StringComparison.OrdinalIgnoreCase))
        {
            label = "FALSE";
            trimmed = trimmed.Substring(6).Trim();
        }

        text = trimmed;
        return label;
    }
}
