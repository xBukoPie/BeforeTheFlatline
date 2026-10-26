using Godot;
using SchoolYardGame;

public partial class HUDController : CanvasLayer
{
    private ProgressBar _sanityBar;
    private Label _clock;

    public override void _Ready()
    {
        _sanityBar = GetNodeOrNull<ProgressBar>("SanityBar");
        _clock = GetNodeOrNull<Label>("Clock");

        if (_sanityBar != null && SanityManager.Instance != null)
        {
            UpdateSanity(SanityManager.Instance.CurrentSanity);
            SanityManager.Instance.OnSanityChanged += UpdateSanity;
        }

        UpdateClock();
    }

    public override void _Process(double delta)
    {
        UpdateClock();
    }

    public override void _ExitTree()
    {
        if (SanityManager.Instance != null)
            SanityManager.Instance.OnSanityChanged -= UpdateSanity;
    }

    private void UpdateClock()
    {
        if (_clock == null || DayClock.Instance == null)
            return;

        float hour = DayClock.Instance.Hour;
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        _clock.Text = $"Day {DayClock.Instance.DayNumber}   {h:00}:{m:00}   [T] +3h";
    }

    private void UpdateSanity(int current)
    {
        if (_sanityBar == null)
            return;

        _sanityBar.MaxValue = SanityManager.MaxSanity;
        _sanityBar.Value = current;
    }
}
