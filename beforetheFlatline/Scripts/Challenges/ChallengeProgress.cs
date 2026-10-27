using Godot;
using System.Collections.Generic;
using SchoolYardGame;

/// <summary>
/// Daily challenge completion record. Progress is tracked by in-game day so each new day
/// presents a fresh challenge set. The result screen handles sanity rewards or penalties after the full assessment.
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

    public static bool MarkCompleted(string id)
    {
        return GetCurrentDaySet().Add(id.ToLowerInvariant());
    }

    public static bool AllCompleted =>
        IsCompleted(SequenceId) &&
        IsCompleted(ConditionId) &&
        IsCompleted(FlowchartId) &&
        IsCompleted(FinalId);

    /// <summary>Serialize all daily completions using string day keys for JSON compatibility.</summary>
    public static Godot.Collections.Dictionary CreateSaveData()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var entry in CompletedByDay)
        {
            var completed = new Godot.Collections.Array();
            foreach (string id in entry.Value)
                completed.Add(id);
            data[entry.Key.ToString()] = completed;
        }
        return data;
    }

    public static void RestoreFromSaveData(Godot.Collections.Dictionary data)
    {
        CompletedByDay.Clear();
        foreach (Variant key in data.Keys)
        {
            if (!int.TryParse(key.AsString(), out int day) || day < 1)
                continue;

            Variant value = data[key];
            if (value.VariantType != Variant.Type.Array)
                continue;

            var completed = new HashSet<string>();
            foreach (Variant item in value.AsGodotArray())
            {
                string id = item.AsString();
                if (!string.IsNullOrWhiteSpace(id))
                    completed.Add(id.ToLowerInvariant());
            }
            CompletedByDay[day] = completed;
        }
    }

    /// <summary>Clear cached day completions when a new run starts from the title screen.</summary>
    public static void Reset() => CompletedByDay.Clear();
}
