using Godot;
using System;

public partial class FlowchartSymbolControl : Control
{
    [Export] public string BlockType { get; set; } = "Process";
    [Export] public string InitialText { get; set; } = "";

    private LineEdit _editor;
    private bool _compact;

    public event Action<string> TextChanged;
    public string BlockText => _editor?.Text ?? InitialText;

    public bool IsPointOnShape(Vector2 globalPoint)
    {
        Vector2 point = GetGlobalTransform().AffineInverse() * globalPoint;
        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        const float height = 72f;

        if (point.Y < 1f || point.Y > height - 1f || point.X < 2f || point.X > width - 2f)
            return false;

        if (BlockType == "Decision")
        {
            float halfWidth = Mathf.Min(width * 0.43f, 190f);
            float centerX = width * 0.5f;
            return Mathf.Abs(point.X - centerX) / halfWidth +
                Mathf.Abs(point.Y - height * 0.5f) / (height * 0.5f) <= 1f;
        }

        if (BlockType == "Input" || BlockType == "Output")
        {
            float skew = Mathf.Min(28f, width * 0.07f);
            float progress = Mathf.Clamp((point.Y - 1f) / (height - 2f), 0f, 1f);
            float left = 2f + skew * (1f - progress);
            float right = width - 2f - skew * progress;
            return point.X >= left && point.X <= right;
        }

        return true;
    }

    public void Configure(string type, string text, bool compact = false)
    {
        BlockType = type ?? "Process";
        InitialText = text ?? "";
        _compact = compact;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseFilter = MouseFilterEnum.Pass;
        UpdateShapeSize();
    }

    public override void _Ready()
    {
        _editor = new LineEdit
        {
            Text = InitialText,
            PlaceholderText = BlockType == "Decision" ? "Enter a condition..." : "Define this block...",
            MaxLength = 180,
            Alignment = HorizontalAlignment.Center,
            ClearButtonEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var transparentEditorStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0f),
            BorderColor = new Color(0f, 0f, 0f, 0f),
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            BorderWidthBottom = 0,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        _editor.AddThemeStyleboxOverride("normal", transparentEditorStyle);
        _editor.AddThemeStyleboxOverride("focus", transparentEditorStyle);
        _editor.AddThemeStyleboxOverride("read_only", transparentEditorStyle);
        _editor.AddThemeStyleboxOverride("disabled", transparentEditorStyle);
        _editor.AddThemeColorOverride("font_color", Colors.White);
        _editor.AddThemeColorOverride("font_placeholder_color", new Color("cbd5e1"));
        _editor.TextChanged += value =>
        {
            InitialText = value;
            UpdateShapeSize();
            TextChanged?.Invoke(value);
        };
        AddChild(_editor);
        UpdateShapeSize();
        LayoutEditor();
    }

    public override void _Draw()
    {
        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        float height = 72f;
        Rect2 rect = new Rect2(2f, 1f, width - 4f, height - 2f);

        if (BlockType == "Decision")
        {
            float half = Mathf.Min(width * 0.43f, 190f);
            float center = width * 0.5f;
            Vector2[] points =
            {
                new(center, 1f),
                new(center + half, height * 0.5f),
                new(center, height - 1f),
                new(center - half, height * 0.5f)
            };
            DrawColoredPolygon(points, new Color("92400e"));
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] },
                new Color("fcd34d"), 2f, true);
        }
        else if (BlockType == "Input" || BlockType == "Output")
        {
            float skew = Mathf.Min(28f, width * 0.07f);
            Vector2[] points =
            {
                new(rect.Position.X + skew, rect.Position.Y),
                new(rect.End.X, rect.Position.Y),
                new(rect.End.X - skew, rect.End.Y),
                new(rect.Position.X, rect.End.Y)
            };
            DrawColoredPolygon(points, new Color("0f766e"));
            DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] },
                new Color("99f6e4"), 2f, true);
        }
        else
        {
            DrawRect(rect, new Color("1d4ed8"));
            DrawRect(rect, new Color("93c5fd"), false, 2f);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(12f, 16f), BlockType.ToUpperInvariant(),
            HorizontalAlignment.Left, -1f, 10, new Color("dbeafe"));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree())
            LayoutEditor();
    }

    private void UpdateShapeSize()
    {
        Font font = _editor?.GetThemeFont("font") ?? ThemeDB.FallbackFont;
        float textWidth = font.GetStringSize(InitialText ?? string.Empty,
            HorizontalAlignment.Left, -1f, 16).X;

        float minimum = _compact ? 170f : 190f;
        float maximum = _compact ? 360f : 700f;
        float required = string.Equals(BlockType, "Decision", StringComparison.OrdinalIgnoreCase)
            ? (textWidth + 26f) / 0.46f
            : (textWidth + 34f) / 0.72f;

        float width = Mathf.Clamp(Mathf.Ceil(required), minimum, maximum);
        if (!Mathf.IsEqualApprox(CustomMinimumSize.X, width) ||
            !Mathf.IsEqualApprox(CustomMinimumSize.Y, 72f))
            CustomMinimumSize = new Vector2(width, 72f);

        QueueRedraw();
        if (_editor != null)
            LayoutEditor();
    }

    private void LayoutEditor()
    {
        if (_editor == null)
            return;

        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        float ratio = BlockType == "Decision" ? 0.46f : 0.72f;
        float editorWidth = Mathf.Min(width * ratio, BlockType == "Decision" ? 290f : 520f);
        _editor.Position = new Vector2((width - editorWidth) * 0.5f, 29f);
        _editor.Size = new Vector2(editorWidth, 26f);
    }
}
