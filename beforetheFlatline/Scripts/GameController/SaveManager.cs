using Godot;
using System;
using System.Globalization;
using SchoolYardGame;

/// <summary>
/// Persistent save system: three manual slots plus a separate rolling autosave.
/// Saves are JSON files under user:// so they survive application restarts and exported builds.
/// </summary>
public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    public const int ManualSlotCount = 3;
    public const string WorldScenePath = "res://Scenes/Home/Rooms/World.tscn";
    private const string DefaultRoomPath = "res://Scenes/Home/Rooms/Kwarto.tscn";
    private const string SaveDirectory = "user://Saves";
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
        string path = GetSlotPath(slot);
        if (!FileAccess.FileExists(path))
            return true;
        Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok)
        {
            GD.PushError($"SaveManager: could not delete slot {slot} ({error}).");
            return false;
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

    /// <summary>Call after entering/loading a room to create an immediate safety autosave.</summary>
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
            // A save taken from the pause menu resumes into a live world.
            ["clock_running"] = true,
            ["school_started"] = DayClock.Instance.SchoolChallengeStartedToday,
            ["school_completed"] = DayClock.Instance.SchoolChallengeCompletedToday,
            ["mystery_encounter_seen"] = DayClock.Instance.FirstDayMysteryEncounterSeen,
            ["first_day_special_greeting_pending"] = DayClock.Instance.FirstDayMysteryGreetingPending,
            ["sanity_state"] = SanityManager.Instance.CreateSaveData(),
            ["room_keys"] = new Godot.Collections.Dictionary
            {
                ["hidden_points"] = RoomKeyProgression.Instance?.HiddenPoints ?? 0,
                ["keys_earned"] = RoomKeyProgression.Instance?.TotalKeysEarned ?? 0
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
        file.Flush();
        return true;
    }

    private async System.Threading.Tasks.Task<bool> LoadSnapshotAsync(Godot.Collections.Dictionary data)
    {
        // Older saves may not have a version field, and some hand-edited/exported
        // JSON files may store it as a string. Treat those as the original v1 format
        // only when the field is absent or explicitly says "1"; never silently load
        // a save that declares a newer, unknown format.
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
            // A save remains useful if a room was renamed or removed in a project update.
            // Restore the saved run into the default home room instead of rejecting its JSON.
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

        // Apply saved autoload state before World.tscn _Ready runs so rooms/UI see the
        // restored day, sanity, key ownership and daily progress from their first frame.
        DayClock.Instance?.RestoreState(day, hour, true, schoolStarted, schoolCompleted);
        DayClock.Instance?.RestoreFirstDayMysteryState(
            ReadBool(data, "mystery_encounter_seen", false),
            ReadBool(data, "first_day_special_greeting_pending", false));
        if (SanityManager.Instance != null && sanityState.Count > 0)
            SanityManager.Instance.RestoreFromSaveData(sanityState);
        RoomKeyProgression.Instance?.RestoreProgress(
            ReadInt(keyState, "hidden_points", 0),
            ReadInt(keyState, "keys_earned", 0));
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
                // If a reload fails while still in the world, keep the current session running.
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
        const string legacyPath = "user://autosave.json";

        if (!FileAccess.FileExists(path) && FileAccess.FileExists(legacyPath))
        {
            using var source = FileAccess.Open(legacyPath, FileAccess.ModeFlags.Read);
            using var destination = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (source != null && destination != null)
                destination.StoreString(source.GetAsText());
        }

        return path;
    }

    private string GetSlotPath(int slot)
    {
        EnsureSaveDirectory();
        string path = $"{SaveDirectory}/save_slot_{slot}.json";
        string legacyPath = $"user://save_slot_{slot}.json";
        if (!FileAccess.FileExists(path) && FileAccess.FileExists(legacyPath))
        {
            using var source = FileAccess.Open(legacyPath, FileAccess.ModeFlags.Read);
            using var destination = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (source != null && destination != null)
                destination.StoreString(source.GetAsText());
        }
        return path;
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

        // JSON parsers may represent a version written as 1.0 as a floating-point
        // value instead of an integer. Accept it only when it is a whole number.
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
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static float ReadFloat(Godot.Collections.Dictionary data, string key, float fallback)
    {
        if (!data.ContainsKey(key))
            return fallback;

        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.Float => (float)value.AsDouble(),
            Variant.Type.Int => value.AsInt32(),
            _ => fallback
        };
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
