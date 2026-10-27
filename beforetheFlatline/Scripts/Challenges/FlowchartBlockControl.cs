using Godot;
using System;

public partial class FlowchartBlockControl : HBoxContainer
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string InitialText { get; set; } = "value = 0";
    private FlowchartSymbolControl _symbol;

    public string BlockText => _symbol?.BlockText ?? InitialText;

    public void Configure(string type, string text, int index, Action<int> moveUp, Action<int> moveDown, Action<int> remove)
    {
        BlockType = type;
        InitialText = text;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(0, 80);
        AddThemeConstantOverride("separation", 6);

        _symbol = new FlowchartSymbolControl();
        _symbol.Configure(type, text);
        AddChild(_symbol);

        AddControlButton("↑", index, moveUp, index == 0);
        AddControlButton("↓", index, moveDown, false);
        AddControlButton("×", index, remove, false);
    }

    private void AddControlButton(string caption, int index, Action<int> callback, bool disabled)
    {
        var button = new Button
        {
            Text = caption,
            CustomMinimumSize = new Vector2(30, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Disabled = disabled || callback == null,
            TooltipText = caption == "×" ? "Remove block" : "Reorder block"
        };
        if (callback != null)
            button.Pressed += () => callback(index);
        AddChild(button);
    }
}
