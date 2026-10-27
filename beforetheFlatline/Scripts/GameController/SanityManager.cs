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
    /// Restore sanity at reduced efficiency while debuffs are active. Recovery no longer
    /// removes a debuff quickly: each effect has its own high recovery threshold.
    /// </summary>
    public void Recover(int amount)
    {
        if (_badEndingQueued || CurrentSanity <= 0 || amount <= 0)
            return;

        int clamped = Mathf.Clamp(amount, 1, MaxSanity);
        int activeDebuffCount = 0;
        foreach (int level in _debuffs.Values)
        {
            if (level > 0)
                activeDebuffCount++;
        }

        float recoveryEfficiency = Mathf.Clamp(
            1f - activeDebuffCount * 0.12f - GetTotalDebuffLevel() / 250f,
            0.35f,
            1f);
        int effectiveRecovery = Mathf.Max(1, Mathf.FloorToInt(clamped * recoveryEfficiency));

        int previous = CurrentSanity;
        CurrentSanity = Mathf.Min(CurrentSanity + effectiveRecovery, MaxSanity);

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

        // Debuffs are intentionally persistent. Each effect requires recovering most of the
        // lost sanity, with a minimum 18-point recovery so small losses cannot be cleansed by
        // one visit indoors. Reapplying a type refreshes its independent recovery gate.
        int missingSanity = MaxSanity - CurrentSanity;
        int recoveryPercent = _rng.RandiRange(75, 90);
        int recoveryNeeded = Mathf.Max(18,
            Mathf.CeilToInt(missingSanity * recoveryPercent / 100f));
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

    /// <summary>
    /// Active debuffs have direct gameplay consequences in addition to their screen effects.
    /// Stacks compound, while the lower sanity band adds a smaller additional slowdown.
    /// </summary>
    public float GetMovementSpeedMultiplier()
    {
        float dizziness = DebuffSeverity(DebuffType.Dizziness);
        float confusion = DebuffSeverity(DebuffType.Confusion);
        float anxiety = DebuffSeverity(DebuffType.Anxiety);
        float pressure = DebuffSeverity(DebuffType.Pressure);
        float debuffPenalty = dizziness * 0.20f
            + confusion * 0.16f
            + anxiety * 0.12f
            + pressure * 0.18f;
        float lowSanityPenalty = Mathf.Clamp(
            (StableSanityThreshold - CurrentSanity) / (float)StableSanityThreshold,
            0f,
            1f) * 0.12f;

        return Mathf.Clamp(1f - debuffPenalty - lowSanityPenalty, 0.35f, 1f);
    }

    public float DizzinessSeverity => DebuffSeverity(DebuffType.Dizziness);
    public float ConfusionSeverity => DebuffSeverity(DebuffType.Confusion);

    private float DebuffSeverity(DebuffType type) =>
        Mathf.Clamp(GetDebuffLevel(type) / 16f, 0f, 1f);

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
