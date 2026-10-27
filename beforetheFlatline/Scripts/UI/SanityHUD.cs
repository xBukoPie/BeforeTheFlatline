using Godot;
using SchoolYardGame;

public partial class SanityHUD : ProgressBar
{
    public override void _Ready()
    {
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -180;
        OffsetRight = 180;
        OffsetTop = 24;
        OffsetBottom = 42;
        ShowPercentage = false;
        AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color("292336"),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });
        AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color("aa8add"),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });

        if (SanityManager.Instance != null)
            SanityManager.Instance.OnSanityChanged += UpdateSanity;
        UpdateSanity(SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity);
    }

    private void UpdateSanity(int current)
    {
        MaxValue = SanityManager.MaxSanity;
        Value = current;
        Color fill = current <= 25 ? new Color("df5066")
            : current <= 50 ? new Color("d79b57")
            : new Color("aa8add");
        AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = fill,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        });
    }

    public override void _ExitTree()
    {
        if (SanityManager.Instance != null)
            SanityManager.Instance.OnSanityChanged -= UpdateSanity;
    }
}
