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
    private AudioStreamPlayer _sadEndingPlayer;
    private AudioStreamPlayer _goodEnding1Player;
    private AudioStreamPlayer _goodEnding2Player;
    private int _goodEnding1RequestId;
    private AudioStreamPlayer _goodEndingHeartbeatPlayer;
    private AudioStream _goodEndingHeartbeatStream;
    private bool _goodEndingHeartbeatStarted;
    private bool _goodEndingHeartbeatFading;

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
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0)
        {
            AudioServer.AddBus(-1);
            busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, busName);
        }

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
    public void PlayFallThud() => PlaySoundEffect("SoundFX/fallThud.mp3", -1f);

    public void FadeOutBackgroundMusic(double duration = 0.75) =>
        FadeOutMusic(duration);

    public async void PlayGoodEnding1()
    {
        int requestId = ++_goodEnding1RequestId;
        FadeOutMusic(0.65);

        // Let the world score finish fading before the summary cue begins.
        SceneTreeTimer timer = GetTree().CreateTimer(0.68, true);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

        if (requestId != _goodEnding1RequestId ||
            !IsCurrentScene("res://Scenes/Ending/GoodEndingBriefing.tscn"))
            return;

        string preferredPath = ResolveSoundPath(SoundRoot + "Ending/goodEnding1.mp3");
        string selectedPath = ResourceLoader.Exists(preferredPath)
            ? "Ending/goodEnding1.mp3"
            : "Ending/goodEnding.mp3";
        AudioStream stream = LoadSound(selectedPath);

        if (stream == null)
        {
            GD.PushWarning("AudioManager: good-ending summary audio was not found.");
            return;
        }

        StopTrackedEndingPlayer(_goodEnding1Player);
        _goodEnding1Player = PlayTrackedEndingMusic("GoodEnding1Player", stream, -2f);
    }

    public void FadeOutGoodEnding1(double duration = 0.75)
    {
        _goodEnding1RequestId++;
        AudioStreamPlayer player = _goodEnding1Player;
        _goodEnding1Player = null;
        FadeTrackedEndingPlayer(player, duration);
    }

    public void PlayGoodEnding2()
    {
        if (!IsCurrentScene("res://Scenes/Ending/EndingCredits.tscn"))
            return;

        FadeOutMusic(0.55);
        FadeOutGoodEnding1(0.55);

        AudioStream stream = LoadSound("Ending/goodEnding2.wav");
        if (stream == null)
        {
            GD.PushWarning("AudioManager: good-ending credits audio was not found.");
            return;
        }

        StopTrackedEndingPlayer(_goodEnding2Player);
        _goodEnding2Player = PlayTrackedEndingMusic("GoodEnding2Player", stream, -2f);
    }

    public void FadeOutGoodEnding2(double duration = 0.75)
    {
        AudioStreamPlayer player = _goodEnding2Player;
        _goodEnding2Player = null;
        FadeTrackedEndingPlayer(player, duration);
    }

    private AudioStreamPlayer PlayTrackedEndingMusic(
        string playerName, AudioStream stream, float targetVolumeDb)
    {
        if (stream == null)
            return null;

        var player = new AudioStreamPlayer
        {
            Name = playerName,
            ProcessMode = ProcessModeEnum.Always,
            Bus = MusicBusName,
            Stream = stream,
            VolumeDb = MusicSilentDb
        };
        AddChild(player);
        player.Play();

        Tween fadeIn = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fadeIn.TweenProperty(player, "volume_db", targetVolumeDb, 0.48)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        return player;
    }

    private void StopTrackedEndingPlayer(AudioStreamPlayer player)
    {
        if (player != null && GodotObject.IsInstanceValid(player))
            player.Stop();
    }

    private async void FadeTrackedEndingPlayer(AudioStreamPlayer player, double duration)
    {
        if (player == null || !GodotObject.IsInstanceValid(player) || !player.Playing)
            return;

        Tween fade = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fade.TweenProperty(player, "volume_db", MusicSilentDb, Math.Max(0.1, duration))
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(fade, Tween.SignalName.Finished);

        if (GodotObject.IsInstanceValid(player))
            player.Stop();
    }

    private bool IsCurrentScene(string scenePath)
    {
        Node current = GetTree().CurrentScene;
        return current != null &&
            string.Equals(current.SceneFilePath, scenePath, StringComparison.OrdinalIgnoreCase);
    }

    public async void PlaySadEndingAfter(double delay)
    {
        if (delay > 0.0)
        {
            SceneTreeTimer timer = GetTree().CreateTimer(delay, true);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        }

        Node currentScene = GetTree().CurrentScene;
        if (SceneTransition.Instance?.IsBusy == true ||
            currentScene == null ||
            !string.Equals(currentScene.SceneFilePath, "res://Scenes/Ending/BadEnding.tscn",
                StringComparison.OrdinalIgnoreCase))
            return;

        FadeOutGameOver(0.55);
        _sadEndingPlayer = PlaySoundEffect("Ending/sadEnding.mp3", -3f);
    }

    public double PlayGoodEndingHeartbeat()
    {
        if (_goodEndingHeartbeatPlayer != null &&
            GodotObject.IsInstanceValid(_goodEndingHeartbeatPlayer) &&
            _goodEndingHeartbeatPlayer.Playing)
            return GetGoodEndingHeartbeatRemaining();

        if (_goodEndingHeartbeatStarted)
            return 0.0;

        _goodEndingHeartbeatStream = LoadGoodEndingHeartbeat();
        if (_goodEndingHeartbeatStream == null)
            return 0.0;

        if (_goodEndingHeartbeatPlayer == null ||
            !GodotObject.IsInstanceValid(_goodEndingHeartbeatPlayer))
        {
            _goodEndingHeartbeatPlayer = new AudioStreamPlayer
            {
                Name = "GoodEndingHeartbeatPlayer",
                ProcessMode = ProcessModeEnum.Always,
                Bus = EffectsBusName,
                VolumeDb = 0f
            };
            AddChild(_goodEndingHeartbeatPlayer);
        }

        _goodEndingHeartbeatPlayer.Stream = _goodEndingHeartbeatStream;
        _goodEndingHeartbeatPlayer.VolumeDb = 0f;
        _goodEndingHeartbeatPlayer.Play();
        _goodEndingHeartbeatStarted = true;
        _goodEndingHeartbeatFading = false;
        GD.Print("[AudioManager] Good-ending heartbeat started.");
        return _goodEndingHeartbeatStream.GetLength();
    }

    public void ResetGoodEndingHeartbeat()
    {
        if (_goodEndingHeartbeatPlayer != null &&
            GodotObject.IsInstanceValid(_goodEndingHeartbeatPlayer))
            _goodEndingHeartbeatPlayer.Stop();

        _goodEndingHeartbeatStarted = false;
        _goodEndingHeartbeatFading = false;
    }

    public double GetGoodEndingHeartbeatRemaining()
    {
        if (_goodEndingHeartbeatPlayer == null ||
            !GodotObject.IsInstanceValid(_goodEndingHeartbeatPlayer) ||
            !_goodEndingHeartbeatPlayer.Playing)
            return 0.0;

        double length = _goodEndingHeartbeatPlayer.Stream?.GetLength() ?? 0.0;
        return Math.Max(0.0, length - _goodEndingHeartbeatPlayer.GetPlaybackPosition());
    }

    public async void FadeOutGoodEndingHeartbeatAfter(double delay, double duration = 2.2)
    {
        if (delay > 0.0)
        {
            SceneTreeTimer timer = GetTree().CreateTimer(delay, true);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        }

        FadeOutGoodEndingHeartbeat(duration);
    }

    public async void FadeOutGoodEndingHeartbeat(double duration = 2.2)
    {
        AudioStreamPlayer player = _goodEndingHeartbeatPlayer;
        if (_goodEndingHeartbeatFading || player == null ||
            !GodotObject.IsInstanceValid(player) || !player.Playing)
            return;

        _goodEndingHeartbeatFading = true;
        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(player, "volume_db", MusicSilentDb, Math.Max(0.1, duration))
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        if (GodotObject.IsInstanceValid(player))
            player.Stop();
    }

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
        AudioStreamPlayer[] players = { _gameOverPlayer, _sadEndingPlayer };
        _gameOverPlayer = null;
        _sadEndingPlayer = null;

        foreach (AudioStreamPlayer player in players)
        {
            if (player == null || !GodotObject.IsInstanceValid(player) || !player.Playing)
                continue;

            Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenProperty(player, "volume_db", MusicSilentDb, Math.Max(0.1, duration))
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            await ToSignal(tween, Tween.SignalName.Finished);

            if (GodotObject.IsInstanceValid(player))
                player.Stop();
        }
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

    private AudioStream LoadGoodEndingHeartbeat()
    {
        const string preferredPath = "Ending/goodEndingHeartbeat.mp3";
        AudioStream preferred = LoadSound(preferredPath);
        if (preferred != null)
            return preferred;

        string[] folders =
        {
            "res://Assets/Sounds/Ending",
            "res://Assets/Heartbeat",
            "res://Assets/Sounds/Heartbeat"
        };

        foreach (string folderPath in folders)
        {
            using DirAccess directory = DirAccess.Open(folderPath);
            if (directory == null)
                continue;

            string[] files = directory.GetFiles();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string lower = file.ToLowerInvariant();
                if (!lower.Contains("heartbeat", StringComparison.Ordinal) ||
                    !(lower.EndsWith(".mp3") || lower.EndsWith(".wav") ||
                      lower.EndsWith(".ogg") || lower.EndsWith(".opus")))
                    continue;

                string candidatePath = folderPath + "/" + file;
                AudioStream stream = GD.Load<AudioStream>(candidatePath);
                if (stream != null)
                {
                    GD.Print($"[AudioManager] Loaded good-ending heartbeat: {candidatePath}");
                    return stream;
                }
            }
        }

        GD.PushWarning(
            "AudioManager: heartbeat audio was not found. Expected " +
            "res://Assets/Sounds/Ending/goodEndingHeartbeat.mp3.");
        return null;
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
