using System;
using System.Collections.Generic;
using System.Linq;

public static class DailyTestSession
{
    private static readonly List<TaskAttemptResult> Attempts = new();
    private static readonly List<string> Notifications = new();
    private static readonly List<AssessmentHistoryEntry> History = new();
    private static int _pointsReceived;
    private static readonly List<string> QuestionOrder = new();
    private static readonly string[] StandardQuestionIds =
    {
        "sequence", "condition", "flowchart", "final"
    };

    private static int _day = 1;
    private static bool _active;
    private static bool _finalized;
    private static bool _failurePenaltyApplied;

    public const int QuestionCount = 4;
    public const int QuestionsNeededToPass = 3;
    public const int PointsPerCorrectAnswer = 10;
    public const int MinimumPassingScore = 30;
    public const int MaximumScore = QuestionCount * PointsPerCorrectAnswer;

    public static int Day => _day;
    public static bool IsActive => _active;
    public static bool IsFinalized => _finalized;
    public static bool FailurePenaltyApplied => _failurePenaltyApplied;
    public static IReadOnlyList<TaskAttemptResult> Results => Attempts;
    public static IReadOnlyList<string> PendingNotifications => Notifications;
    public static IReadOnlyList<AssessmentHistoryEntry> AssessmentHistory => History;
    public static int PointsReceived => _pointsReceived;
    public static int AttemptCount => Attempts.Count;
    public static string CurrentQuestionId =>
        Attempts.Count < QuestionOrder.Count ? QuestionOrder[Attempts.Count] : string.Empty;
    public static string NextQuestionId =>
        Attempts.Count + 1 < QuestionOrder.Count ? QuestionOrder[Attempts.Count + 1] : string.Empty;
    public static int CorrectCount => Attempts.Count(result => result.Correct);
    public static int AnsweredCount => Attempts.Count(result => !result.GaveUp);
    public static int Score => CorrectCount * PointsPerCorrectAnswer;
    public static bool Passed => Attempts.Count >= QuestionCount && Score >= MinimumPassingScore;

    public static void Begin(int day)
    {
        _day = day;
        Attempts.Clear();
        Notifications.Clear();
        _pointsReceived = 0;
        QuestionOrder.Clear();
        QuestionOrder.AddRange(StandardQuestionIds);

        // Fisher-Yates shuffle: every assessment contains each question type exactly once.
        var rng = new Random();
        for (int i = QuestionOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (QuestionOrder[i], QuestionOrder[j]) = (QuestionOrder[j], QuestionOrder[i]);
        }

        _active = true;
        _finalized = false;
        _failurePenaltyApplied = false;
    }

    public static void BeginIfNeeded(int day)
    {
        if (!_active || _day != day || _finalized)
            Begin(day);
    }

    public static void Record(string challengeId, bool correct, bool gaveUp, int difficulty)
    {
        if (!_active || _finalized || Attempts.Count >= QuestionCount)
            return;

        string expectedId = CurrentQuestionId;
        if (!string.Equals(expectedId, challengeId, StringComparison.OrdinalIgnoreCase))
            return;

        if (Attempts.Any(attempt => string.Equals(attempt.ChallengeId, challengeId, StringComparison.OrdinalIgnoreCase)))
            return;

        Attempts.Add(new TaskAttemptResult(challengeId, correct, gaveUp, difficulty));
    }

    public static void AddPointsReceived(int points)
    {
        if (points > 0)
            _pointsReceived += points;
    }

    public static void RecordHistory(
        int sanityAfter,
        string debuffType,
        int debuffAmount,
        string rewardText)
    {
        int attempt = 1;
        foreach (AssessmentHistoryEntry entry in History)
            if (entry.Day == _day)
                attempt = Math.Max(attempt, entry.Attempt + 1);

        History.Add(new AssessmentHistoryEntry(
            _day,
            attempt,
            Score,
            MaximumScore,
            MinimumPassingScore,
            _pointsReceived,
            Passed,
            sanityAfter,
            debuffType ?? string.Empty,
            Math.Max(0, debuffAmount),
            rewardText ?? string.Empty,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
    }

    public static Godot.Collections.Array CreateHistorySaveData()
    {
        var result = new Godot.Collections.Array();
        foreach (AssessmentHistoryEntry entry in History)
        {
            result.Add(new Godot.Collections.Dictionary
            {
                ["day"] = entry.Day,
                ["attempt"] = entry.Attempt,
                ["score"] = entry.Score,
                ["maximum_score"] = entry.MaximumScore,
                ["passing_score"] = entry.PassingScore,
                ["points_received"] = entry.PointsReceived,
                ["passed"] = entry.Passed,
                ["sanity_after"] = entry.SanityAfter,
                ["debuff_type"] = entry.DebuffType,
                ["debuff_amount"] = entry.DebuffAmount,
                ["reward_text"] = entry.RewardText,
                ["timestamp"] = entry.Timestamp
            });
        }
        return result;
    }

    public static void RestoreHistoryFromSaveData(Godot.Collections.Dictionary data)
    {
        History.Clear();
        if (data == null || !data.ContainsKey("entries") ||
            data["entries"].VariantType != Godot.Variant.Type.Array)
            return;

        foreach (Godot.Variant item in data["entries"].AsGodotArray())
        {
            if (item.VariantType != Godot.Variant.Type.Dictionary)
                continue;

            var value = item.AsGodotDictionary();
            History.Add(new AssessmentHistoryEntry(
                ReadHistoryInt(value, "day", 1),
                ReadHistoryInt(value, "attempt", 1),
                ReadHistoryInt(value, "score", 0),
                ReadHistoryInt(value, "maximum_score", MaximumScore),
                ReadHistoryInt(value, "passing_score", MinimumPassingScore),
                ReadHistoryInt(value, "points_received", 0),
                value.ContainsKey("passed") && value["passed"].VariantType == Godot.Variant.Type.Bool &&
                    value["passed"].AsBool(),
                ReadHistoryInt(value, "sanity_after", 0),
                ReadHistoryString(value, "debuff_type"),
                ReadHistoryInt(value, "debuff_amount", 0),
                ReadHistoryString(value, "reward_text"),
                ReadHistoryString(value, "timestamp")));
        }
    }

    public static void ResetForNewRun()
    {
        Reset();
        History.Clear();
    }

    private static int ReadHistoryInt(Godot.Collections.Dictionary data, string key, int fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        if (value.VariantType == Variant.Type.Int)
            return value.AsInt32();
        return fallback;
    }

    private static string ReadHistoryString(Godot.Collections.Dictionary data, string key)
    {
        if (!data.ContainsKey(key) || data[key].VariantType != Variant.Type.String)
            return string.Empty;
        return data[key].AsString();
    }

    public static Godot.Collections.Dictionary CreateHistorySaveContainer() =>
        new() { ["entries"] = CreateHistorySaveData() };

    public static void QueueNotification(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Notifications.Add(message);
    }

    public static bool FinalizeTest()
    {
        if (_finalized)
            return Passed;

        _active = false;
        _finalized = true;
        return Passed;
    }

    public static void MarkFailurePenaltyApplied() => _failurePenaltyApplied = true;

    public static void Reset()
    {
        Attempts.Clear();
        Notifications.Clear();
        QuestionOrder.Clear();
        _pointsReceived = 0;
        _active = false;
        _finalized = false;
        _failurePenaltyApplied = false;
    }
}

public sealed class TaskAttemptResult
{
    public string ChallengeId { get; }
    public bool Correct { get; }
    public bool GaveUp { get; }
    public int Difficulty { get; }

    public TaskAttemptResult(string challengeId, bool correct, bool gaveUp, int difficulty)
    {
        ChallengeId = challengeId;
        Correct = correct;
        GaveUp = gaveUp;
        Difficulty = difficulty;
    }
}


public sealed class AssessmentHistoryEntry
{
    public int Day { get; }
    public int Attempt { get; }
    public int Score { get; }
    public int MaximumScore { get; }
    public int PassingScore { get; }
    public int PointsReceived { get; }
    public bool Passed { get; }
    public int SanityAfter { get; }
    public string DebuffType { get; }
    public int DebuffAmount { get; }
    public string RewardText { get; }
    public string Timestamp { get; }

    public AssessmentHistoryEntry(
        int day, int attempt, int score, int maximumScore, int passingScore,
        int pointsReceived, bool passed, int sanityAfter, string debuffType,
        int debuffAmount, string rewardText, string timestamp)
    {
        Day = day;
        Attempt = attempt;
        Score = score;
        MaximumScore = maximumScore;
        PassingScore = passingScore;
        PointsReceived = pointsReceived;
        Passed = passed;
        SanityAfter = sanityAfter;
        DebuffType = debuffType ?? string.Empty;
        DebuffAmount = debuffAmount;
        RewardText = rewardText ?? string.Empty;
        Timestamp = timestamp ?? string.Empty;
    }
}
