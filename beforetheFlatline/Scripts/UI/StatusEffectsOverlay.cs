using Godot;
using System;
using System.Collections.Generic;
using SchoolYardGame;

/// <summary>
/// Persistent screen tint/status notice. Uses low-alpha color washes and edge strips so effects
/// remain visible without blocking game input or making the scene difficult to read.
/// </summary>
public partial class StatusEffectsOverlay : CanvasLayer
{
    public static StatusEffectsOverlay Instance { get; private set; }

    private ColorRect _brightnessWash;
    private ColorRect _effectWash;
    private ColorRect _topEdge;
    private ColorRect _bottomEdge;
    private ColorRect _leftEdge;
    private ColorRect _rightEdge;
    private Label _statusLabel;
    private Label _noticeLabel;
    private Label _fxGlyph;
    private Label _fxLeftGlyph;
    private Label _fxRightGlyph;
    private ColorRect _pressureTop;
    private ColorRect _pressureBottom;

    private float _flashTime;
    private float _noticeTime;
    private float _elapsed;
    private float _sanity = 100f;
    private SanityManager.DebuffType _dominantDebuff = SanityManager.DebuffType.Dizziness;
    private int _dominantLevel;
    private Color _flashColor = new Color("e34d5b");

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        Layer = 100;
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

        UpdateDebuffCue();
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
        UpdateDebuffCue();
    }

    public void TriggerTaskPass()
    {
        _flashColor = new Color("65d6a5");
        _flashTime = 0.35f;
        Notify("ASSESSMENT PASSED");
    }

    private void BuildOverlay()
    {
        _brightnessWash = MakeRect("BrightnessWash");
        _effectWash = MakeRect("DebuffWash");

        _topEdge = MakeRect("TopEdge");
        _topEdge.AnchorLeft = 0f;
        _topEdge.AnchorTop = 0f;
        _topEdge.AnchorRight = 1f;
        _topEdge.AnchorBottom = 0f;
        _topEdge.OffsetLeft = 0f;
        _topEdge.OffsetTop = 0f;
        _topEdge.OffsetRight = 0f;
        _topEdge.OffsetBottom = 14f;

        _bottomEdge = MakeRect("BottomEdge");
        _bottomEdge.AnchorLeft = 0f;
        _bottomEdge.AnchorTop = 1f;
        _bottomEdge.AnchorRight = 1f;
        _bottomEdge.AnchorBottom = 1f;
        _bottomEdge.OffsetLeft = 0f;
        _bottomEdge.OffsetTop = -18f;
        _bottomEdge.OffsetRight = 0f;
        _bottomEdge.OffsetBottom = 0f;

        _leftEdge = MakeRect("LeftEdge");
        _leftEdge.AnchorLeft = 0f;
        _leftEdge.AnchorTop = 0f;
        _leftEdge.AnchorRight = 0f;
        _leftEdge.AnchorBottom = 1f;
        _leftEdge.OffsetLeft = 0f;
        _leftEdge.OffsetTop = 0f;
        _leftEdge.OffsetRight = 16f;
        _leftEdge.OffsetBottom = 0f;

        _rightEdge = MakeRect("RightEdge");
        _rightEdge.AnchorLeft = 1f;
        _rightEdge.AnchorTop = 0f;
        _rightEdge.AnchorRight = 1f;
        _rightEdge.AnchorBottom = 1f;
        _rightEdge.OffsetLeft = -16f;
        _rightEdge.OffsetTop = 0f;
        _rightEdge.OffsetRight = 0f;
        _rightEdge.OffsetBottom = 0f;

        _statusLabel = new Label
        {
            Name = "StatusLabel",
            Position = new Vector2(18, 72),
            Size = new Vector2(760, 42),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        _statusLabel.AddThemeColorOverride("font_color", new Color("e8eaf0"));
        _statusLabel.AddThemeColorOverride("font_outline_color", new Color("10131c"));
        _statusLabel.AddThemeConstantOverride("outline_size", 4);
        AddChild(_statusLabel);

        _noticeLabel = new Label
        {
            Name = "NoticeLabel",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -330,
            OffsetRight = 330,
            AnchorTop = 0f,
            OffsetTop = 20,
            OffsetBottom = 64,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _noticeLabel.AddThemeFontSizeOverride("font_size", 18);
        _noticeLabel.AddThemeColorOverride("font_color", Colors.White);
        _noticeLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _noticeLabel.AddThemeConstantOverride("outline_size", 5);
        AddChild(_noticeLabel);

        // A large, animated center cue makes the particular debuff readable at the moment it
        // is applied, instead of relying only on the persistent low-alpha screen tint.
        _fxGlyph = CreateCueLabel("DebuffFxGlyph", "", 76, new Vector2(-95, -90), new Vector2(95, 12));
        _fxLeftGlyph = CreateCueLabel("DebuffFxLeft", "", 36, new Vector2(-280, -42), new Vector2(-190, 36));
        _fxRightGlyph = CreateCueLabel("DebuffFxRight", "", 36, new Vector2(190, -42), new Vector2(280, 36));

        _pressureTop = new ColorRect
        {
            Name = "PressureTopBar",
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = 0f, OffsetTop = 0f, OffsetRight = 0f, OffsetBottom = 0f,
            Visible = false
        };
        AddChild(_pressureTop);
        _pressureBottom = new ColorRect
        {
            Name = "PressureBottomBar",
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 0f, OffsetTop = 0f, OffsetRight = 0f, OffsetBottom = 0f,
            Visible = false
        };
        AddChild(_pressureBottom);

        // Keep the sliding pressure bars below the status/notice labels so they never obscure
        // the textual cue while still overlaying the world and the persistent color tint.
        int statusIndex = _statusLabel.GetIndex();
        MoveChild(_pressureTop, statusIndex);
        MoveChild(_pressureBottom, _statusLabel.GetIndex());
    }

    private Label CreateCueLabel(string nodeName, string text, int fontSize, Vector2 offsetMin, Vector2 offsetMax)
    {
        var label = new Label
        {
            Name = nodeName,
            Text = text,
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = offsetMin.X, OffsetTop = offsetMin.Y,
            OffsetRight = offsetMax.X, OffsetBottom = offsetMax.Y,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color("090b14"));
        label.AddThemeConstantOverride("outline_size", 7);
        AddChild(label);
        return label;
    }

    private void UpdateDebuffCue()
    {
        if (_fxGlyph == null)
            return;

        var manager = SanityManager.Instance;
        int dizziness = manager?.GetDebuffLevel(SanityManager.DebuffType.Dizziness) ?? 0;
        int confusion = manager?.GetDebuffLevel(SanityManager.DebuffType.Confusion) ?? 0;
        int anxiety = manager?.GetDebuffLevel(SanityManager.DebuffType.Anxiety) ?? 0;
        int pressure = manager?.GetDebuffLevel(SanityManager.DebuffType.Pressure) ?? 0;
        int total = dizziness + confusion + anxiety + pressure;
        bool unstableSanity = _sanity < SanityManager.StableSanityThreshold;
        bool active = total > 0 || unstableSanity;
        float pulse = (Mathf.Sin(_elapsed * 7f) + 1f) * 0.5f;

        // The cue is state-driven, not timer-driven. Each symbol disappears only when that
        // debuff's stack decays to zero; the generic sanity cue remains until sanity stabilizes.
        _fxGlyph.Visible = active;
        _fxLeftGlyph.Visible = active && (dizziness > 0 || confusion > 0 || anxiety > 0 || unstableSanity);
        _fxRightGlyph.Visible = active && (dizziness > 0 || confusion > 0 || anxiety > 0 || unstableSanity);
        _pressureTop.Visible = pressure > 0;
        _pressureBottom.Visible = pressure > 0;

        var symbols = new List<string>();
        if (dizziness > 0) symbols.Add("◎");
        if (confusion > 0) symbols.Add("?!");
        if (anxiety > 0) symbols.Add("!");
        if (pressure > 0) symbols.Add("PRESSURE");
        if (symbols.Count == 0 && unstableSanity) symbols.Add("...");
        _fxGlyph.Text = string.Join("   ", symbols);

        _fxLeftGlyph.Text = dizziness > 0 ? "◌" : confusion > 0 ? "?" : anxiety > 0 ? "!" : "...";
        _fxRightGlyph.Text = confusion > 0 ? "??" : dizziness > 0 ? "◌" : anxiety > 0 ? "!" : "...";

        Color tint = GetStackTint(dizziness, confusion, anxiety, pressure);
        float alpha = Mathf.Clamp(0.58f + total / 250f, 0.58f, 1f);
        _fxGlyph.Modulate = new Color(1f, 1f, 1f, alpha);
        _fxLeftGlyph.Modulate = new Color(1f, 1f, 1f, alpha);
        _fxRightGlyph.Modulate = new Color(1f, 1f, 1f, alpha);
        _fxGlyph.AddThemeColorOverride("font_color", tint);
        _fxLeftGlyph.AddThemeColorOverride("font_color", tint);
        _fxRightGlyph.AddThemeColorOverride("font_color", tint);

        // Every active debuff contributes its own motion cue, allowing multiple types to
        // remain visible at once instead of replacing the previous effect.
        _fxGlyph.Rotation = dizziness > 0 ? _elapsed * 1.8f : confusion > 0 ? Mathf.Sin(_elapsed * 11f) * 0.08f : 0f;
        _fxGlyph.Scale = Vector2.One * (0.92f + pulse * (anxiety > 0 ? 0.24f : 0.08f));
        _fxLeftGlyph.Rotation = dizziness > 0 ? -_elapsed * 1.4f : confusion > 0 ? Mathf.Sin(_elapsed * 17f) * 0.12f : 0f;
        _fxRightGlyph.Rotation = dizziness > 0 ? _elapsed * 1.2f : confusion > 0 ? Mathf.Cos(_elapsed * 15f) * 0.12f : 0f;
        _fxLeftGlyph.Position = confusion > 0
            ? new Vector2(-235f + Mathf.Sin(_elapsed * 19f) * 12f, -30f)
            : new Vector2(-235f + (dizziness > 0 ? Mathf.Sin(_elapsed * 3f) * 24f : 0f), -30f);
        _fxRightGlyph.Position = confusion > 0
            ? new Vector2(145f + Mathf.Cos(_elapsed * 17f) * 14f, -30f)
            : new Vector2(145f + (dizziness > 0 ? Mathf.Cos(_elapsed * 2.7f) * 24f : 0f), -30f);

        float pressureProgress = Mathf.Clamp(pressure / 100f, 0f, 1f);
        float barSize = pressure > 0 ? 8f + pressureProgress * 24f + pulse * 8f : 0f;
        _pressureTop.OffsetBottom = barSize;
        _pressureBottom.OffsetTop = -barSize;
        float pressureAlpha = pressureProgress * (0.12f + pulse * 0.14f);
        _pressureTop.Color = new Color(DebuffColor(SanityManager.DebuffType.Pressure).R,
            DebuffColor(SanityManager.DebuffType.Pressure).G, DebuffColor(SanityManager.DebuffType.Pressure).B, pressureAlpha);
        _pressureBottom.Color = _pressureTop.Color;
    }

    private Color GetStackTint(int dizziness, int confusion, int anxiety, int pressure)
    {
        float weight = dizziness + confusion + anxiety + pressure;
        if (weight <= 0f)
            return _sanity < SanityManager.StableSanityThreshold ? new Color("d9233e") : DebuffColor(_dominantDebuff);

        Color d = DebuffColor(SanityManager.DebuffType.Dizziness);
        Color c = DebuffColor(SanityManager.DebuffType.Confusion);
        Color a = DebuffColor(SanityManager.DebuffType.Anxiety);
        Color p = DebuffColor(SanityManager.DebuffType.Pressure);
        return new Color(
            (d.R * dizziness + c.R * confusion + a.R * anxiety + p.R * pressure) / weight,
            (d.G * dizziness + c.G * confusion + a.G * anxiety + p.G * pressure) / weight,
            (d.B * dizziness + c.B * confusion + a.B * anxiety + p.B * pressure) / weight);
    }

    private void RefreshEffects()
    {
        if (_effectWash == null)
            return;

        var manager = SanityManager.Instance;
        int dizziness = manager?.GetDebuffLevel(SanityManager.DebuffType.Dizziness) ?? 0;
        int confusion = manager?.GetDebuffLevel(SanityManager.DebuffType.Confusion) ?? 0;
        int anxiety = manager?.GetDebuffLevel(SanityManager.DebuffType.Anxiety) ?? 0;
        int pressure = manager?.GetDebuffLevel(SanityManager.DebuffType.Pressure) ?? 0;
        int total = dizziness + confusion + anxiety + pressure;

        float lowSanity = Mathf.Clamp((SanityManager.StableSanityThreshold - _sanity) /
            (float)SanityManager.StableSanityThreshold, 0f, 1f);
        float pulse = (Mathf.Sin(_elapsed * 2.3f) + 1f) * 0.5f;
        int activeTypes = (dizziness > 0 ? 1 : 0) + (confusion > 0 ? 1 : 0) +
                          (anxiety > 0 ? 1 : 0) + (pressure > 0 ? 1 : 0);
        float stackAlpha = Mathf.Clamp(activeTypes * 0.025f + total / 400f, 0f, 0.16f);
        float baseAlpha = Mathf.Clamp(lowSanity * 0.13f + stackAlpha, 0f, 0.20f);

        Color tint = GetStackTint(dizziness, confusion, anxiety, pressure);
        if (_flashTime > 0f)
            tint = tint.Lerp(_flashColor, 0.62f);
        if (_sanity < 25f)
            tint = tint.Lerp(new Color("d9233e"), 0.35f);

        if (_flashTime > 0f)
            baseAlpha = Mathf.Max(baseAlpha, (_flashTime / 1.25f) * 0.17f);

        _effectWash.Color = new Color(tint.R, tint.G, tint.B,
            Mathf.Clamp(baseAlpha + pulse * lowSanity * 0.018f, 0f, 0.24f));

        float edgeAlpha = Mathf.Clamp(baseAlpha * 0.75f + (_flashTime > 0f ? 0.10f : 0f) +
            (anxiety > 0 ? 0.07f + pulse * 0.06f : 0f), 0f, 0.30f);
        Color edge = new Color(tint.R, tint.G, tint.B, edgeAlpha);
        _topEdge.Color = edge;
        _bottomEdge.Color = edge;
        _leftEdge.Color = edge;
        _rightEdge.Color = edge;

        bool active = total > 0 || _sanity < SanityManager.StableSanityThreshold;
        _statusLabel.Visible = active;
        if (active)
        {
            string low = _sanity < 25 ? "CRITICAL SANITY" :
                         _sanity < 50 ? "LOW SANITY" :
                         _sanity < SanityManager.StableSanityThreshold ? "SANITY UNSTEADY" : "DEBUFFS ACTIVE";
            var statusParts = new List<string>();
            if (dizziness > 0) statusParts.Add($"DIZZINESS {dizziness}%");
            if (confusion > 0) statusParts.Add($"CONFUSION {confusion}%");
            if (anxiety > 0) statusParts.Add($"ANXIETY {anxiety}%");
            if (pressure > 0) statusParts.Add($"PRESSURE {pressure}%");
            _statusLabel.Text = statusParts.Count > 0
                ? $"{low}  •  {string.Join("  •  ", statusParts)}"
                : low;
            _statusLabel.Modulate = Colors.White;
        }

        UpdateDebuffCue();
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
        AddChild(rect);
        return rect;
    }

    private void OnSanityChanged(int current)
    {
        _sanity = current;
        RefreshEffects();
    }

    private void OnDebuffChanged(SanityManager.DebuffType type, int intensity)
    {
        // Decay also raises this event. Only use it to refresh the current stack display;
        // the temporary "applied" notice is shown by TriggerTaskFailure, not every decay tick.
        int strongestLevel = 0;
        foreach (SanityManager.DebuffType candidate in Enum.GetValues(typeof(SanityManager.DebuffType)))
        {
            int level = SanityManager.Instance?.GetDebuffLevel(candidate) ?? 0;
            if (level > strongestLevel)
            {
                strongestLevel = level;
                _dominantDebuff = candidate;
            }
        }

        _dominantLevel = strongestLevel;
        if (intensity > 0)
            _flashColor = DebuffColor(type);

        UpdateDebuffCue();
        RefreshEffects();
    }

    private void ApplyBrightness()
    {
        if (_brightnessWash == null)
            return;

        float brightness = SettingsManager.Instance?.Brightness ?? 0.88f;
        if (brightness < 1f)
            _brightnessWash.Color = new Color(0f, 0f, 0f, (1f - brightness) * 0.62f);
        else
            _brightnessWash.Color = new Color(1f, 1f, 1f, (brightness - 1f) * 0.18f);
    }

    private void RefreshEffects()
    {
        if (_effectWash == null)
            return;

        float lowSanity = Mathf.Clamp((70f - _sanity) / 70f, 0f, 1f);
        float debuff = Mathf.Clamp(_dominantLevel / 100f, 0f, 1f);
        float pulse = (Mathf.Sin(_elapsed * 2.3f) + 1f) * 0.5f;
        float baseAlpha = lowSanity * 0.13f + (_dominantLevel > 0 ? 0.025f + debuff * 0.10f : 0f);

        Color tint = DebuffColor(_dominantDebuff);
        if (_flashTime > 0f)
            tint = tint.Lerp(_flashColor, 0.62f);
        if (_sanity < 25f)
            tint = tint.Lerp(new Color("d9233e"), 0.35f);

        if (_flashTime > 0f)
            baseAlpha = Mathf.Max(baseAlpha, (_flashTime / 1.25f) * 0.17f);

        _effectWash.Color = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(baseAlpha + pulse * lowSanity * 0.018f, 0f, 0.22f));
        Color edge = new Color(tint.R, tint.G, tint.B, Mathf.Clamp(baseAlpha * 0.75f + (_flashTime > 0f ? 0.10f : 0f), 0f, 0.25f));
        _topEdge.Color = edge;
        _bottomEdge.Color = edge;
        _leftEdge.Color = edge;
        _rightEdge.Color = edge;

        bool active = _dominantLevel > 0 || _sanity < 70f;
        _statusLabel.Visible = active;
        if (active)
        {
            string low = _sanity < 25 ? "CRITICAL SANITY" : _sanity < 50 ? "LOW SANITY" : _sanity < 70 ? "SANITY UNSTEADY" : "DEBUFF ACTIVE";
            _statusLabel.Text = $"{low}   •   {_dominantDebuff.ToString().ToUpperInvariant()} {_dominantLevel}%";
            _statusLabel.Modulate = Colors.White;
        }
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
