using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Owns run-wide sanity and the independent intensity of each debuff.
/// Debuffs lose one intensity point per in-game hour; reaching zero sanity ends the run.
/// </summary>
public partial class SanityManager : Node
{
    public static SanityManager Instance { get; private set; }

    public const int MaxSanity = 100;
    public const int StableSanityThreshold = 70;
    private const string BadEndingScenePath = "res://Scenes/Ending/BadEnding.tscn";
    private const int DebuffDecayPerGameHour = 1;

    public int FailedTestCount { get; private set; }
    public int DifficultyPenalty { get; private set; }
    public int CurrentSanity { get; private set; } = MaxSanity;
    public bool BadEndingQueued => _badEndingQueued;

    public enum DebuffType
    {
        Dizziness,
        Confusion,
        Anxiety,
        Pressure
    }

    private readonly Dictionary<DebuffType, int> _debuffs = new();
    private bool _badEndingQueued;
    private float _lastClockHour;
    private float _decayHourRemainder;

    public event Action<int> OnSanityChanged;
    public event Action<DebuffType, int> OnDebuffChanged;

    public override void _Ready()
    {
        // The project Autoload is the single source of truth. World scenes may still contain
        // legacy SanityManager children; they must not reset or subscribe to the run state.
        if (GetParent() != GetTree().Root)
            return;

        Instance = this;
        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            if (!_debuffs.ContainsKey(type))
                _debuffs[type] = 0;
        }

        if (DayClock.Instance != null)
        {
            _lastClockHour = DayClock.Instance.Hour;
            DayClock.Instance.TimeChanged += OnClockTimeChanged;
        }

        OnSanityChanged?.Invoke(CurrentSanity);
    }

    public override void _ExitTree()
    {
        if (GetParent() == GetTree().Root && DayClock.Instance != null)
            DayClock.Instance.TimeChanged -= OnClockTimeChanged;

        if (Instance == this)
            Instance = null;
    }

    /// <summary>Each failed assessment slightly raises the difficulty of later tasks, capped at two tiers.</summary>
    public void RegisterFailedTest()
    {
        FailedTestCount++;
        DifficultyPenalty = Mathf.Clamp(DifficultyPenalty + 1, 0, 2);
    }

    public void Recover(int amount)
    {
        if (_badEndingQueued || CurrentSanity <= 0)
            return;

        int clamped = Mathf.Clamp(amount, 1, 10);
        CurrentSanity = Mathf.Min(CurrentSanity + clamped, MaxSanity);
        OnSanityChanged?.Invoke(CurrentSanity);
    }

    public void ApplyDebuff(string source, DebuffType type, int intensity)
    {
        if (_badEndingQueued || intensity <= 0)
            return;

        int previous = GetDebuffLevel(type);
        _debuffs[type] = Mathf.Clamp(previous + intensity, 0, 100);

        // Sanity is a run resource, separate from the debuff's slowly decaying intensity.
        CurrentSanity = Mathf.Max(CurrentSanity - intensity, 0);
        OnDebuffChanged?.Invoke(type, _debuffs[type]);
        OnSanityChanged?.Invoke(CurrentSanity);

        if (CurrentSanity == 0)
            QueueBadEnding();
    }

    public int GetDebuffLevel(DebuffType type) =>
        _debuffs.TryGetValue(type, out int value) ? value : 0;

    public int GetTotalDebuffLevel()
    {
        int total = 0;
        foreach (int level in _debuffs.Values)
            total += level;
        return total;
    }

    public bool HasActiveDebuffs => GetTotalDebuffLevel() > 0;

    /// <summary>Starts a completely fresh run after the player returns to the title screen.</summary>
    public void ResetForNewRun()
    {
        CurrentSanity = MaxSanity;
        FailedTestCount = 0;
        DifficultyPenalty = 0;
        _badEndingQueued = false;
        _decayHourRemainder = 0f;

        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            _debuffs[type] = 0;
            OnDebuffChanged?.Invoke(type, 0);
        }

        _lastClockHour = DayClock.Instance?.Hour ?? 8f;
        OnSanityChanged?.Invoke(CurrentSanity);
    }

    private void OnClockTimeChanged(float hour)
    {
        float elapsedHours = hour - _lastClockHour;
        if (elapsedHours < 0f)
            elapsedHours += 24f;

        _lastClockHour = hour;
        if (elapsedHours <= 0f || elapsedHours > 24f)
            return;

        _decayHourRemainder += elapsedHours;
        int wholeHours = Mathf.FloorToInt(_decayHourRemainder);
        if (wholeHours <= 0)
            return;

        _decayHourRemainder -= wholeHours;
        DecayDebuffs(wholeHours * DebuffDecayPerGameHour);
    }

    private void DecayDebuffs(int amount)
    {
        if (amount <= 0)
            return;

        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            int oldLevel = GetDebuffLevel(type);
            if (oldLevel <= 0)
                continue;

            int newLevel = Mathf.Max(0, oldLevel - amount);
            if (newLevel == oldLevel)
                continue;

            _debuffs[type] = newLevel;
            OnDebuffChanged?.Invoke(type, newLevel);
        }
    }

    private void QueueBadEnding()
    {
        if (_badEndingQueued)
            return;

        _badEndingQueued = true;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        // Defer the scene swap so HUD and overlay listeners finish processing the
        // sanity/debuff change before the ending replaces the current scene.
        if (!ResourceLoader.Exists(BadEndingScenePath))
        {
            GD.PushError($"SanityManager: bad-ending scene was not found at '{BadEndingScenePath}'.");
            return;
        }

        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, BadEndingScenePath);
    }
}
