using Godot;
using SchoolYardGame;
public partial class SanityHUD : ProgressBar
{
    public override void _Ready()
    {
        // Subscribe to SanityManager events
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnSanityChanged += UpdateSanity;
        }
        // Initialize
        UpdateSanity(SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity);
    }

    private void UpdateSanity(int current)
    {
        MaxValue = SanityManager.MaxSanity;
        Value = current;
    }

    public override void _ExitTree()
    {
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnSanityChanged -= UpdateSanity;
        }
    }
}
