using Godot;
using SchoolYardGame;

public partial class HUDController : CanvasLayer
{
    private ProgressBar _sanityBar;

    public override void _Ready()
    {
        // Find the sanity bar under this CanvasLayer
        _sanityBar = GetNode<ProgressBar>("SanityBar");
        // Subscribe to SanityManager events
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnSanityChanged += UpdateSanity;
        }
        // Initialize bar
        UpdateSanity(SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity);
    }

    private void UpdateSanity(int current)
    {
        if (_sanityBar != null)
        {
            _sanityBar.MaxValue = SanityManager.MaxSanity;
            _sanityBar.Value = current;
        }
    }
}
