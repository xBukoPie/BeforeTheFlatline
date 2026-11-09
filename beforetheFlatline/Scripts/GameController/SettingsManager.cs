using Godot;
using System;

namespace SchoolYardGame;

public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; }
    public event Action SettingsChanged;
    public event Action SuperUserAccessRequested;

    private const string SettingsPath = "user://before_the_flatline_settings.cfg";
    private const string AudioBusName = "Master";

    public float MasterVolume { get; private set; } = 0.85f;
    public float MusicVolume { get; private set; } = 0.8f;
    public float EffectsVolume { get; private set; } = 0.85f;
    public float Brightness { get; private set; } = 0.88f;
    public bool Fullscreen { get; private set; } = false;
    public string PlayerName { get; private set; } = "Player";
    public bool SuperUserAccessEnabledThisSession { get; private set; }

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        LoadSettings();
        ApplyAll();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyVolume();
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyVolume();
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    public void SetEffectsVolume(float value)
    {
        EffectsVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyVolume();
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    public void SetBrightness(float value)
    {
        Brightness = Mathf.Clamp(value, 0.65f, 1.05f);
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    public void SetFullscreen(bool enabled)
    {
        Fullscreen = enabled;
        ApplyDisplayMode();
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    public void SetPlayerName(string value)
    {
        string enteredName = (value ?? string.Empty).Trim();

        // Accept both spellings so the command remains compatible with earlier instructions.
        // The command is consumed immediately and is never stored as the character name.
        if (IsSuperUserCommand(enteredName))
        {
            SuperUserAccessEnabledThisSession = true;
            PlayerName = "Default";
            SaveSettings();
            SettingsChanged?.Invoke();
            SuperUserAccessRequested?.Invoke();
            return;
        }

        string normalized = string.IsNullOrWhiteSpace(enteredName) ? "Player" : enteredName;
        if (normalized.Length > 24)
            normalized = normalized[..24];

        if (PlayerName == normalized)
            return;

        PlayerName = normalized;
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    private static bool IsSuperUserCommand(string value)
    {
        string command = (value ?? string.Empty).Trim();
        return string.Equals(command, "/BTFDevMode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(command, "/BFTDevMode", StringComparison.OrdinalIgnoreCase);
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok)
            return;

        MasterVolume = Mathf.Clamp((float)config.GetValue("audio", "master_volume", MasterVolume), 0f, 1f);
        MusicVolume = Mathf.Clamp((float)config.GetValue("audio", "music_volume", MusicVolume), 0f, 1f);
        EffectsVolume = Mathf.Clamp((float)config.GetValue("audio", "effects_volume", EffectsVolume), 0f, 1f);
        Brightness = Mathf.Clamp((float)config.GetValue("display", "brightness", Brightness), 0.65f, 1.05f);
        Fullscreen = (bool)config.GetValue("display", "fullscreen", Fullscreen);
        PlayerName = ((string)config.GetValue("player", "name", PlayerName) ?? PlayerName).Trim();
        if (string.IsNullOrWhiteSpace(PlayerName)) PlayerName = "Player";
        if (IsSuperUserCommand(PlayerName))
        {
            SuperUserAccessEnabledThisSession = true;
            PlayerName = "Default";
            SaveSettings();
        }
        if (PlayerName.Length > 24) PlayerName = PlayerName[..24];
    }

    private void ApplyAll()
    {
        ApplyVolume();
        ApplyDisplayMode();
        SettingsChanged?.Invoke();
    }

    private void ApplyVolume()
    {
        int bus = AudioServer.GetBusIndex(AudioBusName);
        if (bus >= 0)
        {
            AudioServer.SetBusMute(bus, MasterVolume <= 0.001f);
            AudioServer.SetBusVolumeDb(bus,
                MasterVolume <= 0.001f ? -80f : Mathf.LinearToDb(MasterVolume));
        }

        AudioManager.Instance?.ApplyVolumeSettings(MusicVolume, EffectsVolume);
    }

    private void ApplyDisplayMode()
    {
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master_volume", MasterVolume);
        config.SetValue("audio", "music_volume", MusicVolume);
        config.SetValue("audio", "effects_volume", EffectsVolume);
        config.SetValue("display", "brightness", Brightness);
        config.SetValue("display", "fullscreen", Fullscreen);
        config.SetValue("player", "name", PlayerName);
        config.Save(SettingsPath);
    }
}
