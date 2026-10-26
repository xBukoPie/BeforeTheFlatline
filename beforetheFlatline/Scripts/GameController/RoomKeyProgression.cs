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

    public bool IsRoomUnlocked(string roomId)
    {
        string id = (roomId ?? "").Trim().ToLowerInvariant();
        int index = Array.FindIndex(LockedRooms, room => room.Id == id);

        // Rooms not listed here (Kwarto, Sala, Lot, School, etc.) are open by default.
        return index < 0 || TotalKeysEarned > index;
    }

    /// <summary>Grant hidden points for a correctly completed task and unlock any reached quotas.</summary>
    public TaskRewardResult AwardTaskCompletion(int difficulty)
    {
        int tier = Mathf.Clamp(difficulty, 1, 5);
        int earnedPoints = 20 + tier * 5;
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

        return new TaskRewardResult(earnedPoints, newlyUnlocked.ToArray());
    }
}

/// <summary>Result used by the challenge UI. PointsAdded is backend data and must not be displayed.</summary>
public sealed class TaskRewardResult
{
    public int PointsAdded { get; }
    public string[] RoomsUnlocked { get; }
    public int KeysEarned => RoomsUnlocked.Length;

    public TaskRewardResult(int pointsAdded, string[] roomsUnlocked)
    {
        PointsAdded = pointsAdded;
        RoomsUnlocked = roomsUnlocked ?? Array.Empty<string>();
    }
}
