using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Backend-only point balance and sequential room-key progression.
/// The point balance is deliberately never rendered in the HUD or challenge UI.
/// </summary>
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
        // Fail closed for a misspelled lock ID; unlocked rooms simply leave UnlockRoomId empty.
        return index >= 0 && TotalKeysEarned > index;
    }

    /// <summary>Grant hidden points for a correctly completed task and unlock any reached quotas.</summary>
    public TaskRewardResult AwardTaskCompletion(int difficulty)
    {
        int tier = Mathf.Clamp(difficulty, 1, 5);
        int earnedPoints = 20 + tier * 5;

        // Keep progression owned by this autoload. Resetting DailyTestSession for a retry
        // must never reset the accumulated points or keys already earned.
        HiddenPoints += earnedPoints;

        var newlyUnlocked = new List<string>();
        while (TotalKeysEarned < LockedRooms.Length &&
               HiddenPoints >= (TotalKeysEarned + 1) * PointsPerKeyQuota)
        {
            string roomName = LockedRooms[TotalKeysEarned].Name;
            TotalKeysEarned++;
            newlyUnlocked.Add(roomName);
            EmitSignal(SignalName.RoomKeyEarned, 1, roomName);
        }

        return new TaskRewardResult(
            earnedPoints,
            newlyUnlocked.ToArray(),
            HiddenPoints,
            TotalKeysEarned,
            PointsUntilNextKey,
            NextRoomKeyName);
    }
}

/// <summary>Result used by the challenge UI. PointsAdded is backend data and must not be displayed.</summary>
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
