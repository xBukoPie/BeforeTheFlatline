using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using SchoolYardGame;

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
    private static bool _passedForDay;
    private static bool _outcomeResolved;
    private static int _bestScore;
    private static int _bestPointsReceived;

    public const int QuestionCount = 4;
    public const int QuestionsNeededToPass = 3;
    public const int PointsPerCorrectAnswer = 10;
    public const int MinimumPassingScore = 30;
    public const int MaximumScore = QuestionCount * PointsPerCorrectAnswer;

    public static int Day => _day;
    public static bool IsActive => _active;
    public static bool IsFinalized => _finalized;
    public static bool FailurePenaltyApplied => _failurePenaltyApplied;
    public static bool PassedForDay => _passedForDay;
    public static bool HasPendingHomeResolution => _finalized && !_outcomeResolved;
    public static IReadOnlyList<TaskAttemptResult> Results => Attempts;
    public static IReadOnlyList<string> PendingNotifications => Notifications;
    public static IReadOnlyList<AssessmentHistoryEntry> AssessmentHistory => History;
    public static int PointsReceived => _pointsReceived;
    public static int BestScore => Math.Max(_bestScore, Score);
    public static int BestPointsReceived => Math.Max(_bestPointsReceived, _pointsReceived);
    public static int AttemptCount => Attempts.Count;
    public static string CurrentQuestionId =>
        Attempts.Count < QuestionOrder.Count ? QuestionOrder[Attempts.Count] : string.Empty;
    public static string NextQuestionId =>
        Attempts.Count + 1 < QuestionOrder.Count ? QuestionOrder[Attempts.Count + 1] : string.Empty;
    public static int CorrectCount => Attempts.Count(result => result.Correct);
    public static int AnsweredCount => Attempts.Count(result => !result.GaveUp);
    public static int Score => CorrectCount * PointsPerCorrectAnswer;
    public static bool CurrentAttemptPassed =>
        Attempts.Count >= QuestionCount && Score >= MinimumPassingScore;

    // A passing attempt is secured for the day and cannot be downgraded by a later retry.
    public static bool Passed => _passedForDay || CurrentAttemptPassed;

    public static void Begin(int day)
    {
        _day = day;
        Attempts.Clear();
        Notifications.Clear();
        _pointsReceived = 0;
        _bestScore = 0;
        _bestPointsReceived = 0;
        _passedForDay = false;
        _outcomeResolved = false;
        _failurePenaltyApplied = false;
        ShuffleQuestionOrder();

        _active = true;
        _finalized = false;
    }

    private static void ShuffleQuestionOrder()
    {
        QuestionOrder.Clear();
        QuestionOrder.AddRange(StandardQuestionIds);

        // Fisher-Yates shuffle: every assessment contains each question type exactly once.
        var rng = new Random();
        for (int i = QuestionOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (QuestionOrder[i], QuestionOrder[j]) = (QuestionOrder[j], QuestionOrder[i]);
        }
    }

    public static bool RetryCurrentAssessment()
    {
        if (!_finalized || _outcomeResolved)
            return false;

        Attempts.Clear();
        Notifications.Clear();
        _pointsReceived = 0;
        ShuffleQuestionOrder();
        _active = true;
        _finalized = false;
        return true;
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
            BestScore,
            MaximumScore,
            MinimumPassingScore,
            BestPointsReceived,
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

        if (value.VariantType == Variant.Type.Float)
        {
            double number = value.AsDouble();
            if (double.IsFinite(number) && number >= int.MinValue && number <= int.MaxValue &&
                Math.Abs(number - Math.Round(number)) < 0.000001)
                return (int)Math.Round(number);
        }

        if (value.VariantType == Variant.Type.String &&
            double.TryParse(value.AsString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsedString) &&
            double.IsFinite(parsedString) &&
            parsedString >= int.MinValue && parsedString <= int.MaxValue &&
            Math.Abs(parsedString - Math.Round(parsedString)) < 0.000001)
            return (int)Math.Round(parsedString);

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

        _bestScore = Math.Max(_bestScore, Score);
        _bestPointsReceived = Math.Max(_bestPointsReceived, _pointsReceived);
        _passedForDay |= CurrentAttemptPassed;
        _active = false;
        _finalized = true;
        return Passed;
    }

    // The final assessment consequence is resolved only after RoomManager has loaded a home room.
    public static bool ResolveAtHome()
    {
        if (!HasPendingHomeResolution)
            return false;

        _outcomeResolved = true;
        int before = SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity;
        int sanityChange = 0;
        string debuffType = string.Empty;
        int debuffAmount = 0;

        if (_passedForDay)
        {
            SanityManager.Instance?.Recover(5);
            int afterRecovery = SanityManager.Instance?.CurrentSanity ?? before;
            sanityChange = afterRecovery - before;
            StatusEffectsOverlay.Instance?.TriggerTaskPass();
        }
        else if (!_failurePenaltyApplied)
        {
            debuffAmount = Mathf.Clamp(34 + (_day - 1) * 3, 34, 46);
            int failedCount = SanityManager.Instance?.FailedTestCount ?? 0;
            SanityManager.DebuffType type = (SanityManager.DebuffType)(failedCount % 4);
            debuffType = type.ToString();

            SanityManager.Instance?.RegisterFailedTest();
            SanityManager.Instance?.ApplyDebuff("failed_assessment", type, debuffAmount);
            _failurePenaltyApplied = true;
            StatusEffectsOverlay.Instance?.TriggerTaskFailure(debuffAmount, debuffType);
            sanityChange = -debuffAmount;
        }

        int sanityAfter = SanityManager.Instance?.CurrentSanity ?? before;
        string rewardText = sanityChange > 0
            ? $"Sanity +{sanityChange}"
            : sanityChange < 0
                ? $"Sanity {sanityChange}; {debuffType} assessment penalty ({debuffAmount})"
                : "No additional sanity change";

        if (RoomKeyProgression.Instance?.HasPendingKey == true)
        {
            string keyName = RoomKeyProgression.Instance.PendingKeyRoomName;
            string keyLocation = RoomKeyProgression.Instance.GetPendingKeyLocationName();
            rewardText += $"; Key available: {keyName} (pickup in {keyLocation})";
        }

        RecordHistory(sanityAfter, debuffType, debuffAmount, rewardText);
        SaveManager.Instance?.RequestAutoSave();
        GD.Print($"[DailyTestSession] Home assessment resolution: passed={_passedForDay}, sanity {before}->{sanityAfter}.");
        return true;
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
        _passedForDay = false;
        _outcomeResolved = false;
        _bestScore = 0;
        _bestPointsReceived = 0;
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
