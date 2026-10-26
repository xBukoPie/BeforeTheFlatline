using Godot;
using System;
using System.Collections.Generic;

/// <summary>Drop target that renders placed flowchart blocks in execution order.</summary>
public partial class FlowchartDropCanvas : PanelContainer
{
    public event Action<string> BlockDropped;
    private readonly VBoxContainer _blocks = new();

    public FlowchartDropCanvas()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("1e293b"),
            BorderColor = new Color("475569"),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 12, ContentMarginBottom = 12
        });
        AddChild(_blocks);
        ShowEmptyMessage();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType == Variant.Type.String)
            BlockDropped?.Invoke(data.AsString());
    }

    public void ShowBlocks(IEnumerable<string> blocks)
    {
        ClearBlocks();
        int index = 1;
        foreach (string block in blocks)
        {
            var label = new Label
            {
                Text = $"{index}.  {block}",
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 34)
            };
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("334155") });
            _blocks.AddChild(label);
            index++;
        }
        if (index == 1)
            ShowEmptyMessage();
    }

    public void ClearBlocks()
    {
        foreach (Node child in _blocks.GetChildren())
        {
            _blocks.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void ShowEmptyMessage()
    {
        _blocks.AddChild(new Label { Text = "Drop blocks here", HorizontalAlignment = HorizontalAlignment.Center });
    }
}
