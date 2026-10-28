using Godot;
using System.Collections.Generic;
using SchoolYardGame;

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

    // Serializes all daily completions using string day keys for JSON compatibility.
    // Save files are in JSON format, so we convert the integer day keys to strings when saving, and convert them back to integers when loading.
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

    // Restores daily completions from a save file, converting string day keys back to integers.
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

    // Clear cached day completions when a new run starts from the title screen.
    public static void Reset() => CompletedByDay.Clear();
}
