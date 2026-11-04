using Godot;
using System;
using System.Collections.Generic;
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
    private bool _finished;
    private bool _previousTreePaused;
    private bool _previousClockRunning;
    private double _elapsed;
    private string _currentImagePath = null;

    private TextureRect _art;
    private ShaderMaterial _artMaterial;
    private VisualNovelScene _dialogue;
    private Tween _artTween;
    private Control _introLayer;

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
        bool isEnding = false)
    {
        _heading = string.IsNullOrWhiteSpace(heading) ? "MEMORY" : heading;
        _beats = beats ?? Array.Empty<StoryCutsceneBeat>();
        _onFinished = onFinished;
        _isEnding = isEnding;
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
        GetTree().Paused = true;
        BuildBackdrop();
        PlayMemoryOpening();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _elapsed += delta;

        // Smoothly approach each cue target. This avoids tweening nonexistent
        // shader_parameter/... properties on ShaderMaterial.
        const float transitionSpeed = 1.8f;
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
            _dialogue.LinePresented -= OnLinePresented;
            _dialogue.DialogueFinished -= OnDialogueFinished;
        }

        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);
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
    }

    private async void PlayMemoryOpening()
    {
        _introLayer = new Control
        {
            Name = "MemoryOpeningTransition",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _introLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_introLayer);

        var veil = new ColorRect
        {
            Color = new Color("030309"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _introLayer.AddChild(veil);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _introLayer.AddChild(center);

        var stack = new VBoxContainer();
        stack.Alignment = BoxContainer.AlignmentMode.Center;
        stack.AddThemeConstantOverride("separation", 18);
        stack.CustomMinimumSize = new Vector2(760, 180);
        center.AddChild(stack);

        var eyebrow = new Label
        {
            Text = _heading.ToUpperInvariant(),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
        eyebrow.AddThemeFontSizeOverride("font_size", 15);
        eyebrow.AddThemeColorOverride("font_color", new Color("c5a7f2"));
        stack.AddChild(eyebrow);

        var title = new Label
        {
            Text = "MEMORY COMES FLOODING YOUR HEAD.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color("f5ebff"));
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 3);
        stack.AddChild(title);

        var subtitle = new Label
        {
            Text = _isEnding
                ? "The final memory is waiting on the other side."
                : "The photograph pulls you into something you thought you had forgotten.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
        subtitle.AddThemeFontSizeOverride("font_size", 18);
        subtitle.AddThemeColorOverride("font_color", new Color("b9b1c8"));
        stack.AddChild(subtitle);

        var tween = CreateTween();
        tween.TweenProperty(eyebrow, "modulate:a", 1f, 0.55)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(title, "modulate:a", 1f, 0.75)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.7);
        tween.TweenProperty(subtitle, "modulate:a", 1f, 0.7)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(1.1);
        tween.TweenProperty(_introLayer, "modulate:a", 0f, 0.75)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        await ToSignal(tween, Tween.SignalName.Finished);
        if (!IsInsideTree() || _finished)
            return;

        _introLayer.QueueFree();
        _introLayer = null;
        StartDialogue();
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
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.LinePresented += OnLinePresented;
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

    private void OnLinePresented(int index, DialogueLine line)
    {
        if (index < 0 || index >= _beats.Count)
            return;

        StoryCutsceneBeat beat = _beats[index];
        GD.Print($"[StoryCutsceneOverlay] Beat {index + 1}/{_beats.Count}: {beat.Dialogue.Speaker} [{beat.EffectCue}]");
        SetArtwork(beat.ImagePath);
        ApplyMemoryCue(beat.EffectCue);
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

    private async void Finish()
    {
        if (_finished)
            return;

        _finished = true;

        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.LinePresented -= OnLinePresented;
            _dialogue.DialogueFinished -= OnDialogueFinished;
        }

        // This fade happens once when leaving the memory, never between dialogue lines.
        Tween exitTween = CreateTween();
        exitTween.TweenProperty(this, "modulate:a", 0f, 0.42)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(exitTween, Tween.SignalName.Finished);

        if (!IsInsideTree())
            return;

        GetTree().Paused = _previousTreePaused;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;
        StatusEffectsOverlay.Instance?.SetPauseMenuVisible(false);

        Action callback = _onFinished;
        _onFinished = null;
        QueueFree();
        callback?.Invoke();
    }
}
