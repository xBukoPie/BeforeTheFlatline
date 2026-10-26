using Godot;
using SchoolYardGame;

public partial class HUDController : CanvasLayer
{
    private ProgressBar _sanityBar;
    private Label _clock;

    public override void _Ready()
    {
        float hour = 8f; // Fallback error incase GameController fails.

        if (GameController.Instance != null)
            hour = GameController.Instance.Hour;
        else if (DayNightCycle.Instance != null)
            hour = DayNightCycle.Instance.Hour;

        int h = (int)hour;
        int m = (int)((hour - h) * 60);

        if (_clock != null)
        {
            _clock.Text = $"{h:00}:{m:00}   [T] skip 3h";
        }
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
