using Godot;
using System;

namespace SchoolYardGame;

public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; }
    public event Action SettingsChanged;

    private const string SettingsPath = "user://before_the_flatline_settings.cfg";
    private const string AudioBusName = "Master";

    public float MasterVolume { get; private set; } = 0.85f;
    public float Brightness { get; private set; } = 0.88f;
    public bool Fullscreen { get; private set; } = false;
    public string PlayerName { get; private set; } = "Player";

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
        string normalized = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        if (normalized.Length > 24)
            normalized = normalized[..24];

        if (PlayerName == normalized)
            return;

        PlayerName = normalized;
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok)
            return;

        MasterVolume = Mathf.Clamp((float)config.GetValue("audio", "master_volume", MasterVolume), 0f, 1f);
        Brightness = Mathf.Clamp((float)config.GetValue("display", "brightness", Brightness), 0.65f, 1.05f);
        Fullscreen = (bool)config.GetValue("display", "fullscreen", Fullscreen);
        PlayerName = ((string)config.GetValue("player", "name", PlayerName) ?? PlayerName).Trim();
        if (string.IsNullOrWhiteSpace(PlayerName)) PlayerName = "Player";
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
        if (bus < 0)
            return;

        AudioServer.SetBusMute(bus, MasterVolume <= 0.001f);
        AudioServer.SetBusVolumeDb(bus, MasterVolume <= 0.001f ? -80f : Mathf.LinearToDb(MasterVolume));
    }

    private void ApplyDisplayMode()
    {
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master_volume", MasterVolume);
        config.SetValue("display", "brightness", Brightness);
        config.SetValue("display", "fullscreen", Fullscreen);
        config.SetValue("player", "name", PlayerName);
        config.Save(SettingsPath);
    }
}
