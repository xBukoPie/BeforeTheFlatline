using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

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

    // Restore sanity and clear each debuff independently once the player has recovered the rolled 30-60….
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

        int missingSanity = MaxSanity - CurrentSanity;
        int recoveryPercent = _rng.RandiRange(30, 60);
        int recoveryNeeded = Mathf.CeilToInt(missingSanity * recoveryPercent / 100f);
        _recoveryTargets[type] = Mathf.Min(MaxSanity, CurrentSanity + recoveryNeeded);

        OnDebuffChanged?.Invoke(type, _debuffs[type]);
        OnSanityChanged?.Invoke(CurrentSanity);

        if (CurrentSanity == 0)
            QueueBadEnding();
    }

    // Capture sanity, failure penalties, active debuffs, and their recovery gates.
    public Godot.Collections.Dictionary CreateSaveData()
    {
        var debuffs = new Godot.Collections.Dictionary();
        var targets = new Godot.Collections.Dictionary();

        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            debuffs[type.ToString()] = GetDebuffLevel(type);
            targets[type.ToString()] = GetRecoveryTarget(type);
        }

        return new Godot.Collections.Dictionary
        {
            ["current_sanity"] = CurrentSanity,
            ["failed_test_count"] = FailedTestCount,
            ["difficulty_penalty"] = DifficultyPenalty,
            ["debuffs"] = debuffs,
            ["recovery_targets"] = targets
        };
    }

    // Restore saved sanity data and refresh UI subscribers.
    public void RestoreFromSaveData(Godot.Collections.Dictionary data)
    {
        CurrentSanity = Mathf.Clamp(ReadInt(data, "current_sanity", MaxSanity), 1, MaxSanity);
        FailedTestCount = Mathf.Max(0, ReadInt(data, "failed_test_count", 0));
        DifficultyPenalty = Mathf.Clamp(ReadInt(data, "difficulty_penalty", 0), 0, 2);
        _badEndingQueued = false;

        var debuffs = ReadDictionary(data, "debuffs");
        var targets = ReadDictionary(data, "recovery_targets");

        foreach (DebuffType type in Enum.GetValues(typeof(DebuffType)))
        {
            string key = type.ToString();
            _debuffs[type] = Mathf.Clamp(ReadInt(debuffs, key, 0), 0, MaxSanity);
            _recoveryTargets[type] = Mathf.Clamp(ReadInt(targets, key, MaxSanity), 0, MaxSanity);
            OnDebuffChanged?.Invoke(type, _debuffs[type]);
        }

        OnSanityChanged?.Invoke(CurrentSanity);
    }

    private static Godot.Collections.Dictionary ReadDictionary(
        Godot.Collections.Dictionary data, string key)
    {
        if (!data.ContainsKey(key))
            return new Godot.Collections.Dictionary();

        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    private static int ReadInt(Godot.Collections.Dictionary data, string key, int fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        if (value.VariantType == Variant.Type.Int)
            return value.AsInt32();

        if (value.VariantType == Variant.Type.Float)
        {
            double number = value.AsDouble();
            if (number >= int.MinValue && number <= int.MaxValue &&
                Math.Abs(number - Math.Round(number)) < 0.000001)
                return (int)Math.Round(number);
            return fallback;
        }

        if (value.VariantType == Variant.Type.String &&
            double.TryParse(value.AsString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double parsed) &&
            parsed >= int.MinValue && parsed <= int.MaxValue &&
            Math.Abs(parsed - Math.Round(parsed)) < 0.000001)
            return (int)Math.Round(parsed);

        return fallback;
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
