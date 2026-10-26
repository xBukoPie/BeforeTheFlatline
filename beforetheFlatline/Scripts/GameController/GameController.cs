using Godot;

namespace SchoolYardGame;

/// GameController: Allows all scenes to access the time of day/night.
public partial class GameController : CanvasModulate
{
    public static GameController Instance { get; private set; }
    public static float Darkness { get; private set; }

    [Export] public float DayLengthSeconds = 720f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float StartHour = 16.5f;
    [Export] public bool Running = true;

    public float Hour { get; private set; }

    private static readonly (float hour, Color color)[] Keys =
    {
        (0f,  new Color("1a2144")),
        (5f,  new Color("232b57")),
        (6.5f,new Color("e8a77a")),   // Sunrise
        (8f,  new Color("ffffff")),
        (15f, new Color("ffffff")),
        (17f, new Color("ffd9a8")),   // Golden Hour
        (18.5f,new Color("c27d86")),  // Sunset
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
        float lum = (Color.R + Color.G + Color.B) / 3f;
        Darkness = Mathf.Clamp(Mathf.Remap(lum, 0.95f, 0.25f, 0f, 1f), 0f, 1f);
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