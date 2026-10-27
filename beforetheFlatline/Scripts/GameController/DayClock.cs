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
    [Export(PropertyHint.Range, "0,24,0.25")] public float SchoolClosingHour { get; set; } = 17f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float NightBeginsHour { get; set; } = 18.5f;

    public float Hour { get; private set; }
    public int DayNumber { get; private set; } = 1;
    public bool SchoolChallengeStartedToday { get; private set; }
    public bool SchoolChallengeCompletedToday { get; private set; }

    private bool _skipSchoolGreetingOnNextLoad;
    private bool _initialized;
    public bool FirstDayMysteryEncounterSeen { get; private set; }
    private bool _firstDaySpecialGreetingPending;
    public bool FirstDayMysteryGreetingPending => _firstDaySpecialGreetingPending;

    /// <summary>Whether the one-time mysterious encounter should play before classroom entry.</summary>
    public bool ShouldShowFirstDayMysteryEncounter =>
        !FirstDayMysteryEncounterSeen || DayNumber == 0;

    /// <summary>Set after the mysterious person exits; the next classroom greeting gets unique choices.</summary>
    public void MarkFirstDayMysteryEncounterCompleted()
    {
        FirstDayMysteryEncounterSeen = true;
        _firstDaySpecialGreetingPending = true;
    }

    public bool ConsumeFirstDaySpecialGreetingPending()
    {
        bool pending = _firstDaySpecialGreetingPending;
        _firstDaySpecialGreetingPending = false;
        return pending;
    }

    public void RestoreFirstDayMysteryState(bool encounterSeen, bool specialGreetingPending)
    {
        FirstDayMysteryEncounterSeen = encounterSeen;
        _firstDaySpecialGreetingPending = specialGreetingPending;
    }

    public bool IsSchoolOpen => Hour >= SchoolOpenHour && Hour < SchoolClosingHour;
    public bool IsNight => Hour < SchoolOpenHour || Hour >= NightBeginsHour;

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
        if (!Running)
            return;

        if (Input.IsActionJustPressed("time_skip"))
            AdvanceHours(3f);

        if (DayLengthSeconds > 0f)
            AdvanceHours((float)delta * 24f / DayLengthSeconds);
    }

    /// <summary>Set the clock to a specific hour without changing the day counter (used by the N-key debug toggle).</summary>
    public void SetHourOfDay(float hour)
    {
        Hour = Mathf.PosMod(hour, 24f);
        EmitSignal(SignalName.TimeChanged, Hour);
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
            SchoolChallengeCompletedToday = false;
            EmitSignal(SignalName.DayChanged, DayNumber);
        }

        Hour = updated;
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    /// <summary>Restore the persistent in-game clock from a save slot.</summary>
    public void RestoreState(int dayNumber, float hour, bool running, bool schoolStarted, bool schoolCompleted)
    {
        DayNumber = Mathf.Max(1, dayNumber);
        Hour = Mathf.PosMod(hour, 24f);
        Running = running;
        SchoolChallengeStartedToday = schoolStarted;
        SchoolChallengeCompletedToday = schoolCompleted;
        _initialized = true;
        EmitSignal(SignalName.DayChanged, DayNumber);
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    /// <summary>Reset the clock and daily school flags when starting a new run from the title screen.</summary>
    public void ResetForNewRun()
    {
        Hour = Mathf.PosMod(StartHour, 24f);
        DayNumber = 1;
        SchoolChallengeStartedToday = false;
        SchoolChallengeCompletedToday = false;
        _skipSchoolGreetingOnNextLoad = false;
        FirstDayMysteryEncounterSeen = false;
        _firstDaySpecialGreetingPending = false;
        EmitSignal(SignalName.DayChanged, DayNumber);
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    public void MarkSchoolChallengeStartedToday()
    {
        SchoolChallengeStartedToday = true;
    }

    public void MarkSchoolChallengeCompletedToday()
    {
        SchoolChallengeCompletedToday = true;
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
