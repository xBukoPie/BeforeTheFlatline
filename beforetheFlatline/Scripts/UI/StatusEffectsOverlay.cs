using Godot;
using System;
using System.Collections.Generic;
using SchoolYardGame;

/// <summary>
/// Screen-space sanity presentation: subtle UV wobble for dizziness, animated grain for anxiety,
/// slowly surfacing words for confusion, and a pulsing heart/edge squeeze for pressure.
/// </summary>
public partial class StatusEffectsOverlay : CanvasLayer
{
    public static StatusEffectsOverlay Instance { get; private set; }

    private Control _root;
    private ColorRect _brightnessWash;
    private ColorRect _screenFx;
    private ColorRect _effectWash;
    private ColorRect _pressureTop;
    private ColorRect _pressureBottom;
    private Label _noticeLabel;
    private Label _statusLabel;
    private Label _heart;
    private readonly List<Label> _confusionWords = new();

    private float _flashTime;
    private float _noticeTime;
    private float _elapsed;
    private float _sanity = SanityManager.MaxSanity;
    private float _endingFade = 1f;
    private bool _endingTransitionStarted;
    private SanityManager.DebuffType _dominantDebuff = SanityManager.DebuffType.Dizziness;
    private Color _flashColor = new("e34d5b");
    private ShaderMaterial _fxMaterial;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        Layer = 100;
        // Sanity visuals freeze with gameplay during pause; the pause/loading UI has its own Always mode.
        ProcessMode = ProcessModeEnum.Pausable;
        BuildOverlay();

        if (SanityManager.Instance != null)
        {
            _sanity = SanityManager.Instance.CurrentSanity;
            SanityManager.Instance.OnSanityChanged += OnSanityChanged;
            SanityManager.Instance.OnDebuffChanged += OnDebuffChanged;
        }

        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SettingsChanged += ApplyBrightness;

        ApplyBrightness();
        RefreshEffects();
    }

    public override void _ExitTree()
    {
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.OnSanityChanged -= OnSanityChanged;
            SanityManager.Instance.OnDebuffChanged -= OnDebuffChanged;
        }

        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SettingsChanged -= ApplyBrightness;

        if (Instance == this)
            Instance = null;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _elapsed += dt;
        _flashTime = Mathf.Max(0f, _flashTime - dt);
        _noticeTime = Mathf.Max(0f, _noticeTime - dt);
        if (_noticeLabel != null)
            _noticeLabel.Visible = _noticeTime > 0f;

        RefreshEffects();
    }

    public void Notify(string message)
    {
        if (_noticeLabel == null || string.IsNullOrWhiteSpace(message))
            return;

        _noticeLabel.Text = message;
        _noticeLabel.Visible = true;
        _noticeTime = 4.2f;
    }

    public void TriggerTaskFailure(int sanityLoss, string debuffName)
    {
        _flashColor = new Color("ee465b");
        _flashTime = 1.25f;
        Notify($"DEBUFF APPLIED: {debuffName.ToUpperInvariant()}  •  SANITY -{sanityLoss}");
    }

    public void TriggerTaskPass()
    {
        _flashColor = new Color("65d6a5");
        _flashTime = 0.35f;
        Notify("ASSESSMENT PASSED");
    }

    /// <summary>Restore the overlay after returning to the title and starting a fresh run.</summary>
    public void ResetForNewRun()
    {
        _endingTransitionStarted = false;
        _endingFade = 1f;
        if (_root != null)
            _root.Modulate = Colors.White;
        RefreshEffects();
    }

    /// <summary>Fade the active effects away before loading the bad ending.</summary>
    public async void FadeForBadEnding(string scenePath)
    {
        if (_endingTransitionStarted)
            return;

        _endingTransitionStarted = true;
        var tween = CreateTween();
        tween.TweenProperty(_root, "modulate:a", 0f, 1.6f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(scenePath);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
    }

    private void BuildOverlay()
    {
        _root = new Control
        {
            Name = "StatusEffectsRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _brightnessWash = MakeRect("BrightnessWash");
        _screenFx = MakeRect("SanityScreenShader");
        _fxMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"
shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float dizziness_strength = 0.0;
uniform float anxiety_strength = 0.0;
uniform float pressure_strength = 0.0;
uniform float time_seconds = 0.0;

void fragment() {
    vec2 uv = SCREEN_UV;
    float wobble = dizziness_strength;
    uv.x += sin(uv.y * 24.0 + time_seconds * 2.3) * 0.016 * wobble;
    uv.x += sin(uv.y * 9.0 - time_seconds * 1.1) * 0.007 * wobble;
    uv.y += sin(uv.x * 15.0 + time_seconds * 1.7) * 0.004 * wobble;

    vec4 image = texture(screen_texture, uv);
    float grain = fract(sin(dot(FRAGCOORD.xy + floor(time_seconds * 30.0), vec2(12.9898, 78.233))) * 43758.5453);
    float lineNoise = step(0.985, fract(FRAGCOORD.y * 0.5 + floor(time_seconds * 18.0) * 0.37));
    image.rgb += (grain - 0.5) * anxiety_strength * 0.30;
    image.rgb -= lineNoise * anxiety_strength * 0.17;

    float pulse = (sin(time_seconds * 10.0) + 1.0) * 0.5;
    float vignette = smoothstep(0.48, 1.0, length(SCREEN_UV - vec2(0.5)) * 1.5);
    image.rgb *= 1.0 - vignette * pressure_strength * (0.12 + pulse * 0.20);
    COLOR = vec4(image.rgb, 1.0);
}"
            }
        };
        _screenFx.Material = _fxMaterial;
        _effectWash = MakeRect("SanityTint");

        _pressureTop = MakeRect("PressureTop");
        _pressureTop.AnchorLeft = 0f;
        _pressureTop.AnchorRight = 1f;
        _pressureTop.AnchorTop = 0f;
        _pressureTop.AnchorBottom = 0f;
        _pressureTop.OffsetBottom = 0f;

        _pressureBottom = MakeRect("PressureBottom");
        _pressureBottom.AnchorLeft = 0f;
        _pressureBottom.AnchorRight = 1f;
        _pressureBottom.AnchorTop = 1f;
        _pressureBottom.AnchorBottom = 1f;
        _pressureBottom.OffsetTop = 0f;

        _noticeLabel = new Label
        {
            Name = "NoticeLabel",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -360,
            OffsetRight = 360,
            OffsetTop = 18,
            OffsetBottom = 54,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _noticeLabel.AddThemeFontSizeOverride("font_size", 17);
        _noticeLabel.AddThemeColorOverride("font_color", Colors.White);
        _noticeLabel.AddThemeColorOverride("font_outline_color", new Color("07070d"));
        _noticeLabel.AddThemeConstantOverride("outline_size", 5);
        _root.AddChild(_noticeLabel);

        _statusLabel = new Label
        {
            Name = "StatusLabel",
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = 20,
            OffsetRight = 220,
            OffsetTop = -14,
            OffsetBottom = 14,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.AddThemeColorOverride("font_color", new Color("ebe6f1"));
        _statusLabel.AddThemeColorOverride("font_outline_color", new Color("090912"));
        _statusLabel.AddThemeConstantOverride("outline_size", 4);
        _root.AddChild(_statusLabel);

        _heart = new Label
        {
            Name = "PressureHeartbeat",
            Text = "♥",
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -104,
            OffsetRight = -38,
            OffsetTop = -110,
            OffsetBottom = -42,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _heart.AddThemeFontSizeOverride("font_size", 48);
        _heart.AddThemeColorOverride("font_color", new Color("df3a52"));
        _heart.AddThemeColorOverride("font_outline_color", new Color("130910"));
        _heart.AddThemeConstantOverride("outline_size", 5);
        _root.AddChild(_heart);

        _confusionWords.Add(CreateConfusionWord("why...", 0.04f, 0.36f, -1f));
        _confusionWords.Add(CreateConfusionWord("wait", 0.92f, 0.43f, 1f));
        _confusionWords.Add(CreateConfusionWord("what?", 0.05f, 0.64f, -1f));
        _confusionWords.Add(CreateConfusionWord("I KNOW...", 0.89f, 0.61f, 1f));
    }

    private Label CreateConfusionWord(string text, float anchorX, float anchorY, float side)
    {
        var label = new Label
        {
            Text = text,
            AnchorLeft = anchorX,
            AnchorRight = anchorX,
            AnchorTop = anchorY,
            AnchorBottom = anchorY,
            OffsetLeft = side < 0 ? 0 : -148,
            OffsetRight = side < 0 ? 148 : 0,
            OffsetTop = -12,
            OffsetBottom = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 19);
        label.AddThemeColorOverride("font_color", new Color("f0d38a"));
        label.AddThemeColorOverride("font_outline_color", new Color("100d0a"));
        label.AddThemeConstantOverride("outline_size", 4);
        _root.AddChild(label);
        return label;
    }

    private ColorRect MakeRect(string nodeName)
    {
        var rect = new ColorRect
        {
            Name = nodeName,
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(rect);
        return rect;
    }

    private void RefreshEffects()
    {
        if (_fxMaterial == null || _root == null)
            return;

        var manager = SanityManager.Instance;
        int dizziness = manager?.GetDebuffLevel(SanityManager.DebuffType.Dizziness) ?? 0;
        int confusion = manager?.GetDebuffLevel(SanityManager.DebuffType.Confusion) ?? 0;
        int anxiety = manager?.GetDebuffLevel(SanityManager.DebuffType.Anxiety) ?? 0;
        int pressure = manager?.GetDebuffLevel(SanityManager.DebuffType.Pressure) ?? 0;
        int total = dizziness + confusion + anxiety + pressure;

        float fade = _endingFade;
        float wobble = Mathf.Clamp(dizziness / 22f, 0f, 1f) * fade;
        float staticStrength = Mathf.Clamp(anxiety / 20f, 0f, 1f) * fade;
        float pressureStrength = Mathf.Clamp(pressure / 25f, 0f, 1f) * fade;
        _fxMaterial.SetShaderParameter("dizziness_strength", wobble);
        _fxMaterial.SetShaderParameter("anxiety_strength", staticStrength);
        _fxMaterial.SetShaderParameter("pressure_strength", pressureStrength);
        _fxMaterial.SetShaderParameter("time_seconds", _elapsed);

        // Confusion appears at the screen edges in separate, slow pulses rather than a full-screen
        // text wash. The effect gets stronger with additional confusion intensity.
        for (int i = 0; i < _confusionWords.Count; i++)
        {
            float wave = (Mathf.Sin(_elapsed * 0.8f + i * 1.9f) + 1f) * 0.5f;
            bool active = confusion > 0 && wave > 0.25f && _endingFade > 0.02f;
            _confusionWords[i].Visible = active;
            _confusionWords[i].Modulate = new Color(1f, 1f, 1f,
                active ? Mathf.Clamp((0.12f + wave * 0.45f) * confusion / 18f, 0f, 0.7f) * fade : 0f);
        }

        float pulse = (Mathf.Sin(_elapsed * (8f + pressure / 22f)) + 1f) * 0.5f;
        _heart.Visible = pressure > 0 && _endingFade > 0.02f;
        _heart.Scale = Vector2.One * (0.82f + pulse * (0.22f + pressureStrength * 0.22f));
        _heart.Modulate = new Color(1f, 1f, 1f,
            _heart.Visible ? (0.38f + pulse * 0.52f) * fade : 0f);

        float squeeze = pressure > 0 ? 8f + pressureStrength * 38f + pulse * 10f : 0f;
        float pressureAlpha = Mathf.Clamp(pressureStrength * (0.18f + pulse * 0.16f) * fade, 0f, 0.38f);
        _pressureTop.OffsetBottom = squeeze;
        _pressureBottom.OffsetTop = -squeeze;
        _pressureTop.Color = new Color(0.72f, 0.08f, 0.18f, pressureAlpha);
        _pressureBottom.Color = _pressureTop.Color;

        float lowSanity = Mathf.Clamp((SanityManager.StableSanityThreshold - _sanity) /
            (float)SanityManager.StableSanityThreshold, 0f, 1f);
        Color tint = GetStackTint(dizziness, confusion, anxiety, pressure);
        if (_flashTime > 0f)
            tint = tint.Lerp(_flashColor, 0.62f);
        float washAlpha = Mathf.Clamp(lowSanity * 0.17f + total / 420f, 0f, 0.30f) * fade;
        if (_flashTime > 0f)
            washAlpha = Mathf.Max(washAlpha, (_flashTime / 1.25f) * 0.14f * fade);
        _statusLabel.Visible = total > 0 || _sanity < SanityManager.StableSanityThreshold;
        _statusLabel.Text = total > 0 ? "DISTRESS" : (_sanity < 25 ? "CRITICAL" : "UNSTEADY");
        _statusLabel.Modulate = new Color(1f, 1f, 1f, 0.55f * fade);

        // The tint remains low-alpha; the recognizable debuff-specific cues do the main work.
        _effectWash.Color = new Color(tint.R, tint.G, tint.B, washAlpha);
        _screenFx.Modulate = new Color(1f, 1f, 1f, fade);

        _pressureTop.Visible = pressure > 0;
        _pressureBottom.Visible = pressure > 0;
        _dominantDebuff = FindDominant(dizziness, confusion, anxiety, pressure);
    }

    private static SanityManager.DebuffType FindDominant(int d, int c, int a, int p)
    {
        int max = Math.Max(Math.Max(d, c), Math.Max(a, p));
        if (max == c) return SanityManager.DebuffType.Confusion;
        if (max == a) return SanityManager.DebuffType.Anxiety;
        if (max == p) return SanityManager.DebuffType.Pressure;
        return SanityManager.DebuffType.Dizziness;
    }

    private Color GetStackTint(int d, int c, int a, int p)
    {
        if (d + c + a + p == 0)
            return _sanity < SanityManager.StableSanityThreshold ? new Color("d9233e") : DebuffColor(_dominantDebuff);

        Color cd = DebuffColor(SanityManager.DebuffType.Dizziness);
        Color cc = DebuffColor(SanityManager.DebuffType.Confusion);
        Color ca = DebuffColor(SanityManager.DebuffType.Anxiety);
        Color cp = DebuffColor(SanityManager.DebuffType.Pressure);
        float sum = d + c + a + p;
        return new Color(
            (cd.R * d + cc.R * c + ca.R * a + cp.R * p) / sum,
            (cd.G * d + cc.G * c + ca.G * a + cp.G * p) / sum,
            (cd.B * d + cc.B * c + ca.B * a + cp.B * p) / sum);
    }

    private void OnSanityChanged(int current)
    {
        _sanity = current;
        RefreshEffects();
    }

    private void OnDebuffChanged(SanityManager.DebuffType type, int intensity)
    {
        if (intensity > 0)
            _flashColor = DebuffColor(type);
        RefreshEffects();
    }

    private void ApplyBrightness()
    {
        if (_brightnessWash == null)
            return;

        float brightness = SettingsManager.Instance?.Brightness ?? 0.88f;
        float alpha = brightness < 1f
            ? (1f - brightness) * 0.62f
            : -(brightness - 1f) * 0.18f;
        _brightnessWash.Color = alpha >= 0f
            ? new Color(0f, 0f, 0f, alpha)
            : new Color(1f, 1f, 1f, -alpha);
    }

    private static Color DebuffColor(SanityManager.DebuffType type) => type switch
    {
        SanityManager.DebuffType.Dizziness => new Color("9876dc"),
        SanityManager.DebuffType.Confusion => new Color("e0b44e"),
        SanityManager.DebuffType.Anxiety => new Color("db4054"),
        SanityManager.DebuffType.Pressure => new Color("7896aa"),
        _ => new Color("9876dc")
    };
}
