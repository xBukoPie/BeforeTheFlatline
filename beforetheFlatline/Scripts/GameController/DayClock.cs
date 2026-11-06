using Godot;

namespace SchoolYardGame;

public partial class DayClock : Node
{
    [Signal]
    public delegate void DayChangedEventHandler(int dayNumber);

    [Signal]
    public delegate void TimeChangedEventHandler(float hour);

    public static DayClock Instance { get; private set; }

    [Export] public float DayLengthSeconds { get; set; } = 600f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float StartHour { get; set; } = 8f;
    [Export] public bool Running { get; set; } = true;
    [Export(PropertyHint.Range, "0,24,0.25")] public float SchoolOpenHour { get; set; } = 6.5f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float SchoolEntryDeadlineHour { get; set; } = 12f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float SchoolClosingHour { get; set; } = 17f;
    [Export(PropertyHint.Range, "0,24,0.25")] public float NightBeginsHour { get; set; } = 18.5f;

    public float Hour { get; private set; }
    public int DayNumber { get; private set; } = 1;
    public bool SchoolChallengeStartedToday { get; private set; }
    public bool SchoolChallengeCompletedToday { get; private set; }
    public bool SchoolDeadlinePenaltyAppliedToday { get; private set; }
    public int WrongRoomAttemptsToday { get; private set; }

    private readonly RandomNumberGenerator _activityRng = new();
    private bool _tvAvailableToday = true;
    private bool _bicycleAvailableToday = true;
    private bool _bathAvailableToday = true;
    private bool _tvUsedToday;
    private bool _bicycleUsedToday;
    private bool _bathUsedToday;

    private bool _skipSchoolGreetingOnNextLoad;
    private bool _initialized;
    public bool FirstDayMysteryEncounterSeen { get; private set; }
    private bool _firstDaySpecialGreetingPending;
    public bool FirstDayMysteryGreetingPending => _firstDaySpecialGreetingPending;

    public bool ShouldShowFirstDayMysteryEncounter => !FirstDayMysteryEncounterSeen;

    // Set after the mysterious person exits; the next classroom greeting gets unique choices.
    public void MarkFirstDayMysteryEncounterCompleted()
    {
        FirstDayMysteryEncounterSeen = true;
        _firstDaySpecialGreetingPending = false;
    }

    public bool ConsumeFirstDaySpecialGreetingPending()
    {
        bool pending = _firstDaySpecialGreetingPending && !FirstDayMysteryEncounterSeen;
        _firstDaySpecialGreetingPending = false;
        return pending;
    }

    public void RestoreFirstDayMysteryState(bool encounterSeen, bool specialGreetingPending)
    {
        FirstDayMysteryEncounterSeen = encounterSeen;
        // Old saves may contain both flags. A completed mystery encounter always wins.
        _firstDaySpecialGreetingPending = specialGreetingPending && !encounterSeen;
    }

    public bool IsSchoolOpen => Hour >= SchoolOpenHour && Hour < SchoolEntryDeadlineHour;
    public bool IsNight => Hour < SchoolOpenHour || Hour >= NightBeginsHour;

    public bool CanSleep =>
        (Hour >= SchoolClosingHour || Hour < SchoolOpenHour) &&
        (DayNumber > 1 || SchoolChallengeCompletedToday);

    // Advance directly to the next school morning. Sleeping after midnight wakes
    // the player later that same morning; sleeping in the evening advances a day.
    public bool SleepUntilSchoolMorning()
    {
        if (!CanSleep)
            return false;

        float hoursUntilMorning = Mathf.PosMod(SchoolOpenHour - Hour, 24f);
        if (hoursUntilMorning < 0.01f)
            hoursUntilMorning = 24f;

        AdvanceHours(hoursUntilMorning);
        Running = true;
        return true;
    }

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        _activityRng.Randomize();
        RollDailyHouseActivities();
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

        // T is a developer-only time control. The value is entered as whole minutes
        // in the SuperUser panel (60 minutes = 1 in-game hour).
        if (Input.IsActionJustPressed("time_skip") &&
            global::SuperUserAccess.DeveloperAccessAvailable)
        {
            AdvanceMinutes(global::SuperUserAccess.RequestedTimeSkipMinutes);
        }

        // Keep the mandatory first school day frozen for normal players.
        if (DayNumber == 1 && !SchoolChallengeCompletedToday)
            return;

        if (DayLengthSeconds > 0f)
            AdvanceHours((float)delta * 24f / DayLengthSeconds);
    }

    // Set the clock to a specific hour without changing the day counter (used by the N-key debug toggle).
    public void SetHourOfDay(float hour)
    {
        Hour = Mathf.PosMod(hour, 24f);
        CheckSchoolDeadlinePenalty();
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    public void AdvanceMinutes(int minutes)
    {
        if (minutes <= 0)
            return;

        AdvanceHours(minutes / 60f);
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
            SchoolDeadlinePenaltyAppliedToday = false;
            WrongRoomAttemptsToday = 0;
            RollDailyHouseActivities();
            EmitSignal(SignalName.DayChanged, DayNumber);
        }

        Hour = updated;
        CheckSchoolDeadlinePenalty();
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    // Restore the persistent in-game clock from a save slot.
    public void RestoreState(int dayNumber, float hour, bool running, bool schoolStarted, bool schoolCompleted)
    {
        DayNumber = Mathf.Max(1, dayNumber);
        Hour = Mathf.PosMod(hour, 24f);
        Running = running;
        SchoolChallengeStartedToday = schoolStarted;
        SchoolChallengeCompletedToday = schoolCompleted;
        SchoolDeadlinePenaltyAppliedToday = false;
        _initialized = true;
        EmitSignal(SignalName.DayChanged, DayNumber);
        EmitSignal(SignalName.TimeChanged, Hour);
    }

    public Godot.Collections.Dictionary CreateHouseActivitySaveData()
    {
        return new Godot.Collections.Dictionary
        {
            ["tv_available"] = _tvAvailableToday,
            ["bicycle_available"] = _bicycleAvailableToday,
            ["bath_available"] = _bathAvailableToday,
            ["tv_used"] = _tvUsedToday,
            ["bicycle_used"] = _bicycleUsedToday,
            ["bath_used"] = _bathUsedToday
        };
    }

    public void RestoreHouseActivityState(Godot.Collections.Dictionary data)
    {
        if (data == null || data.Count == 0)
        {
            RollDailyHouseActivities();
            return;
        }

        _tvAvailableToday = ReadActivityFlag(data, "tv_available", _tvAvailableToday);
        _bicycleAvailableToday = ReadActivityFlag(data, "bicycle_available", _bicycleAvailableToday);
        _bathAvailableToday = ReadActivityFlag(data, "bath_available", _bathAvailableToday);
        _tvUsedToday = ReadActivityFlag(data, "tv_used", false);
        _bicycleUsedToday = ReadActivityFlag(data, "bicycle_used", false);
        _bathUsedToday = ReadActivityFlag(data, "bath_used", false);
    }

    public void RestoreSchoolDeadlinePenaltyState(bool alreadyApplied)
    {
        SchoolDeadlinePenaltyAppliedToday = alreadyApplied;
        CheckSchoolDeadlinePenalty();
    }

    public int RegisterWrongRoomAttempt()
    {
        WrongRoomAttemptsToday = Mathf.Clamp(WrongRoomAttemptsToday + 1, 0, 99);
        return WrongRoomAttemptsToday;
    }

    public void RestoreWrongRoomAttempts(int count)
    {
        WrongRoomAttemptsToday = Mathf.Clamp(count, 0, 99);
    }

    public bool IsHouseActivityRolledAvailable(string activityId) =>
        NormalizeActivityId(activityId) switch
        {
            "tv" => _tvAvailableToday,
            "bicycle" => _bicycleAvailableToday,
            "bath" => _bathAvailableToday,
            _ => false
        };

    public bool HasUsedHouseActivity(string activityId) =>
        NormalizeActivityId(activityId) switch
        {
            "tv" => _tvUsedToday,
            "bicycle" => _bicycleUsedToday,
            "bath" => _bathUsedToday,
            _ => true
        };

    public bool IsHouseActivityAvailableToday(string activityId) =>
        IsHouseActivityRolledAvailable(activityId) && !HasUsedHouseActivity(activityId);

    public bool MarkHouseActivityUsed(string activityId)
    {
        string key = NormalizeActivityId(activityId);
        if (!IsHouseActivityAvailableToday(key))
            return false;

        switch (key)
        {
            case "tv": _tvUsedToday = true; break;
            case "bicycle": _bicycleUsedToday = true; break;
            case "bath": _bathUsedToday = true; break;
            default: return false;
        }

        return true;
    }

    private static string NormalizeActivityId(string activityId) =>
        (activityId ?? string.Empty).Trim().ToLowerInvariant();

    private static bool ReadActivityFlag(Godot.Collections.Dictionary data, string key, bool fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private void RollDailyHouseActivities()
    {
        _tvAvailableToday = _activityRng.RandiRange(0, 99) >= 18;
        _bicycleAvailableToday = _activityRng.RandiRange(0, 99) >= 22;
        _bathAvailableToday = _activityRng.RandiRange(0, 99) >= 16;
        _tvUsedToday = false;
        _bicycleUsedToday = false;
        _bathUsedToday = false;
    }

    private void CheckSchoolDeadlinePenalty()
    {
        if (SchoolDeadlinePenaltyAppliedToday || SchoolChallengeStartedToday ||
            Hour < SchoolEntryDeadlineHour)
            return;

        SchoolDeadlinePenaltyAppliedToday = true;
        SanityManager.Instance?.ApplyDebuff(
            "missed_school", SanityManager.DebuffType.Pressure, 8);
        SanityManager.Instance?.ApplyDebuff(
            "missed_school", SanityManager.DebuffType.Anxiety, 8);

        GD.Print($"[DayClock] School attendance deadline missed on day {DayNumber}; Pressure and Anxiety applied.");
        SaveManager.Instance?.RequestAutoSave();
    }

    // Reset the clock and daily school flags when starting a new run from the title screen.
    public void ResetForNewRun()
    {
        Hour = Mathf.PosMod(StartHour, 24f);
        DayNumber = 1;
        SchoolChallengeStartedToday = false;
        SchoolChallengeCompletedToday = false;
        SchoolDeadlinePenaltyAppliedToday = false;
        WrongRoomAttemptsToday = 0;
        RollDailyHouseActivities();
        _skipSchoolGreetingOnNextLoad = false;
        FirstDayMysteryEncounterSeen = false;
        _firstDaySpecialGreetingPending = false;
        SchoolFriendInteractionHistory.Reset();
        SchoolFriendSpawnHistory.Reset();
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
