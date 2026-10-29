using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using SchoolYardGame;

public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    public const int ManualSlotCount = 3;
    public const string WorldScenePath = "res://Scenes/Home/Rooms/World.tscn";
    private const string DefaultRoomPath = "res://Scenes/Home/Rooms/Kwarto.tscn";
    private const string SaveDirectory = "res://Saves";
    private const string AutoSaveFileName = "autosave.json";
    private const double AutoSaveIntervalSeconds = 60.0;

    private double _autosaveElapsed;
    private bool _writingAutosave;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        EnsureSaveDirectory();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (GetTree().Paused || !CanSaveCurrentGame ||
            DayClock.Instance == null || !DayClock.Instance.Running)
            return;

        _autosaveElapsed += delta;
        if (_autosaveElapsed >= AutoSaveIntervalSeconds)
        {
            _autosaveElapsed = 0;
            SaveAutoSave();
        }
    }

    public bool CanSaveCurrentGame
    {
        get
        {
            if (GetTree()?.CurrentScene == null)
                return false;

            if (GetTree().CurrentScene.SceneFilePath != WorldScenePath)
                return false;

            var roomManager = GetTree().GetFirstNodeInGroup("room_manager") as RoomManager;
            return roomManager != null && !string.IsNullOrWhiteSpace(roomManager.CurrentRoomPath);
        }
    }

    public bool HasSlot(int slot) =>
        IsValidSlot(slot) && FileAccess.FileExists(GetSlotPath(slot));

    public bool HasAutoSave => FileAccess.FileExists(GetAutoSavePath());

    public string GetSlotTimestamp(int slot) => GetTimestamp(GetSlotPath(slot));

    public string GetAutoSaveTimestamp() => GetTimestamp(GetAutoSavePath());

    public string GetSlotDescription(int slot)
    {
        if (!IsValidSlot(slot))
            return "Invalid slot";

        string timestamp = GetSlotTimestamp(slot);
        return string.IsNullOrWhiteSpace(timestamp) ? "Empty slot" : $"Saved: {timestamp}";
    }

    public bool SaveSlot(int slot)
    {
        if (!IsValidSlot(slot))
            return false;

        return WriteSave(GetSlotPath(slot), slot, false);
    }

    public bool DeleteSlot(int slot)
    {
        if (!IsValidSlot(slot))
            return false;

        // Remove every copy so an old save cannot be migrated back into the slot.
        string[] paths =
        {
            $"{SaveDirectory}/save_slot_{slot}.json",
            $"user://Saves/save_slot_{slot}.json",
            $"user://save_slot_{slot}.json"
        };

        foreach (string path in paths)
        {
            if (!FileAccess.FileExists(path))
                continue;

            Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            if (error != Error.Ok)
            {
                GD.PushError($"SaveManager: could not delete '{path}' for slot {slot} ({error}).");
                return false;
            }

        }

        return true;
    }

    public bool SaveAutoSave()
    {
        if (_writingAutosave)
            return false;

        _writingAutosave = true;
        try
        {
            bool saved = WriteSave(GetAutoSavePath(), 0, true);
            if (saved)
                _autosaveElapsed = 0;
            return saved;
        }
        finally
        {
            _writingAutosave = false;
        }
    }

    // Call after entering/loading a room to create an immediate safety autosave.
    public void RequestAutoSave()
    {
        _autosaveElapsed = 0;
        SaveAutoSave();
    }

    public async System.Threading.Tasks.Task<bool> LoadSlotAsync(int slot)
    {
        if (!IsValidSlot(slot))
            return false;

        if (!TryReadSave(GetSlotPath(slot), out var data))
            return false;

        return await LoadSnapshotAsync(data);
    }

    public async System.Threading.Tasks.Task<bool> LoadAutoSaveAsync()
    {
        if (!TryReadSave(GetAutoSavePath(), out var data))
            return false;

        return await LoadSnapshotAsync(data);
    }

    private bool WriteSave(string path, int slot, bool isAutoSave)
    {
        if (!CanSaveCurrentGame)
        {
            GD.PushWarning("SaveManager: saving is only available while exploring the world.");
            return false;
        }

        var roomManager = GetTree().GetFirstNodeInGroup("room_manager") as RoomManager;
        if (roomManager == null || DayClock.Instance == null || SanityManager.Instance == null)
            return false;

        Vector2 position = roomManager.CurrentPlayerPosition;
        var data = new Godot.Collections.Dictionary
        {
            ["version"] = 1,
            ["slot"] = slot,
            ["kind"] = isAutoSave ? "autosave" : "manual",
            ["saved_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["scene_path"] = WorldScenePath,
            ["room_path"] = roomManager.CurrentRoomPath,
            ["player_x"] = position.X,
            ["player_y"] = position.Y,
            ["day"] = DayClock.Instance.DayNumber,
            ["hour"] = DayClock.Instance.Hour,
            ["clock_running"] = true,
            ["school_started"] = DayClock.Instance.SchoolChallengeStartedToday,
            ["school_completed"] = DayClock.Instance.SchoolChallengeCompletedToday,
            ["mystery_encounter_seen"] = DayClock.Instance.FirstDayMysteryEncounterSeen,
            ["first_day_special_greeting_pending"] = DayClock.Instance.FirstDayMysteryGreetingPending,
            ["sanity_state"] = SanityManager.Instance.CreateSaveData(),
            ["room_keys"] = new Godot.Collections.Dictionary
            {
                ["hidden_points"] = RoomKeyProgression.Instance?.HiddenPoints ?? 0,
                ["keys_earned"] = RoomKeyProgression.Instance?.TotalKeysEarned ?? 0,
                ["pending_key_room"] = RoomKeyProgression.Instance?.PendingKeyRoomName ?? string.Empty
            },
            ["challenge_progress"] = ChallengeProgress.CreateSaveData()
        };

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"SaveManager: could not open '{path}' for writing ({FileAccess.GetOpenError()}).");
            return false;
        }

        file.StoreString(Json.Stringify(data, "\t"));
        file.Flush(); // DO NOT REMOVE. It handles unexpected crashed during save and ensures that the file is written to disk right away. -bvkopie.
        return true;
    }

    private async System.Threading.Tasks.Task<bool> LoadSnapshotAsync(Godot.Collections.Dictionary data)
    {
        int saveVersion = ReadSaveVersion(data);
        if (saveVersion != 1)
        {
            GD.PushError(
                $"SaveManager: unsupported save format version '{(data.ContainsKey("version") ? data["version"].ToString() : "missing")}'. " +
                "This game supports version 1 saves. Keep a backup of the JSON before changing it.");
            return false;
        }

        string roomPath = ReadString(data, "room_path", "");
        bool savedRoomExists = !string.IsNullOrWhiteSpace(roomPath) && ResourceLoader.Exists(roomPath);
        if (!savedRoomExists)
        {
            if (!ResourceLoader.Exists(DefaultRoomPath))
            {
                GD.PushError($"SaveManager: saved room '{roomPath}' is missing, and fallback room '{DefaultRoomPath}' is unavailable.");
                return false;
            }

            GD.PushWarning($"SaveManager: saved room '{roomPath}' is missing. Loading the save into '{DefaultRoomPath}' instead.");
            roomPath = DefaultRoomPath;
        }

        float x = ReadFloat(data, "player_x", 0f);
        float y = ReadFloat(data, "player_y", 0f);
        int day = Mathf.Max(1, ReadInt(data, "day", 1));
        float hour = ReadFloat(data, "hour", 8f);
        bool schoolStarted = ReadBool(data, "school_started", false);
        bool schoolCompleted = ReadBool(data, "school_completed", false);

        var sanityState = ReadDictionary(data, "sanity_state");
        var keyState = ReadDictionary(data, "room_keys");
        var challengeState = ReadDictionary(data, "challenge_progress");

        DayClock.Instance?.RestoreState(day, hour, true, schoolStarted, schoolCompleted);
        DayClock.Instance?.RestoreFirstDayMysteryState(
            ReadBool(data, "mystery_encounter_seen", false),
            ReadBool(data, "first_day_special_greeting_pending", false));
        if (SanityManager.Instance != null && sanityState.Count > 0)
            SanityManager.Instance.RestoreFromSaveData(sanityState);
        int keysToRestore = Mathf.Max(0, ReadInt(keyState, "keys_earned", 0));
        string pendingKeyRoom = ReadString(keyState, "pending_key_room", string.Empty);
        if (data.ContainsKey("challenge_progress"))
        {
            int passedAssessments = CountPassedAssessmentDays(challengeState);
            int validKeys = Math.Min(keysToRestore, passedAssessments);
            if (validKeys < keysToRestore)
                GD.PushWarning($"SaveManager: ignored {keysToRestore - validKeys} room key(s) without a matching passed assessment.");
            keysToRestore = validKeys;

            // An uncollected key also needs a completed passing assessment in the save history.
            if (passedAssessments <= keysToRestore)
                pendingKeyRoom = string.Empty;
        }

        RoomKeyProgression.Instance?.RestoreProgress(
            ReadInt(keyState, "hidden_points", 0),
            keysToRestore,
            pendingKeyRoom);
        ChallengeProgress.RestoreFromSaveData(challengeState);

        RoomManager.RequestStartRoomOnNextLoad(roomPath, "startSpawn");
        if (savedRoomExists)
            RoomManager.RequestPlayerPositionOnNextLoad(new Vector2(x, y));

        PauseMenuController.Instance?.PrepareForExternalLoad();
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        if (SceneTransition.Instance != null)
        {
            bool loaded = await SceneTransition.Instance.LoadSceneWithLoadingScreen(WorldScenePath);
            if (!loaded)
            {
                if (DayClock.Instance != null)
                    DayClock.Instance.Running = GetTree().CurrentScene?.SceneFilePath == WorldScenePath;
                return false;
            }
        }
        else
        {
            Error result = GetTree().ChangeSceneToFile(WorldScenePath);
            if (result != Error.Ok)
            {
                GD.PushError($"SaveManager: failed to return to the saved world ({result}).");
                return false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        _autosaveElapsed = 0;
        return true;
    }

    private bool TryReadSave(string path, out Godot.Collections.Dictionary data)
    {
        data = new Godot.Collections.Dictionary();
        if (!FileAccess.FileExists(path))
            return false;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"SaveManager: could not open '{path}' ({FileAccess.GetOpenError()}).");
            return false;
        }

        Variant parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError($"SaveManager: '{path}' is not a valid save file.");
            return false;
        }

        data = parsed.AsGodotDictionary();
        return true;
    }

    private string GetTimestamp(string path)
    {
        if (!TryReadSave(path, out var data))
            return string.Empty;

        return ReadString(data, "saved_at", string.Empty);
    }

    private static void EnsureSaveDirectory()
    {
        Error error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectory));
        if (error != Error.Ok && error != Error.AlreadyExists)
            GD.PushError($"SaveManager: could not create save directory '{SaveDirectory}' ({error}).");
    }

    private string GetAutoSavePath()
    {
        EnsureSaveDirectory();
        string path = $"{SaveDirectory}/{AutoSaveFileName}";
        MigrateLegacySave(path, "user://Saves/autosave.json", "user://autosave.json");
        return path;
    }

    private string GetSlotPath(int slot)
    {
        EnsureSaveDirectory();
        string path = $"{SaveDirectory}/save_slot_{slot}.json";
        MigrateLegacySave(path, $"user://Saves/save_slot_{slot}.json", $"user://save_slot_{slot}.json");
        return path;
    }

    private static void MigrateLegacySave(string destinationPath, params string[] legacyPaths)
    {
        if (FileAccess.FileExists(destinationPath))
            return;

        foreach (string legacyPath in legacyPaths)
        {
            if (!FileAccess.FileExists(legacyPath))
                continue;

            using var source = FileAccess.Open(legacyPath, FileAccess.ModeFlags.Read);
            if (source == null)
                continue;

            using var destination = FileAccess.Open(destinationPath, FileAccess.ModeFlags.Write);
            if (destination == null)
            {
                GD.PushError($"SaveManager: could not migrate '{legacyPath}' to '{destinationPath}'.");
                return;
            }

            destination.StoreString(source.GetAsText());
            destination.Flush();
            GD.PushWarning($"SaveManager: migrated legacy save '{legacyPath}' to '{destinationPath}'.");
            return;
        }
    }

    private static int CountPassedAssessmentDays(Godot.Collections.Dictionary progress)
    {
        var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ChallengeProgress.SequenceId,
            ChallengeProgress.ConditionId,
            ChallengeProgress.FlowchartId,
            ChallengeProgress.FinalId
        };

        int passedDays = 0;
        foreach (Variant dayKey in progress.Keys)
        {
            if (!int.TryParse(dayKey.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) ||
                day < 1 || progress[dayKey].VariantType != Variant.Type.Array)
                continue;

            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Variant item in progress[dayKey].AsGodotArray())
            {
                if (item.VariantType != Variant.Type.String)
                    continue;

                string id = item.AsString();
                if (validIds.Contains(id))
                    completed.Add(id);
            }

            if (completed.Count >= 3)
                passedDays++;
        }

        return passedDays;
    }

    private static bool IsValidSlot(int slot) => slot >= 1 && slot <= ManualSlotCount;

    private static Godot.Collections.Dictionary ReadDictionary(
        Godot.Collections.Dictionary data, string key)
    {
        if (!data.ContainsKey(key))
            return new Godot.Collections.Dictionary();

        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    private static int ReadSaveVersion(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("version"))
            return 1; // Legacy save files predate the explicit version field.

        Variant value = data["version"];
        if (value.VariantType == Variant.Type.Int)
            return value.AsInt32();

        if (value.VariantType == Variant.Type.Float)
        {
            double numericVersion = value.AsDouble();
            if (numericVersion >= int.MinValue && numericVersion <= int.MaxValue &&
                Math.Abs(numericVersion - Math.Round(numericVersion)) < 0.000001)
                return (int)Math.Round(numericVersion);
            return 0;
        }

        if (value.VariantType == Variant.Type.String &&
            double.TryParse(value.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedVersion) &&
            parsedVersion >= int.MinValue && parsedVersion <= int.MaxValue &&
            Math.Abs(parsedVersion - Math.Round(parsedVersion)) < 0.000001)
            return (int)Math.Round(parsedVersion);

        return 0;
    }

    private static int ReadInt(Godot.Collections.Dictionary data, string key, int fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        if (value.VariantType == Variant.Type.Int)
            return value.AsInt32();

        if (value.VariantType == Variant.Type.Float)
        {
            double number = value.AsDouble();
            if (number >= int.MinValue && number <= int.MaxValue &&
                Math.Abs(number - Math.Round(number)) < 0.000001)
                return (int)Math.Round(number);
            return fallback;
        }

        if (value.VariantType == Variant.Type.String &&
            double.TryParse(value.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
            parsed >= int.MinValue && parsed <= int.MaxValue &&
            Math.Abs(parsed - Math.Round(parsed)) < 0.000001)
            return (int)Math.Round(parsed);

        return fallback;
    }

    private static float ReadFloat(Godot.Collections.Dictionary data, string key, float fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        if (value.VariantType == Variant.Type.Float)
            return (float)value.AsDouble();
        if (value.VariantType == Variant.Type.Int)
            return value.AsInt32();
        if (value.VariantType == Variant.Type.String &&
            float.TryParse(value.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            return parsed;

        return fallback;
    }

    private static bool ReadBool(Godot.Collections.Dictionary data, string key, bool fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static string ReadString(Godot.Collections.Dictionary data, string key, string fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }
}
