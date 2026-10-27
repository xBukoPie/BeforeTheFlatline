using Godot;
using System;

namespace SchoolYardGame;

public partial class RoomKeyProgression : Node
{
    [Signal]
    public delegate void RoomKeyEarnedEventHandler(int keysEarned, string roomName);

    public static RoomKeyProgression Instance { get; private set; }

    public const int PointsPerKeyQuota = 100;
    public int HiddenPoints { get; private set; }
    public int TotalKeysEarned { get; private set; }
    public int PointsUntilNextKey => TotalKeysEarned >= LockedRooms.Length
        ? 0
        : Math.Max(0, (TotalKeysEarned + 1) * PointsPerKeyQuota - HiddenPoints);

    public string NextRoomKeyName => TotalKeysEarned >= LockedRooms.Length
        ? string.Empty
        : LockedRooms[TotalKeysEarned].Name;

    private static readonly (string Id, string Name)[] LockedRooms =
    {
        ("banyo", "Banyo"),
        ("kusina", "Kusina"),
        ("pasilyo", "Pasilyo"),
        ("dirty_kitchen", "Dirty Kitchen")
    };

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    // Restore the hidden point total and unlocked-key count from a save file.
    public void RestoreProgress(int hiddenPoints, int totalKeysEarned)
    {
        HiddenPoints = Math.Max(0, hiddenPoints);
        TotalKeysEarned = Math.Clamp(totalKeysEarned, 0, LockedRooms.Length);
    }

    public string[] GetUnlockedRoomNames()
    {
        int count = Math.Clamp(TotalKeysEarned, 0, LockedRooms.Length);
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = LockedRooms[i].Name;
        return names;
    }

    public bool IsRoomUnlocked(string roomId)
    {
        string id = (roomId ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(id))
            return true;

        int index = Array.FindIndex(LockedRooms, room => room.Id == id);
        return index >= 0 && TotalKeysEarned > index;
    }

    /// <summary>Grant the next room key when a full assessment is passed. Day and score above the pass threshold do not affect the reward.</summary>
    public string AwardAssessmentCompletion()
    {
        if (TotalKeysEarned >= LockedRooms.Length)
            return string.Empty;

        string roomName = LockedRooms[TotalKeysEarned].Name;
        TotalKeysEarned++;
        EmitSignal(SignalName.RoomKeyEarned, 1, roomName);
        return roomName;
    }

    /// <summary>Grant hidden points for a correctly completed task and unlock any reached quotas.</summary>
    public TaskRewardResult AwardTaskCompletion(int difficulty)
    {
        int tier = Mathf.Clamp(difficulty, 1, 5);
        int earnedPoints = 20 + tier * 5;
        HiddenPoints += earnedPoints;

        return new TaskRewardResult(
            earnedPoints,
            Array.Empty<string>(),
            HiddenPoints,
            TotalKeysEarned,
            PointsUntilNextKey,
            NextRoomKeyName);
    }

    // Start a new run without carrying over room keys or hidden points.
    public void ResetForNewRun()
    {
        HiddenPoints = 0;
        TotalKeysEarned = 0;
    }
}

public sealed class TaskRewardResult
{
    public int PointsAdded { get; }
    public string[] RoomsUnlocked { get; }
    public int KeysEarned => RoomsUnlocked.Length;
    public int TotalPoints { get; }
    public int TotalKeys { get; }
    public int PointsUntilNextKey { get; }
    public string NextRoomKeyName { get; }

    public TaskRewardResult(
        int pointsAdded,
        string[] roomsUnlocked,
        int totalPoints,
        int totalKeys,
        int pointsUntilNextKey,
        string nextRoomKeyName)
    {
        PointsAdded = pointsAdded;
        RoomsUnlocked = roomsUnlocked ?? Array.Empty<string>();
        TotalPoints = totalPoints;
        TotalKeys = totalKeys;
        PointsUntilNextKey = pointsUntilNextKey;
        NextRoomKeyName = nextRoomKeyName ?? string.Empty;
    }
}
