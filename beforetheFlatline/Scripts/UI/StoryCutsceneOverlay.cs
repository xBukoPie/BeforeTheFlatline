using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolYardGame;

public sealed record StoryCutsceneBeat(
    string ImagePath,
    DialogueLine Dialogue,
    string EffectCue = "");

public partial class StoryCutsceneOverlay : Control
{
    private const string MemoryDistortionShader = @"shader_type canvas_item;

uniform float time_seconds = 0.0;
uniform float dizziness_strength : hint_range(0.0, 1.0) = 0.0;
uniform float nausea_strength : hint_range(0.0, 1.0) = 0.0;
uniform float pressure_strength : hint_range(0.0, 1.0) = 0.0;
uniform float panic_strength : hint_range(0.0, 1.0) = 0.0;

void fragment()
{
    vec2 centered = UV - vec2(0.5);
    float radius = length(centered);
    vec2 warped_uv = UV;

    float slow_wave = sin(time_seconds * 2.25 + centered.y * 13.0);
    warped_uv.x += slow_wave * dizziness_strength * 0.012 * (0.25 + radius);
    warped_uv.y += cos(time_seconds * 1.55 + centered.x * 9.0) * dizziness_strength * 0.004;

    vec4 pixel = texture(TEXTURE, clamp(warped_uv, vec2(0.0), vec2(1.0)));

    // Nausea drains warmth from the image and adds a faint sickly green cast.
    vec3 nausea_tint = pixel.rgb * vec3(0.72, 0.92, 0.68);
    pixel.rgb = mix(pixel.rgb, nausea_tint, nausea_strength * 0.46);

    // Pressure closes in from the edges instead of shaking the whole screen.
    float vignette = smoothstep(0.16, 0.78, radius);
    pixel.rgb *= 1.0 - pressure_strength * vignette * 0.32;

    // Panic introduces a muted red cast without a harsh full-screen flash.
    vec3 panic_tint = vec3(pixel.r * 1.05, pixel.g * 0.60, pixel.b * 0.66);
    pixel.rgb = mix(pixel.rgb, panic_tint, panic_strength * 0.32);

    COLOR = pixel;
}";

    private string _heading = "MEMORY";
    private IReadOnlyList<StoryCutsceneBeat> _beats = Array.Empty<StoryCutsceneBeat>();
    private Action _onFinished;
    private bool _isEnding;
    private bool _replayOnly;
    private bool _callbackAfterReveal;
    private string _memoryId = string.Empty;
    private bool _finished;
    private bool _worldStateRestored;
    private bool _previousTreePaused;
    private bool _previousClockRunning;
    private double _elapsed;
    private string _currentImagePath = null;

    private TextureRect _art;
    private ShaderMaterial _artMaterial;
    private CanvasLayer _transitionCanvas;
    private Control _transitionRoot;
    private ColorRect _transitionCover;
    private Label _transitionText;
    private bool _collapseTransitionRunning;
    private bool _collapseTransitionPlayed;
    private Tween _collapseTween;
    private VisualNovelScene _dialogue;
    private Tween _artTween;
    private Tween _masterBedroomBeatTween;

    // Shader uniforms are not ordinary Godot properties, so animate their values
    // in _Process and push them through ShaderMaterial.SetShaderParameter().
    private float _dizzinessCurrent;
    private float _dizzinessTarget;
    private float _nauseaCurrent;
    private float _nauseaTarget;
    private float _pressureCurrent;
    private float _pressureTarget;
    private float _panicCurrent;
    private float _panicTarget;

    public void Configure(
        string heading,
        IReadOnlyList<StoryCutsceneBeat> beats,
        Action onFinished,
        bool isEnding = false,
        string memoryId = "",
        bool replayOnly = false,
        bool callbackAfterReveal = false)
    {
        _heading = string.IsNullOrWhiteSpace(heading) ? "MEMORY" : heading;
        _beats = beats ?? Array.Empty<StoryCutsceneBeat>();
        _onFinished = onFinished;
        _isEnding = isEnding;
        _memoryId = memoryId ?? string.Empty;
        _replayOnly = replayOnly;
        _callbackAfterReveal = callbackAfterReveal;
    }

    public override void _Ready()
    {
        AddToGroup("story_cutscene");
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _previousTreePaused = GetTree().Paused;
        _previousClockRunning = DayClock.Instance?.Running ?? false;

        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        // Keep persistent HUD distortion from competing with the memory-specific effects.
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(true);
        StatusEffectsOverlay.Instance?.SetCinematicVignette(this, true);
        GetTree().Paused = true;
        BuildBackdrop();
        if (_beats.Count > 0)
        {
            SetArtwork(_beats[0].ImagePath);
            ApplyMemoryCue(_beats[0].EffectCue);
        }
        PlayRecollectionIntro();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _elapsed += delta;

        // Smoothly approach each cue target. This avoids tweening nonexistent
        // shader_parameter/... properties on ShaderMaterial.
        const float transitionSpeed = 0.12f;
        _dizzinessCurrent = Mathf.MoveToward(_dizzinessCurrent, _dizzinessTarget, transitionSpeed * dt);
        _nauseaCurrent = Mathf.MoveToward(_nauseaCurrent, _nauseaTarget, transitionSpeed * dt);
        _pressureCurrent = Mathf.MoveToward(_pressureCurrent, _pressureTarget, transitionSpeed * dt);
        _panicCurrent = Mathf.MoveToward(_panicCurrent, _panicTarget, transitionSpeed * dt);

        if (_artMaterial == null || !GodotObject.IsInstanceValid(_artMaterial))
            return;

        _artMaterial.SetShaderParameter("time_seconds", (float)_elapsed);
        _artMaterial.SetShaderParameter("dizziness_strength", _dizzinessCurrent);
        _artMaterial.SetShaderParameter("nausea_strength", _nauseaCurrent);
        _artMaterial.SetShaderParameter("pressure_strength", _pressureCurrent);
        _artMaterial.SetShaderParameter("panic_strength", _panicCurrent);
    }

    public override void _ExitTree()
    {
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.BeforeLinePresentationAsync = null;
            _dialogue.LinePresented -= OnLinePresented;
            _dialogue.DialogueFinished -= OnDialogueFinished;
            _dialogue.ChoiceSelectedAtLine -= OnChoiceSelectedAtLine;
            _dialogue.SetMemoryTransitionBlocked(false);
        }

        if (_masterBedroomBeatTween != null && _masterBedroomBeatTween.IsValid())
            _masterBedroomBeatTween.Kill();
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
        StatusEffectsOverlay.Instance?.SetCinematicVignette(this, false);
    }

    private void BuildBackdrop()
    {
        var background = new ColorRect
        {
            Name = "MemoryBackground",
            Color = new Color("05050a"),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _art = new TextureRect
        {
            Name = "MemoryArtwork",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White
        };
        _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _artMaterial = new ShaderMaterial
        {
            Shader = new Shader { Code = MemoryDistortionShader }
        };
        _art.Material = _artMaterial;
        _artMaterial.SetShaderParameter("time_seconds", 0f);
        _artMaterial.SetShaderParameter("dizziness_strength", 0f);
        _artMaterial.SetShaderParameter("nausea_strength", 0f);
        _artMaterial.SetShaderParameter("pressure_strength", 0f);
        _artMaterial.SetShaderParameter("panic_strength", 0f);
        AddChild(_art);

        // A restrained edge shade keeps the dialogue readable without dimming each line.
        var edgeShade = new ColorRect
        {
            Name = "MemoryEdgeShade",
            Color = new Color(0f, 0f, 0f, 0.12f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        edgeShade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(edgeShade);

        _transitionCanvas = new CanvasLayer
        {
            Name = "MemoryTransitionCanvas",
            Layer = 120
        };
        AddChild(_transitionCanvas);

        _transitionRoot = new Control
        {
            Name = "MemoryTransitionRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _transitionRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _transitionCanvas.AddChild(_transitionRoot);

        _transitionCover = new ColorRect
        {
            Name = "MemoryTransitionCover",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Modulate = new Color(1f, 1f, 1f, 1f)
        };
        _transitionCover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _transitionRoot.AddChild(_transitionCover);

        _transitionText = new Label
        {
            Name = "MemoryTransitionText",
            Text = "Memory recollection...",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _transitionText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _transitionText.AddThemeFontSizeOverride("font_size", 23);
        _transitionText.AddThemeColorOverride("font_color", new Color("e8e2ee"));
        _transitionText.AddThemeColorOverride("font_outline_color", new Color("08070c"));
        _transitionText.AddThemeConstantOverride("outline_size", 3);
        _transitionRoot.AddChild(_transitionText);
    }

    private async void PlayRecollectionIntro()
    {
        if (_beats.Count == 0)
        {
            StartDialogue();
            return;
        }

        _transitionCover.Visible = true;
        _transitionCover.Modulate = Colors.White;
        _transitionText.Modulate = new Color(1f, 1f, 1f, 0f);

        Tween fadeTextIn = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fadeTextIn.TweenProperty(_transitionText, "modulate:a", 1f, 0.72f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(fadeTextIn, Tween.SignalName.Finished);
        await WaitTransition(1.15f);

        Tween revealMemory = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        revealMemory.TweenProperty(_transitionText, "modulate:a", 0f, 0.5f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        revealMemory.TweenProperty(_transitionCover, "modulate:a", 0f, 0.95f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(revealMemory, Tween.SignalName.Finished);

        if (_finished || !IsInsideTree())
            return;

        _transitionCover.Visible = false;
        _transitionText.Visible = false;
        StartDialogue();
    }

    private async System.Threading.Tasks.Task WaitTransition(double seconds)
    {
        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenInterval(seconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async void StartDialogue()
    {
        if (_beats.Count == 0)
        {
            GD.PushWarning("StoryCutsceneOverlay: no dialogue beats were configured.");
            Finish();
            return;
        }

        // Put the first memory artwork on screen before creating the dialogue UI.
        // This prevents the choice buttons and dialogue panel appearing over an empty frame.
        SetArtwork(_beats[0].ImagePath);
        ApplyMemoryCue(_beats[0].EffectCue);

        PackedScene dialogueScene = GD.Load<PackedScene>("res://Scenes/UI/VisualNovelScene.tscn");
        if (dialogueScene == null)
        {
            GD.PushError("StoryCutsceneOverlay: VisualNovelScene.tscn could not be loaded.");
            Finish();
            return;
        }

        _dialogue = dialogueScene.Instantiate<VisualNovelScene>();
        _dialogue.Name = "MemoryDialogue";
        // The parent cutscene pauses the game tree; dialogue typing, input,
        // Auto mode, and its tweens must still run while paused.
        _dialogue.ProcessMode = ProcessModeEnum.Always;
        _dialogue.ShowBackground = false;
        _dialogue.ShowPlaceholders = false;
        _dialogue.ShowCharacterSlots = false;
        _dialogue.AllowPauseMenu = false;
        _dialogue.CompactChoiceMode = true;
        if (_isEnding)
            _dialogue.CharsPerSecond = 30f;
        if (_replayOnly && !string.IsNullOrWhiteSpace(_memoryId))
            _dialogue.ReplayChoiceSelections =
                StoryProgression.Instance?.GetMemoryChoiceSelections(_memoryId);
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.BeforeLinePresentationAsync = null;
        _dialogue.LinePresented += OnLinePresented;
        _dialogue.ChoiceSelectedAtLine += OnChoiceSelectedAtLine;
        _dialogue.DialogueFinished += OnDialogueFinished;
        AddChild(_dialogue);

        var lines = new List<DialogueLine>(_beats.Count);
        foreach (StoryCutsceneBeat beat in _beats)
            lines.Add(beat.Dialogue);

        // Wait one rendered frame so the packed scene completes layout before
        // typing begins. The dialogue node is Always, so this also works paused.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_finished || !IsInsideTree() ||
            _dialogue == null || !GodotObject.IsInstanceValid(_dialogue))
            return;

        GD.Print($"[StoryCutsceneOverlay] Starting {_beats.Count} dialogue beats for '{_heading}'.");
        _dialogue.StartDialogue(lines);
    }

    private void OnChoiceSelectedAtLine(int lineIndex, int choiceIndex)
    {
        if (string.IsNullOrWhiteSpace(_memoryId) || StoryProgression.Instance == null)
            return;

        if (_replayOnly &&
            StoryProgression.Instance.GetMemoryChoiceSelections(_memoryId).ContainsKey(lineIndex))
            return;

        StoryProgression.Instance.RecordMemoryChoice(_memoryId, lineIndex, choiceIndex);
    }

    private void OnLinePresented(int index, DialogueLine line)
    {
        if (index < 0 || index >= _beats.Count)
            return;

        StoryCutsceneBeat beat = _beats[index];
        GD.Print($"[StoryCutsceneOverlay] Beat {index + 1}/{_beats.Count}: {beat.Dialogue.Speaker} [{beat.EffectCue}]");

        bool collapseCue =
            string.Equals(beat.EffectCue, "blackout", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(beat.EffectCue, "collapse", StringComparison.OrdinalIgnoreCase);
        bool beginCollapse = collapseCue && !_collapseTransitionPlayed;

        if (_isEnding && index == 0)
            SetArtwork(beat.ImagePath);
        else if (_isEnding && index > 0 && !beginCollapse)
            TransitionMasterBedroomArtwork(beat.ImagePath);

        ApplyMemoryCue(beat.EffectCue);
        if (beginCollapse)
            PlayCollapseTransition(beat.ImagePath);
    }

    private async void TransitionMasterBedroomArtwork(string imagePath)
    {
        if (_finished || _art == null || !GodotObject.IsInstanceValid(_art))
            return;

        string requested = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        if (string.Equals(requested, _currentImagePath, StringComparison.OrdinalIgnoreCase))
            return;

        if (_masterBedroomBeatTween != null && _masterBedroomBeatTween.IsValid())
            _masterBedroomBeatTween.Kill();
        if (_artTween != null && _artTween.IsValid())
            _artTween.Kill();

        _masterBedroomBeatTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _masterBedroomBeatTween.TweenProperty(_art, "modulate:a", 0f, 1.65f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(_masterBedroomBeatTween, Tween.SignalName.Finished);
        if (_finished || !IsInsideTree() || !GodotObject.IsInstanceValid(_art))
            return;

        SetArtworkImmediately(requested);
        _masterBedroomBeatTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _masterBedroomBeatTween.TweenProperty(_art, "modulate:a", 1f, 2.1f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private void SetArtworkImmediately(string imagePath)
    {
        string requested = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        if (string.Equals(requested, _currentImagePath, StringComparison.OrdinalIgnoreCase))
            return;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            texture = GD.Load<Texture2D>(requested);
            if (texture == null)
                GD.PushWarning($"StoryCutsceneOverlay: could not load image '{requested}'.");
        }

        _currentImagePath = requested;
        _art.Texture = texture;
    }

    private async void PlayCollapseTransition(string imagePath)
    {
        if (_collapseTransitionRunning || _collapseTransitionPlayed ||
            _transitionCover == null || _finished)
            return;

        _collapseTransitionRunning = true;
        _collapseTransitionPlayed = true;
        _dialogue?.SetMemoryTransitionBlocked(true);
        _transitionCover.Visible = true;
        _transitionText.Visible = false;

        // Hide dialogue UI during the collapse so the transition cover can cover the full frame.
        bool dialogueWasVisible = _dialogue != null &&
            GodotObject.IsInstanceValid(_dialogue) && _dialogue.IsVisibleInTree();
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
            _dialogue.SetPresentationVisible(false);

        _transitionRoot.MoveChild(_transitionCover, _transitionRoot.GetChildCount() - 1);
        if (_transitionText != null && GodotObject.IsInstanceValid(_transitionText))
            _transitionRoot.MoveChild(_transitionText, _transitionRoot.GetChildCount() - 1);
        _transitionCover.Modulate = new Color(1f, 1f, 1f, 0f);

        _collapseTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _collapseTween.TweenProperty(_transitionCover, "modulate:a", 1f, 0.85f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(_collapseTween, Tween.SignalName.Finished);
        await WaitTransition(0.42f);

        if (_finished || !IsInsideTree())
            return;

        // Swap the bedroom frame while the blackout fully covers the previous artwork.
        SetArtworkImmediately(imagePath);

        _collapseTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _collapseTween.TweenProperty(_transitionCover, "modulate:a", 0f, 1.15f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(_collapseTween, Tween.SignalName.Finished);

        if (GodotObject.IsInstanceValid(_transitionCover))
            _transitionCover.Visible = false;
        if (!_finished && dialogueWasVisible &&
            _dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.SetPresentationVisible(true);
            _dialogue.SetMemoryTransitionBlocked(false);
        }
        _collapseTransitionRunning = false;
    }

    private void SetArtwork(string imagePath)
    {
        string requested = string.IsNullOrWhiteSpace(imagePath) ? string.Empty : imagePath.Trim();
        if (string.Equals(requested, _currentImagePath, StringComparison.OrdinalIgnoreCase))
            return;

        Texture2D texture = null;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            texture = GD.Load<Texture2D>(requested);
            if (texture == null)
                GD.PushWarning($"StoryCutsceneOverlay: could not load image '{requested}'.");
        }

        bool hasPreviousArtwork = _art.Texture != null;
        _currentImagePath = requested;

        if (!hasPreviousArtwork && texture != null)
        {
            _art.Texture = texture;
            _art.Modulate = Colors.White;
            return;
        }

        if (_artTween != null && _artTween.IsValid())
            _artTween.Kill();

        // Only the artwork crossfades when the actual image changes. Advancing dialogue
        // on the same still never fades the whole cutscene or the dialogue interface.
        _artTween = CreateTween();
        _artTween.TweenProperty(_art, "modulate:a", 0f, 0.12)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _artTween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(_art))
                _art.Texture = texture;
        }));
        _artTween.TweenProperty(_art, "modulate:a", 1f, 0.24)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private void ApplyMemoryCue(string cue)
    {
        string normalized = (cue ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            return;

        _dizzinessTarget = 0f;
        _nauseaTarget = 0f;
        _pressureTarget = 0f;
        _panicTarget = 0f;

        switch (normalized)
        {
            case "dizzy":
            case "dizziness":
                _dizzinessTarget = 0.78f;
                _nauseaTarget = 0.12f;
                break;

            case "nausea":
            case "nauseous":
                _dizzinessTarget = 0.16f;
                _nauseaTarget = 0.82f;
                break;

            case "pressure":
                _pressureTarget = 0.78f;
                break;

            case "panic":
            case "anxiety":
                _dizzinessTarget = 0.22f;
                _pressureTarget = 0.55f;
                _panicTarget = 0.82f;
                break;

            case "blackout":
                _dizzinessTarget = 0.36f;
                _pressureTarget = 0.9f;
                _panicTarget = 0.4f;
                break;

            case "relief":
                // Neutral targets restore the artwork gradually.
                break;
        }
    }

    private void OnDialogueFinished() => Finish();

    private void RestoreWorldState()
    {
        if (_worldStateRestored)
            return;

        _worldStateRestored = true;
        GetTree().Paused = _previousTreePaused;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
    }

    private async void Finish()
    {
        if (_finished)
            return;

        _finished = true;

        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.LinePresented -= OnLinePresented;
            _dialogue.ChoiceSelectedAtLine -= OnChoiceSelectedAtLine;
            _dialogue.DialogueFinished -= OnDialogueFinished;
        }

        if (_collapseTween != null && _collapseTween.IsValid())
            _collapseTween.Kill();
        if (_masterBedroomBeatTween != null && _masterBedroomBeatTween.IsValid())
            _masterBedroomBeatTween.Kill();
        _collapseTransitionRunning = false;

        // Cover the recollection before returning control to the live room.
        if (_transitionCover != null && GodotObject.IsInstanceValid(_transitionCover))
        {
            _transitionCover.Visible = true;
            _transitionText.Visible = false;
            _transitionRoot.MoveChild(_transitionCover, _transitionRoot.GetChildCount() - 1);

            Tween coverToBlack = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
            coverToBlack.TweenProperty(_transitionCover, "modulate:a", 1f, 0.48f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            await ToSignal(coverToBlack, Tween.SignalName.Finished);
        }

        if (!IsInsideTree())
            return;

        // Remove the memory artwork before revealing the live room. Otherwise
        // fading the cover exposes the memory image again, then QueueFree snaps to gameplay.
        foreach (Node child in GetChildren())
        {
            if (child == _transitionCover)
                continue;

            if (child is CanvasItem visual)
                visual.Visible = false;
        }

        if (_isEnding)
            RestoreWorldState();

        Action callback = _onFinished;
        _onFinished = null;
        if (!_callbackAfterReveal)
            callback?.Invoke();

        // The final memory callback starts the global scene fade while this cover
        // remains black. Journal replays instead reveal the Journal before returning.
        if (!_callbackAfterReveal && _isEnding && SceneTransition.Instance?.IsBusy == true)
            return;

        // For ordinary recollections, reveal the restored room only after the
        // memory overlay is fully opaque, then remove the overlay.
        if (_transitionCover != null && GodotObject.IsInstanceValid(_transitionCover))
        {
            Tween revealRoom = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
            revealRoom.TweenProperty(_transitionCover, "modulate:a", 0f, 0.62f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            await ToSignal(revealRoom, Tween.SignalName.Finished);
        }

        RestoreWorldState();
        if (_callbackAfterReveal)
            callback?.Invoke();
        if (IsInsideTree())
            QueueFree();
    }
}
