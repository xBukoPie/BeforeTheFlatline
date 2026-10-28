using Godot;
using SchoolYardGame;

public partial class HUDController : CanvasLayer
{
    private ProgressBar _sanityBar;
    private Label _clock;
    private ProgressBar _visualSanityBar;
    private Label _sanityValue;
    private Label _clockVisual;
    private Label _clockState;

    public override void _Ready()
    {
        _sanityBar = GetNodeOrNull<ProgressBar>("SanityBar");
        _clock = GetNodeOrNull<Label>("Clock");
        if (_sanityBar != null) _sanityBar.Visible = false;
        if (_clock != null) _clock.Visible = false;

        BuildVitalsPanel();

        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnSanityChanged += UpdateSanity;
            UpdateSanity(SanityManager.Instance.CurrentSanity);
        }

        UpdateClock();
    }

    public override void _Process(double delta) => UpdateClock();

    public override void _ExitTree()
    {
        if (SanityManager.Instance != null)
            SanityManager.Instance.OnSanityChanged -= UpdateSanity;
    }

    private void BuildVitalsPanel()
    {
        var panel = new PanelContainer
        {
            Name = "VitalsHUD",
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 14,
            OffsetRight = 274,
            OffsetTop = 14,
            OffsetBottom = 112,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("10131bea"),
            BorderColor = new Color("8976a8"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 9,
            ContentMarginBottom = 9
        });
        AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 5);
        stack.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(stack);

        var sanityHeader = new HBoxContainer();
        sanityHeader.AddThemeConstantOverride("separation", 12);
        stack.AddChild(sanityHeader);

        var label = new Label { Text = "SANITY", MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color("c7b9e4"));
        sanityHeader.AddChild(label);

        _sanityValue = new Label
        {
            Text = "100 / 100",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _sanityValue.AddThemeFontSizeOverride("font_size", 12);
        _sanityValue.AddThemeColorOverride("font_color", new Color("f3effa"));
        sanityHeader.AddChild(_sanityValue);

        _visualSanityBar = new ProgressBar
        {
            Name = "SanityMeter",
            MinValue = 0,
            MaxValue = SanityManager.MaxSanity,
            Value = SanityManager.MaxSanity,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(220, 10),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _visualSanityBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color("292336"),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });
        _visualSanityBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color("aa8add"),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });
        stack.AddChild(_visualSanityBar);

        var clockRow = new HBoxContainer();
        clockRow.AddThemeConstantOverride("separation", 14);
        stack.AddChild(clockRow);

        _clockVisual = new Label
        {
            Text = "DAY 01  •  08:00",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _clockVisual.AddThemeFontSizeOverride("font_size", 13);
        _clockVisual.AddThemeColorOverride("font_color", new Color("e1e0e8"));
        clockRow.AddChild(_clockVisual);

        _clockState = new Label
        {
            Text = "RUNNING",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _clockState.AddThemeFontSizeOverride("font_size", 11);
        _clockState.AddThemeColorOverride("font_color", new Color("a9a0ba"));
        clockRow.AddChild(_clockState);
    }

    private void UpdateClock()
    {
        if (_clockVisual == null || DayClock.Instance == null)
            return;

        float hour = DayClock.Instance.Hour;
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        _clockVisual.Text = $"DAY {DayClock.Instance.DayNumber:00}  •  {h:00}:{m:00}";

        bool running = DayClock.Instance.Running;
        _clockState.Text = running ? "RUNNING" : "TIME PAUSED";
        _clockState.AddThemeColorOverride("font_color",
            running ? new Color("a9a0ba") : new Color("e4b56b"));
    }

    private void UpdateSanity(int current)
    {
        if (_visualSanityBar == null)
            return;

        _visualSanityBar.MaxValue = SanityManager.MaxSanity;
        _visualSanityBar.Value = current;
        if (_sanityValue != null)
            _sanityValue.Text = $"{current} / {SanityManager.MaxSanity}";

        Color fill = current <= 25 ? new Color("df5066")
            : current <= 50 ? new Color("d79b57")
            : new Color("aa8add");
        _visualSanityBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = fill,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });
    }
}
