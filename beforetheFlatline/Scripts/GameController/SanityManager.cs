using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Run-wide sanity and per-type debuff stacks. Debuffs clear only after enough sanity is
/// recovered; elapsed in-game time never removes a debuff.
/// </summary>
public partial class SanityManager : Node
{
    public static SanityManager Instance { get; private set; }

    public const int MaxSanity = 100;
    public const int StableSanityThreshold = 70;
    private const string BadEndingScenePath = "res://Scenes/Ending/BadEnding.tscn";

    public int FailedTestCount { get; private set; }
    public int DifficultyPenalty { get; private set; }
    public int CurrentSanity { get; private set; } = MaxSanity;
    public bool BadEndingQueued => _badEndingQueued;

    public enum DebuffType { Dizziness, Confusion, Anxiety, Pressure }

    private readonly Dictionary<DebuffType, int> _debuffs = new();
    private readonly Dictionary<DebuffType, int> _recoveryTargets = new();
    private readonly RandomNumberGenerator _rng = new();
    private bool _badEndingQueued;

    public event Action<int> OnSanityChanged;
    public event Action<DebuffType, int> OnDebuffChanged;

    public override void _Ready()
    {
        // Legacy room children may still reference this script. Only the Autoload owns state.
        if (GetParent() != GetTree().Root)
            return;

        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        _rng.Randomize();
        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            _debuffs[type] = 0;
            _recoveryTargets[type] = MaxSanity;
        }

        OnSanityChanged?.Invoke(CurrentSanity);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterFailedTest()
    {
        FailedTestCount++;
        DifficultyPenalty = Mathf.Clamp(DifficultyPenalty + 1, 0, 2);
    }

    /// <summary>
    /// Restore sanity and clear each debuff independently once the player has recovered the
    /// rolled 30-60 percent of the sanity missing when that debuff was applied.
    /// </summary>
    public void Recover(int amount)
    {
        if (_badEndingQueued || CurrentSanity <= 0 || amount <= 0)
            return;

        int clamped = Mathf.Clamp(amount, 1, MaxSanity);
        int previous = CurrentSanity;
        CurrentSanity = Mathf.Min(CurrentSanity + clamped, MaxSanity);

        if (CurrentSanity > previous)
            ClearRecoveredDebuffs();

        OnSanityChanged?.Invoke(CurrentSanity);
    }

    public void ApplyDebuff(string source, DebuffType type, int intensity)
    {
        if (_badEndingQueued || intensity <= 0)
            return;

        int appliedIntensity = Mathf.Clamp(intensity, 1, MaxSanity);
        _debuffs[type] = Mathf.Clamp(GetDebuffLevel(type) + appliedIntensity, 0, MaxSanity);
        CurrentSanity = Mathf.Max(CurrentSanity - appliedIntensity, 0);

        // Every debuff gets an independent recovery gate. Reapplying the same type refreshes
        // its gate, preventing a newly stacked effect from clearing immediately.
        // Roll the recovery fraction against the sanity still missing after this debuff.
        // This makes the gate achievable even when a small penalty is applied near full sanity.
        int missingSanity = MaxSanity - CurrentSanity;
        int recoveryPercent = _rng.RandiRange(30, 60);
        int recoveryNeeded = Mathf.CeilToInt(missingSanity * recoveryPercent / 100f);
        _recoveryTargets[type] = Mathf.Min(MaxSanity, CurrentSanity + recoveryNeeded);

        OnDebuffChanged?.Invoke(type, _debuffs[type]);
        OnSanityChanged?.Invoke(CurrentSanity);

        if (CurrentSanity == 0)
            QueueBadEnding();
    }

    public int GetDebuffLevel(DebuffType type) =>
        _debuffs.TryGetValue(type, out int value) ? value : 0;

    public int GetRecoveryTarget(DebuffType type) =>
        _recoveryTargets.TryGetValue(type, out int value) ? value : MaxSanity;

    public int GetTotalDebuffLevel()
    {
        int total = 0;
        foreach (int level in _debuffs.Values)
            total += level;
        return total;
    }

    public bool HasActiveDebuffs => GetTotalDebuffLevel() > 0;

    public void ResetForNewRun()
    {
        CurrentSanity = MaxSanity;
        FailedTestCount = 0;
        DifficultyPenalty = 0;
        _badEndingQueued = false;

        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            _debuffs[type] = 0;
            _recoveryTargets[type] = MaxSanity;
            OnDebuffChanged?.Invoke(type, 0);
        }

        OnSanityChanged?.Invoke(CurrentSanity);
    }

    private void ClearRecoveredDebuffs()
    {
        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            int level = GetDebuffLevel(type);
            if (level <= 0 || CurrentSanity < GetRecoveryTarget(type))
                continue;

            _debuffs[type] = 0;
            _recoveryTargets[type] = MaxSanity;
            OnDebuffChanged?.Invoke(type, 0);
        }
    }

    private void QueueBadEnding()
    {
        if (_badEndingQueued)
            return;

        _badEndingQueued = true;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        // The persistent effect overlay gets a short fade before the transition takes over.
        if (StatusEffectsOverlay.Instance != null)
        {
            StatusEffectsOverlay.Instance.FadeForBadEnding(BadEndingScenePath);
            return;
        }

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(BadEndingScenePath);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, BadEndingScenePath);
    }
}
