using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Centralized music and sound-effect routing.
/// Music crossfades between menu/day/night tracks; UI and gameplay effects use a small
/// player pool so one sound does not cut off another.
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private const string SoundRoot = "res://Assets/Sounds/";
    private const string MusicBusName = "Music";
    private const string EffectsBusName = "Effects";
    private const float MusicVolumeDb = -9f;
    private const float MusicSilentDb = -60f;
    private const double CrossfadeSeconds = 1.6;

    private AudioStreamPlayer _musicA;
    private AudioStreamPlayer _musicB;
    private AudioStreamPlayer _activeMusicPlayer;
    private readonly List<AudioStreamPlayer> _sfxPlayers = new();
    private readonly Dictionary<string, AudioStream> _soundCache =
        new(StringComparer.OrdinalIgnoreCase);
    private int _nextSfxPlayer;
    private string _musicMode = "none";
    private int _menuTrackIndex;
    private AudioStream _menuTrackOne;
    private AudioStream _menuTrackTwo;
    private AudioStream _dayTrack;
    private AudioStream _nightTrack;
    private AudioStreamPlayer _gameOverPlayer;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        EnsureAudioBus(MusicBusName);
        EnsureAudioBus(EffectsBusName);

        _musicA = CreateMusicPlayer("MusicPlayerA");
        _musicB = CreateMusicPlayer("MusicPlayerB");
        _musicA.Finished += () => OnMusicFinished(_musicA);
        _musicB.Finished += () => OnMusicFinished(_musicB);

        for (int i = 0; i < 8; i++)
        {
            var player = new AudioStreamPlayer
            {
                Name = $"SfxPlayer_{i + 1}",
                ProcessMode = ProcessModeEnum.Always,
                Bus = EffectsBusName,
                VolumeDb = 0f
            };
            AddChild(player);
            _sfxPlayers.Add(player);
        }

        _menuTrackOne = LoadSound("Menu/Menu.mp3");
        _menuTrackTwo = LoadSound("Menu/Menu2.mp3");
        _dayTrack = LoadSound("Game/dayTime.mp3");
        _nightTrack = LoadSound("Game/nightTime.mp3");

        ApplyVolumeSettings(
            SettingsManager.Instance?.MusicVolume ?? 0.8f,
            SettingsManager.Instance?.EffectsVolume ?? 0.85f);

        if (DayClock.Instance != null)
            DayClock.Instance.TimeChanged += OnTimeChanged;
    }

    public override void _ExitTree()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.TimeChanged -= OnTimeChanged;

        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        if (_musicMode != "menu" || _activeMusicPlayer == null ||
            !GodotObject.IsInstanceValid(_activeMusicPlayer) || !_activeMusicPlayer.Playing)
            return;

        // Start the next menu track just before the current one ends.
        AudioStream activeStream = _activeMusicPlayer.Stream;
        double length = activeStream?.GetLength() ?? 0.0;
        if (length <= CrossfadeSeconds + 0.25)
            return;

        if (_activeMusicPlayer.GetPlaybackPosition() >= length - CrossfadeSeconds)
            AdvanceMenuTrack();
    }

    public void ApplyVolumeSettings(float musicVolume, float effectsVolume)
    {
        ApplyBusVolume(MusicBusName, musicVolume);
        ApplyBusVolume(EffectsBusName, effectsVolume);
    }

    private static void EnsureAudioBus(string busName)
    {
        if (AudioServer.GetBusIndex(busName) >= 0)
            return;

        AudioServer.AddBus(-1);
        int busIndex = AudioServer.BusCount - 1;
        AudioServer.SetBusName(busIndex, busName);
        AudioServer.SetBusSend(busIndex, "Master");
    }

    private static void ApplyBusVolume(string busName, float volume)
    {
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0)
            return;

        float normalized = Mathf.Clamp(volume, 0f, 1f);
        bool muted = normalized <= 0.001f;
        AudioServer.SetBusMute(busIndex, muted);
        AudioServer.SetBusVolumeDb(busIndex, muted ? -80f : Mathf.LinearToDb(normalized));
    }

    public void PlayMenuMusic()
    {
        if (_musicMode == "menu" && _activeMusicPlayer != null &&
            GodotObject.IsInstanceValid(_activeMusicPlayer) && _activeMusicPlayer.Playing)
            return;

        _musicMode = "menu";
        _menuTrackIndex = 0;
        CrossfadeToMusic(_menuTrackOne, loop: false);
    }

    public void PlayWorldMusic(bool isNight)
    {
        _musicMode = "world";
        AudioStream track = isNight ? _nightTrack : _dayTrack;

        CrossfadeToMusic(track, loop: true);
    }

    public void PlayUiClick() => PlaySoundEffect("InterfaceInteraction/uiClick.wav", -2f);
    public void PlayUiDrag() => PlaySoundEffect("InterfaceInteraction/uiDrag.mp3", -4f);
    public void PlayLockedDoor() => PlaySoundEffect("ObjectInteraction/doorKey.wav", -1f);
    public void PlayDoorUnlock() => PlaySoundEffect("ObjectInteraction/doorUnlock.mp3", -1f);
    public void PlayObjectObtain() => PlaySoundEffect("ObjectInteraction/objectObtain.wav", -1f);
    public void PlayAssessmentPassed() => PlaySoundEffect("Tasks/passedTask.mp3", -1f);
    public void PlayAssessmentFailed() => PlaySoundEffect("Tasks/failedTask.mp3", -1f);

    public double PlayGameOver()
    {
        FadeOutMusic(0.7);
        AudioStream track = LoadSound("Ending/gameOverFlatline.mp3");
        if (track == null)
            return 8.0;

        _gameOverPlayer = PlaySoundEffect("Ending/gameOverFlatline.mp3", 0f);
        return track.GetLength();
    }

    public async void FadeOutGameOver(double duration = 0.65)
    {
        AudioStreamPlayer player = _gameOverPlayer;
        _gameOverPlayer = null;
        if (player == null || !GodotObject.IsInstanceValid(player) || !player.Playing)
            return;

        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(player, "volume_db", MusicSilentDb, Math.Max(0.1, duration))
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        if (GodotObject.IsInstanceValid(player))
            player.Stop();
    }

    private AudioStreamPlayer CreateMusicPlayer(string playerName)
    {
        var player = new AudioStreamPlayer
        {
            Name = playerName,
            ProcessMode = ProcessModeEnum.Always,
            Bus = MusicBusName,
            VolumeDb = MusicSilentDb
        };
        AddChild(player);
        return player;
    }

    private AudioStream LoadSound(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        string requestedPath = SoundRoot + relativePath.Replace('\\', '/');
        string path = ResolveSoundPath(requestedPath);

        if (_soundCache.TryGetValue(path, out AudioStream cached) &&
            cached != null && GodotObject.IsInstanceValid(cached))
            return cached;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning(
                $"AudioManager: imported audio resource not found: {path}. " +
                "Verify the file extension and let Godot reimport the asset.");
            return null;
        }

        AudioStream stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"AudioManager: resource exists but could not be loaded as AudioStream: {path}");
            return null;
        }

        _soundCache[path] = stream;
        GD.Print($"[AudioManager] Loaded sound: {path}");
        return stream;
    }

    // Resolve the actual filename from the folder so casing differences do not break
    // resource loading on case-sensitive filesystems.
    private static string ResolveSoundPath(string requestedPath)
    {
        int separator = requestedPath.LastIndexOf('/');
        if (separator < 0 || separator >= requestedPath.Length - 1)
            return requestedPath;

        string directoryPath = requestedPath[..separator];
        string requestedFileName = requestedPath[(separator + 1)..];

        using DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
            return requestedPath;

        foreach (string actualFileName in directory.GetFiles())
        {
            if (string.Equals(
                actualFileName, requestedFileName, StringComparison.OrdinalIgnoreCase))
            {
                return directoryPath + "/" + actualFileName;
            }
        }

        return requestedPath;
    }

    private void AdvanceMenuTrack()
    {
        if (_musicMode != "menu")
            return;

        _menuTrackIndex = 1 - _menuTrackIndex;
        AudioStream next = _menuTrackIndex == 0 ? _menuTrackOne : _menuTrackTwo;
        CrossfadeToMusic(next, loop: false);
    }

    private void OnMusicFinished(AudioStreamPlayer player)
    {
        // If the previous song finishes during a crossfade, it is no longer active.
        if (player != _activeMusicPlayer || _musicMode != "menu")
            return;

        // Fallback for a stream whose duration cannot be queried before playback.
        AdvanceMenuTrack();
    }

    private void OnTimeChanged(float hour)
    {
        if (_musicMode != "world" || DayClock.Instance == null)
            return;

        AudioStream desired = DayClock.Instance.IsNight ? _nightTrack : _dayTrack;
        CrossfadeToMusic(desired, loop: true);
    }

    private static void ConfigureLoop(AudioStream stream, bool enabled)
    {
        if (stream is AudioStreamMP3 mp3)
        {
            mp3.Loop = enabled;
            return;
        }

        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = enabled
                ? AudioStreamWav.LoopModeEnum.Forward
                : AudioStreamWav.LoopModeEnum.Disabled;
        }
    }

    private void CrossfadeToMusic(AudioStream stream, bool loop)
    {
        if (stream == null)
            return;

        ConfigureLoop(stream, loop);

        if (_activeMusicPlayer != null &&
            GodotObject.IsInstanceValid(_activeMusicPlayer) &&
            _activeMusicPlayer.Playing &&
            _activeMusicPlayer.Stream == stream)
            return;

        AudioStreamPlayer outgoing = _activeMusicPlayer;
        AudioStreamPlayer incoming = outgoing == _musicA ? _musicB
            : outgoing == _musicB ? _musicA
            : (!_musicA.Playing ? _musicA : _musicB);

        if (incoming == outgoing)
            incoming = incoming == _musicA ? _musicB : _musicA;

        incoming.Stop();
        incoming.Stream = stream;
        incoming.VolumeDb = MusicSilentDb;
        incoming.Play();

        _activeMusicPlayer = incoming;

        Tween fadeIn = CreateTween();
        fadeIn.TweenProperty(incoming, "volume_db", MusicVolumeDb, CrossfadeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        if (outgoing != null && GodotObject.IsInstanceValid(outgoing) && outgoing.Playing)
            FadeOutAndStop(outgoing, CrossfadeSeconds);
    }

    private async void FadeOutAndStop(AudioStreamPlayer player, double duration)
    {
        Tween tween = CreateTween();
        tween.TweenProperty(player, "volume_db", MusicSilentDb, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, Tween.SignalName.Finished);

        if (GodotObject.IsInstanceValid(player) && player != _activeMusicPlayer)
            player.Stop();
    }

    private void FadeOutMusic(double duration)
    {
        AudioStreamPlayer outgoing = _activeMusicPlayer;
        _activeMusicPlayer = null;
        _musicMode = "none";

        if (outgoing != null && GodotObject.IsInstanceValid(outgoing) && outgoing.Playing)
            FadeOutAndStop(outgoing, duration);
    }

    private AudioStreamPlayer PlaySoundEffect(string relativePath, float volumeDb)
    {
        AudioStream stream = LoadSound(relativePath);
        if (stream == null || _sfxPlayers.Count == 0)
            return null;

        AudioStreamPlayer player = null;
        for (int i = 0; i < _sfxPlayers.Count; i++)
        {
            int index = (_nextSfxPlayer + i) % _sfxPlayers.Count;
            if (!_sfxPlayers[index].Playing)
            {
                player = _sfxPlayers[index];
                _nextSfxPlayer = (index + 1) % _sfxPlayers.Count;
                break;
            }
        }

        if (player == null)
        {
            player = _sfxPlayers[_nextSfxPlayer];
            _nextSfxPlayer = (_nextSfxPlayer + 1) % _sfxPlayers.Count;
        }

        player.Stop();
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.Play();
        return player;
    }
}
