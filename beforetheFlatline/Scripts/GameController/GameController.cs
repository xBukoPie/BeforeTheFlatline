using Godot;

namespace SchoolYardGame;

/// <summary>
/// Applies the global clock's time-of-day colors to the room this node belongs to.
/// Actual timekeeping lives in the DayClock Autoload and survives scene changes.
/// </summary>
public partial class GameController : CanvasModulate
{
    public static GameController Instance { get; private set; }
    public static float Darkness { get; private set; }

    // Kept as an exported setting for the School scene's existing configuration.
    [Export] public float DayLengthSeconds { get; set; } = 180f;

    public float Hour { get; private set; }

    private static readonly (float hour, Color color)[] Keys =
    {
        (0f,  new Color("1a2144")),
        (5f,  new Color("232b57")),
        (6.5f,new Color("cf9674")),   // Sunrise
        (8f,  new Color("e3e0da")),
        (15f, new Color("e3e0da")),
        (17f, new Color("d5b18a")),   // Golden hour
        (18.5f,new Color("a96e77")),  // Sunset
        (20f, new Color("2c3463")),
        (24f, new Color("1a2144")),
    };

    public override void _Ready()
    {
        Instance = this;

        // The scene previously owned the clock. Keep its configured day length, but do not
        // reset the hour when the player re-enters school.
        if (DayClock.Instance != null && DayLengthSeconds > 0f)
            DayClock.Instance.DayLengthSeconds = DayLengthSeconds;

        Apply();
    }

    public override void _Process(double delta)
    {
        Apply();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Apply()
    {
        Hour = DayClock.Instance?.Hour ?? 8f;
        Color = Sample(Hour);
        float lum = (Color.R + Color.G + Color.B) / 3f;
        Darkness = Mathf.Clamp(Mathf.Remap(lum, 0.95f, 0.25f, 0f, 1f), 0f, 1f);
    }

    private static Color Sample(float hour)
    {
        for (int i = 0; i < Keys.Length - 1; i++)
        {
            if (hour >= Keys[i].hour && hour <= Keys[i + 1].hour)
            {
                float t = (hour - Keys[i].hour) / (Keys[i + 1].hour - Keys[i].hour);
                return Keys[i].color.Lerp(Keys[i + 1].color, t);
            }
        }

        return Colors.White;
    }
}
