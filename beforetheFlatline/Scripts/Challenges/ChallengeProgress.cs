using Godot;
using System.Collections.Generic;
using SchoolYardGame;

/// <summary>
/// Daily challenge completion record. Progress is tracked by in-game day so each new day
/// presents a fresh challenge set. A challenge rewards sanity only the first time it is solved that day.
/// </summary>
public static class ChallengeProgress
{
    private static readonly Dictionary<int, HashSet<string>> CompletedByDay = new();

    public const int SanityReward = 10;
    public const string SequenceId = "sequence";
    public const string ConditionId = "condition";
    public const string FlowchartId = "flowchart";
    public const string FinalId = "final";

    private static int CurrentDay => DayClock.Instance?.DayNumber ?? 1;

    private static HashSet<string> GetCurrentDaySet()
    {
        int day = CurrentDay;
        if (!CompletedByDay.TryGetValue(day, out var completed))
        {
            completed = new HashSet<string>();
            CompletedByDay[day] = completed;
        }

        return completed;
    }

    public static bool IsCompleted(string id) =>
        GetCurrentDaySet().Contains(id.ToLowerInvariant());

    public static void MarkCompleted(string id)
    {
        bool newlyCompleted = GetCurrentDaySet().Add(id.ToLowerInvariant());
        if (newlyCompleted)
            SanityManager.Instance?.Recover(SanityReward);
    }

    public static bool AllCompleted =>
        IsCompleted(SequenceId) &&
        IsCompleted(ConditionId) &&
        IsCompleted(FlowchartId) &&
        IsCompleted(FinalId);
}
