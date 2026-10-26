using Godot;
using System.Collections.Generic;

/// <summary>
/// Session-wide record of Algorithm Lab progress.
/// Completing any challenge restores sanity through the autoloaded SanityManager.
/// </summary>
public static class ChallengeProgress
{
    private static readonly HashSet<string> Completed = new();

    public const int SanityReward = 10; // Recover() clamps to a max of 10
    public const string SequenceId = "sequence";
    public const string ConditionId = "condition";
    public const string FlowchartId = "flowchart";
    public const string FinalId = "final";

    public static bool IsCompleted(string id) => Completed.Contains(id.ToLowerInvariant());

    public static void MarkCompleted(string id)
    {
        Completed.Add(id.ToLowerInvariant());
        SchoolYardGame.SanityManager.Instance?.Recover(SanityReward);
    }

    public static bool AllCompleted =>
        IsCompleted(SequenceId) && IsCompleted(ConditionId) &&
        IsCompleted(FlowchartId) && IsCompleted(FinalId);
}