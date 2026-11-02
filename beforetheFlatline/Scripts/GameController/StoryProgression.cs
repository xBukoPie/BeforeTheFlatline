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

        return new Godot.Collections.Dictionary
        {
            ["completed_memories"] = memories
        };
    }

    public void RestoreFromSaveData(Godot.Collections.Dictionary data)
    {
        _completedMemories.Clear();

        if (data == null || !data.ContainsKey("completed_memories") ||
            data["completed_memories"].VariantType != Variant.Type.Array)
            return;

        foreach (Variant item in data["completed_memories"].AsGodotArray())
        {
            string value = item.AsString().Trim();
            if (FindMemoryIndex(value) >= 0)
                _completedMemories.Add(value);
        }
    }

    public void ResetForNewRun()
    {
        _completedMemories.Clear();
    }

    private static int FindMemoryIndex(string roomId)
    {
        string value = (roomId ?? string.Empty).Trim();
        return Array.FindIndex(MemoryOrder, id =>
            string.Equals(id, value, StringComparison.OrdinalIgnoreCase));
    }
}
