using System.Collections.Generic;
using System.Linq;

/// <summary>In-memory test results shared by the separate question scenes.</summary>
public static class DailyTestSession
{
    private static readonly List<TaskAttemptResult> Attempts = new();
    private static readonly List<string> Notifications = new();
    private static int _day = 1;
    private static bool _active;
    private static bool _finalized;

    public const int QuestionCount = 4;
    public const int QuestionsNeededToPass = 3;

    public static int Day => _day;
    public static bool IsActive => _active;
    public static bool IsFinalized => _finalized;
    public static IReadOnlyList<TaskAttemptResult> Results => Attempts;
    public static IReadOnlyList<string> PendingNotifications => Notifications;
    public static int CorrectCount => Attempts.Count(result => result.Correct);
    public static int AnsweredCount => Attempts.Count(result => !result.GaveUp);
    public static bool Passed => Attempts.Count >= QuestionCount && CorrectCount >= QuestionsNeededToPass;

    public static void Begin(int day)
    {
        _day = day;
        Attempts.Clear();
        Notifications.Clear();
        _active = true;
        _finalized = false;
    }

    public static void BeginIfNeeded(int day)
    {
        if (!_active || _day != day || _finalized)
            Begin(day);
    }

    public static void Record(string challengeId, bool correct, bool gaveUp, int difficulty)
    {
        if (!_active || _finalized)
            return;

        // Each slot should only contribute once, even if a button is accidentally clicked twice.
        if (Attempts.Any(attempt => attempt.ChallengeId == challengeId))
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

    public static void Reset()
    {
        Attempts.Clear();
        Notifications.Clear();
        _active = false;
        _finalized = false;
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
