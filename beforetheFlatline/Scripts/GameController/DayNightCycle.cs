using Godot;

namespace SchoolYardGame;

/// <summary>
/// Tints the whole map with a day/night colour ramp. Other scripts read DayNightCycle.Darkness
/// (0 = full day, 1 = full night) to fade lamps and window lights.
/// </summary>
public partial class DayNightCycle : CanvasModulate
{
    public static DayNightCycle Instance { get; private set; }
    public static float Darkness => GameController.Darkness;

    [Export] public float DayLengthSeconds = 180f;   // real seconds per 24 in-game hours
    [Export(PropertyHint.Range, "0,24,0.25")] public float StartHour = 16.5f;
    [Export] public bool Running = true;

    public float Hour { get; private set; }

    private static readonly (float hour, Color color)[] Keys =
    {
        (0f,  new Color("1a2144")),
        (5f,  new Color("232b57")),
        (6.5f,new Color("e8a77a")),   // sunrise
        (8f,  new Color("ffffff")),
        (15f, new Color("ffffff")),
        (17f, new Color("ffd9a8")),   // golden hour
        (18.5f,new Color("c27d86")),  // sunset
        (20f, new Color("2c3463")),
        (24f, new Color("1a2144")),
    };

    public override void _Ready()
    {
        Instance = this;
        Hour = StartHour;
        Apply();
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("time_skip")) Hour = (Hour + 3f) % 24f;
        if (Running) Hour = (Hour + (float)delta * 24f / DayLengthSeconds) % 24f;
        Apply();
    }

    private void Apply()
    {
        Color = Sample(Hour);
        // darkness from colour luminance of the ramp
        float lum = (Color.R + Color.G + Color.B) / 3f;
        // Darkness is managed by GameController – no assignment here

        var label = GetNodeOrNull<Label>("../HUD/Clock");
        if (label != null)
        {
            int h = (int)Hour; int m = (int)((Hour - h) * 60);
            label.Text = $"{h:00}:{m:00}   [T] skip 3h";
        }
    }

    private static Color Sample(float h)
    {
        for (int i = 0; i < Keys.Length - 1; i++)
        {
            if (h >= Keys[i].hour && h <= Keys[i + 1].hour)
            {
                float t = (h - Keys[i].hour) / (Keys[i + 1].hour - Keys[i].hour);
                return Keys[i].color.Lerp(Keys[i + 1].color, t);
            }
        }
        return Colors.White;
    }
}
