using System;
using System.Collections.Generic;
using System.Linq;

public static class DailyTestSession
{
    private static readonly List<TaskAttemptResult> Attempts = new();
    private static readonly List<string> Notifications = new();
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
