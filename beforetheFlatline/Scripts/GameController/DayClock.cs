using Godot;

namespace SchoolYardGame;

/// <summary>
/// Persistent in-game clock. This is an Autoload so time and the day counter continue
/// while scenes change between the world, challenges, and dialogue.
/// </summary>
public partial class DayClock : Node
{
    [Signal]
    public delegate void DayChangedEventHandler(int dayNumber);

    [Signal]
    public delegate void TimeChangedEventHandler(float hour);

    public static DayClock Instance { get; private set; }

    [Export] public float DayLengthSeconds { get; set; } = 180f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float StartHour { get; set; } = 8f;
    [Export] public bool Running { get; set; } = true;
    [Export(PropertyHint.Range, "0,24,0.25")] public float SchoolOpenHour { get; set; } = 6.5f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float SchoolClosingHour { get; set; } = 18.5f;

    public float Hour { get; private set; }
    public int DayNumber { get; private set; } = 1;
    public bool SchoolChallengeStartedToday { get; private set; }

    private bool _skipSchoolGreetingOnNextLoad;
    private bool _initialized;

    public bool IsSchoolOpen => Hour >= SchoolOpenHour && Hour < SchoolClosingHour;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        if (!_initialized)
        {
            Hour = Mathf.PosMod(StartHour, 24f);
            _initialized = true;
        }

        EmitSignal(SignalName.TimeChanged, Hour);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("time_skip"))
            AdvanceHours(3f);

        if (Running && DayLengthSeconds > 0f)
            AdvanceHours((float)delta * 24f / DayLengthSeconds);
    }

    public void AdvanceHours(float amount)
    {
        if (amount <= 0f)
            return;

        float updated = Hour + amount;
        while (updated >= 24f)
        {
            updated -= 24f;
            DayNumber++;
            SchoolChallengeStartedToday = false;
            EmitSignal(SignalName.DayChanged, DayNumber);
        }

        Hour = updated;
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    public void MarkSchoolChallengeStartedToday()
    {
        SchoolChallengeStartedToday = true;
    }

    public void SkipSchoolGreetingOnNextLoad()
    {
        _skipSchoolGreetingOnNextLoad = true;
    }

    public bool ConsumeSkipSchoolGreetingOnNextLoad()
    {
        bool shouldSkip = _skipSchoolGreetingOnNextLoad;
        _skipSchoolGreetingOnNextLoad = false;
        return shouldSkip;
    }
}
