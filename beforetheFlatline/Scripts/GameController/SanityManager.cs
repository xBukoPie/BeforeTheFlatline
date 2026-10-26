using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

public partial class SanityManager : Node
{
    public static SanityManager Instance { get; private set; }

    public const int MaxSanity = 100;
    public int FailedTestCount { get; private set; }
    public int DifficultyPenalty { get; private set; }
    public int CurrentSanity { get; private set; } = MaxSanity;

    public enum DebuffType
    {
        Dizziness,
        Confusion,
        Anxiety,
        Pressure
    }

    private readonly Dictionary<DebuffType, int> _debuffs = new();

    public event Action<int> OnSanityChanged;
    public event Action<DebuffType, int> OnDebuffChanged;

    public override void _Ready()
    {
        // Only the project Autoload should own the singleton reference. World.tscn also
        // contains a legacy child with this script, but it must not replace the Autoload.
        if (GetParent() == GetTree().Root)
            Instance = this;

        foreach (DebuffType d in Enum.GetValues(typeof(DebuffType)))
        {
            _debuffs[d] = 0;
        }
    }

    public override void _ExitTree()
    {
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
        int clamped = Mathf.Clamp(amount, 1, 10);
        CurrentSanity = Mathf.Min(CurrentSanity + clamped, MaxSanity);
        OnSanityChanged?.Invoke(CurrentSanity);
    }

    public void ApplyDebuff(string source, DebuffType type, int intensity)
    {
        _debuffs[type] = Mathf.Min(_debuffs[type] + intensity, 100);
        CurrentSanity = Mathf.Max(CurrentSanity - intensity, 0);
        OnDebuffChanged?.Invoke(type, _debuffs[type]);
        OnSanityChanged?.Invoke(CurrentSanity);
    }

    public int GetDebuffLevel(DebuffType type)
    {
        return _debuffs[type];
    }
}
