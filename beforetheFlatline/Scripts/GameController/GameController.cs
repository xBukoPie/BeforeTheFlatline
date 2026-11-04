using Godot;

namespace SchoolYardGame;

public partial class GameController : CanvasModulate
{
    public static GameController Instance { get; private set; }
    public static float Darkness { get; private set; }

    [Export] public float DayLengthSeconds { get; set; } = 180f;
    public float Hour { get; private set; }
    private bool _hasColorSample;

    private static readonly (float hour, Color color)[] Keys =
    {
        (0f, new Color("202644")),
        (5f, new Color("303454")),
        (6.5f, new Color("ad8779")),
        (8f, new Color("c5c2bb")),
        (15f, new Color("c5c2bb")),
        (17f, new Color("bba184")),
        (18.5f, new Color("956a79")),
        (20f, new Color("333957")),
        (24f, new Color("202644")),
    };

    public override void _Ready()
    {
        Instance = this;
        if (DayClock.Instance != null && DayLengthSeconds > 0f)
            DayClock.Instance.DayLengthSeconds = DayLengthSeconds;
        Apply(0f);
    }

    public override void _Process(double delta) => Apply((float)delta);

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Apply(float delta)
    {
        Hour = DayClock.Instance?.Hour ?? 8f;
        Godot.Color target = Sample(Hour);
        if (!_hasColorSample || delta <= 0f)
        {
            Color = target;
            _hasColorSample = true;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-delta / 3.5f);
            Color = Godot.Color.Lerp(Color, target, blend);
        }

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
        return Keys[0].color;
    }
}
