using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

public partial class RoomKeyProgression : Node
{
    [Signal]
    public delegate void RoomKeyEarnedEventHandler(int keysEarned, string roomName);

    public static RoomKeyProgression Instance { get; private set; }

    public const int PointsPerKeyQuota = 100;
    public int HiddenPoints { get; private set; }
    public int TotalKeysEarned => _unlockedRoomIds.Count;
    public string PendingKeyRoomName { get; private set; } = string.Empty;
    public bool HasPendingKey => !string.IsNullOrWhiteSpace(PendingKeyRoomName);
    public int PointsUntilNextKey => string.IsNullOrEmpty(NextRoomKeyName)
        ? 0
        : Math.Max(0, (TotalKeysEarned + 1) * PointsPerKeyQuota - HiddenPoints);

    public string NextRoomKeyName
    {
        get
        {
            int nextIndex = FindNextLockedRoomIndex();
            return nextIndex < 0 ? string.Empty : LockedRooms[nextIndex].Name;
        }
    }

    private static readonly (string Id, string Name)[] LockedRooms =
    {
        ("Kitchen", "Kitchen"),
        ("Laundry", "Laundry"),
        ("Bathroom", "Bathroom"),
        ("MasterBedroom", "MasterBedroom")
    };

    private readonly HashSet<string> _unlockedRoomIds = new(StringComparer.OrdinalIgnoreCase);

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

    public void RestoreProgress(
        int hiddenPoints,
        int totalKeysEarned,
        string pendingKeyRoomName = "",
        IEnumerable<string> unlockedRoomIds = null)
    {
        HiddenPoints = Math.Max(0, hiddenPoints);
        _unlockedRoomIds.Clear();

        if (unlockedRoomIds == null)
        {
            // Legacy saves represented keys as a sequential count.
            int count = Math.Clamp(totalKeysEarned, 0, LockedRooms.Length);
            for (int i = 0; i < count; i++)
                _unlockedRoomIds.Add(LockedRooms[i].Id);
        }
        else
        {
            foreach (string value in unlockedRoomIds)
            {
                int index = FindRoomIndex(value);
                if (index >= 0)
                    _unlockedRoomIds.Add(LockedRooms[index].Id);
            }
        }

        int pendingIndex = FindRoomIndex(pendingKeyRoomName);
        PendingKeyRoomName = pendingIndex >= 0 && !_unlockedRoomIds.Contains(LockedRooms[pendingIndex].Id)
            ? LockedRooms[pendingIndex].Name
            : string.Empty;
    }

    public string[] GetUnlockedRoomNames()
    {
        var names = new List<string>();
        foreach (var room in LockedRooms)
            if (_unlockedRoomIds.Contains(room.Id))
                names.Add(room.Name);
        return names.ToArray();
    }

    public string[] GetUnlockedRoomIds()
    {
        var ids = new List<string>();
        foreach (var room in LockedRooms)
            if (_unlockedRoomIds.Contains(room.Id))
                ids.Add(room.Id);
        return ids.ToArray();
    }

    public bool IsRoomUnlocked(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return true;

        int index = FindRoomIndex(roomId);
        return index >= 0 && _unlockedRoomIds.Contains(LockedRooms[index].Id);
    }

    public string QueueAssessmentKey()
    {
        if (HasPendingKey)
        {
            int pendingIndex = FindRoomIndex(PendingKeyRoomName);
            if (pendingIndex >= 0 && !_unlockedRoomIds.Contains(LockedRooms[pendingIndex].Id))
                return PendingKeyRoomName;
            PendingKeyRoomName = string.Empty;
        }

        int nextIndex = FindNextLockedRoomIndex();
        if (nextIndex < 0)
            return string.Empty;

        PendingKeyRoomName = LockedRooms[nextIndex].Name;
        return PendingKeyRoomName;
    }

    public string CollectPendingKey()
    {
        int index = FindRoomIndex(PendingKeyRoomName);
        if (index < 0 || _unlockedRoomIds.Contains(LockedRooms[index].Id))
        {
            PendingKeyRoomName = string.Empty;
            return string.Empty;
        }

        string roomName = LockedRooms[index].Name;
        _unlockedRoomIds.Add(LockedRooms[index].Id);
        PendingKeyRoomName = string.Empty;
        EmitSignal(SignalName.RoomKeyEarned, 1, roomName);
        return roomName;
    }

    public string AdminGrantKeyForRoom(string roomIdOrName)
    {
        int index = FindRoomIndex(roomIdOrName);
        if (index < 0 || !_unlockedRoomIds.Add(LockedRooms[index].Id))
            return string.Empty;

        string roomName = LockedRooms[index].Name;
        if (string.Equals(PendingKeyRoomName, roomName, StringComparison.OrdinalIgnoreCase))
            PendingKeyRoomName = string.Empty;

        EmitSignal(SignalName.RoomKeyEarned, 1, roomName);
        return roomName;
    }

    public string AwardAssessmentCompletion() => QueueAssessmentKey();

    public TaskRewardResult AwardTaskCompletion(int difficulty)
    {
        int tier = Mathf.Clamp(difficulty, 1, 5);
        int earnedPoints = 20 + tier * 5;
        HiddenPoints += earnedPoints;
        return new TaskRewardResult(earnedPoints, Array.Empty<string>(), HiddenPoints,
            TotalKeysEarned, PointsUntilNextKey, NextRoomKeyName);
    }

    public void ResetForNewRun()
    {
        HiddenPoints = 0;
        _unlockedRoomIds.Clear();
        PendingKeyRoomName = string.Empty;
    }

    private int FindNextLockedRoomIndex() =>
        Array.FindIndex(LockedRooms, room => !_unlockedRoomIds.Contains(room.Id));

    private static int FindRoomIndex(string idOrName)
    {
        string value = (idOrName ?? string.Empty).Trim();
        return Array.FindIndex(LockedRooms, room =>
            string.Equals(room.Id, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(room.Name, value, StringComparison.OrdinalIgnoreCase));
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

    public TaskRewardResult(int pointsAdded, string[] roomsUnlocked, int totalPoints, int totalKeys,
        int pointsUntilNextKey, string nextRoomKeyName)
    {
        PointsAdded = pointsAdded;
        RoomsUnlocked = roomsUnlocked ?? Array.Empty<string>();
        TotalPoints = totalPoints;
        TotalKeys = totalKeys;
        PointsUntilNextKey = pointsUntilNextKey;
        NextRoomKeyName = nextRoomKeyName ?? string.Empty;
    }
}
