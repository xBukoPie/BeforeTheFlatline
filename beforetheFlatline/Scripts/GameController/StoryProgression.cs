using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

public partial class StoryProgression : Node
{
    public static StoryProgression Instance { get; private set; }

    private static readonly string[] MemoryOrder =
    {
        "Kitchen",
        "Laundry",
        "Bathroom",
        "MasterBedroom"
    };

    private readonly HashSet<string> _completedMemories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _guardianStagesSeen = new();

    public int CompletedMemoryCount => _completedMemories.Count;
    public bool HasCompletedAllMemories => CompletedMemoryCount >= MemoryOrder.Length;

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

    public bool HasSeenMemory(string roomId) =>
        !string.IsNullOrWhiteSpace(roomId) && _completedMemories.Contains(roomId.Trim());

    public bool CanTriggerMemory(string roomId)
    {
        int index = FindMemoryIndex(roomId);
        if (index < 0 || HasSeenMemory(roomId))
            return false;

        if (RoomKeyProgression.Instance == null ||
            !RoomKeyProgression.Instance.IsRoomUnlocked(MemoryOrder[index]))
            return false;

        return index == 0 || HasSeenMemory(MemoryOrder[index - 1]);
    }

    public bool CanOfferKeyForRoomIndex(int roomIndex)
    {
        if (roomIndex <= 0)
            return true;

        if (roomIndex >= MemoryOrder.Length)
            return false;

        return HasSeenMemory(MemoryOrder[roomIndex - 1]);
    }

    // Guardian stages unlock the day after the corresponding room key is collected.
    public bool CanShowGuardianStage(int stage)
    {
        if (stage < 1 || stage > 3 || HasSeenGuardianStage(stage))
            return false;

        // The draft ends Guardian conversations once the Master Bedroom is unlocked.
        if (RoomKeyProgression.Instance?.IsRoomUnlocked("MasterBedroom") == true)
            return false;

        if (stage > 1 && !HasSeenGuardianStage(stage - 1))
            return false;

        string roomId = stage switch
        {
            1 => "Kitchen",
            2 => "Laundry",
            3 => "Bathroom",
            _ => string.Empty
        };

        int unlockedDay = RoomKeyProgression.Instance?.GetRoomUnlockedDay(roomId) ?? -1;
        int currentDay = DayClock.Instance?.DayNumber ?? 1;
        if (RoomKeyProgression.Instance == null || !RoomKeyProgression.Instance.IsRoomUnlocked(roomId))
            return false;

        // Legacy saves without an unlock date make the conversation available from day two onward.
        return unlockedDay >= 1 ? currentDay > unlockedDay : currentDay >= 2;
    }

    public bool HasSeenGuardianStage(int stage) => _guardianStagesSeen.Contains(stage);

    public bool MarkGuardianStageSeen(int stage)
    {
        if (!CanShowGuardianStage(stage))
            return false;

        _guardianStagesSeen.Add(stage);
        GD.Print($"[StoryProgression] Guardian conversation {stage}/3 completed.");
        return true;
    }

    public bool MarkMemorySeen(string roomId)
    {
        if (!CanTriggerMemory(roomId))
            return false;

        _completedMemories.Add(roomId.Trim());
        GD.Print($"[StoryProgression] Memory completed: {roomId}. Progress: {CompletedMemoryCount}/{MemoryOrder.Length}.");
        return true;
    }

    public string GetNextMemoryRoomId()
    {
        foreach (string roomId in MemoryOrder)
            if (!HasSeenMemory(roomId))
                return roomId;

        return string.Empty;
    }

    public Godot.Collections.Dictionary CreateSaveData()
    {
        var memories = new Godot.Collections.Array();
        foreach (string roomId in MemoryOrder)
            if (HasSeenMemory(roomId))
                memories.Add(roomId);

        var guardianStages = new Godot.Collections.Array();
        foreach (int stage in new[] { 1, 2, 3 })
            if (HasSeenGuardianStage(stage))
                guardianStages.Add(stage);

        return new Godot.Collections.Dictionary
        {
            ["completed_memories"] = memories,
            ["guardian_stages_seen"] = guardianStages
        };
    }

    public void RestoreFromSaveData(Godot.Collections.Dictionary data)
    {
        _completedMemories.Clear();
        _guardianStagesSeen.Clear();

        if (data == null || !data.ContainsKey("completed_memories") ||
            data["completed_memories"].VariantType != Variant.Type.Array)
            return;

        foreach (Variant item in data["completed_memories"].AsGodotArray())
        {
            string value = item.AsString().Trim();
            if (FindMemoryIndex(value) >= 0)
                _completedMemories.Add(value);
        }

        if (data.ContainsKey("guardian_stages_seen") &&
            data["guardian_stages_seen"].VariantType == Variant.Type.Array)
        {
            foreach (Variant item in data["guardian_stages_seen"].AsGodotArray())
            {
                int stage = item.VariantType == Variant.Type.Int ? item.AsInt32() : -1;
                if (stage >= 1 && stage <= 3)
                    _guardianStagesSeen.Add(stage);
            }
        }
    }

    public void ResetForNewRun()
    {
        _completedMemories.Clear();
        _guardianStagesSeen.Clear();
    }

    private static int FindMemoryIndex(string roomId)
    {
        string value = (roomId ?? string.Empty).Trim();
        return Array.FindIndex(MemoryOrder, id =>
            string.Equals(id, value, StringComparison.OrdinalIgnoreCase));
    }
}
