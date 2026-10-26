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

    private float _debuffFxTime;
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
        _debuffFxTime = Mathf.Max(0f, _debuffFxTime - dt);

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
        _debuffFxTime = Mathf.Max(_debuffFxTime, 3.4f);
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
            Size = new Vector2(360, 28),
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
        int statusIndex = GetIndex(_statusLabel);
        MoveChild(_pressureTop, statusIndex);
        MoveChild(_pressureBottom, statusIndex);
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

        bool visible = _debuffFxTime > 0f;
        float progress = Mathf.Clamp(_debuffFxTime / 3.4f, 0f, 1f);
        float pulse = (Mathf.Sin(_elapsed * 7f) + 1f) * 0.5f;
        _fxGlyph.Visible = visible;
        _fxLeftGlyph.Visible = visible && _dominantDebuff != SanityManager.DebuffType.Pressure;
        _fxRightGlyph.Visible = visible && _dominantDebuff != SanityManager.DebuffType.Pressure;
        _pressureTop.Visible = visible && _dominantDebuff == SanityManager.DebuffType.Pressure;
        _pressureBottom.Visible = _pressureTop.Visible;

        Color tint = DebuffColor(_dominantDebuff);
        _fxGlyph.Modulate = new Color(1f, 1f, 1f, progress);
        _fxGlyph.AddThemeColorOverride("font_color", tint);
        _fxLeftGlyph.AddThemeColorOverride("font_color", tint);
        _fxRightGlyph.AddThemeColorOverride("font_color", tint);

        switch (_dominantDebuff)
        {
            case SanityManager.DebuffType.Dizziness:
                _fxGlyph.Text = "◎";
                _fxLeftGlyph.Text = "◌";
                _fxRightGlyph.Text = "◌";
                _fxGlyph.Rotation = _elapsed * 1.8f;
                _fxLeftGlyph.Rotation = -_elapsed * 1.4f;
                _fxRightGlyph.Rotation = _elapsed * 1.2f;
                _fxGlyph.Scale = Vector2.One * (0.86f + pulse * 0.34f);
                _fxLeftGlyph.Position = new Vector2(-235f + Mathf.Sin(_elapsed * 3f) * 24f, -30f);
                _fxRightGlyph.Position = new Vector2(145f + Mathf.Cos(_elapsed * 2.7f) * 24f, -30f);
                break;

            case SanityManager.DebuffType.Confusion:
                _fxGlyph.Text = "?!";
                _fxLeftGlyph.Text = "?";
                _fxRightGlyph.Text = "??";
                _fxGlyph.Rotation = Mathf.Sin(_elapsed * 11f) * 0.08f;
                _fxLeftGlyph.Rotation = -0.12f;
                _fxRightGlyph.Rotation = 0.10f;
                _fxGlyph.Scale = Vector2.One * (0.9f + pulse * 0.22f);
                _fxLeftGlyph.Position = new Vector2(-245f + Mathf.Sin(_elapsed * 19f) * 12f, -28f);
                _fxRightGlyph.Position = new Vector2(155f + Mathf.Cos(_elapsed * 17f) * 14f, -28f);
                break;

            case SanityManager.DebuffType.Anxiety:
                _fxGlyph.Text = "!";
                _fxLeftGlyph.Text = "!";
                _fxRightGlyph.Text = "!";
                _fxGlyph.Rotation = Mathf.Sin(_elapsed * 14f) * 0.04f;
                _fxGlyph.Scale = Vector2.One * (0.9f + pulse * 0.28f);
                _fxLeftGlyph.Position = new Vector2(-265f, -30f);
                _fxRightGlyph.Position = new Vector2(175f, -30f);
                _topEdge.Color = new Color(tint.R, tint.G, tint.B, Mathf.Max(_topEdge.Color.A, 0.10f + pulse * 0.16f));
                _bottomEdge.Color = new Color(tint.R, tint.G, tint.B, Mathf.Max(_bottomEdge.Color.A, 0.10f + pulse * 0.16f));
                break;

            case SanityManager.DebuffType.Pressure:
                _fxGlyph.Text = "PRESSURE";
                _fxGlyph.Rotation = 0f;
                _fxGlyph.Scale = Vector2.One * (0.96f + pulse * 0.06f);
                float barSize = 18f + (1f - progress) * 24f + pulse * 8f;
                _pressureTop.OffsetBottom = barSize;
                _pressureBottom.OffsetTop = -barSize;
                float alpha = progress * (0.16f + pulse * 0.12f);
                _pressureTop.Color = new Color(tint.R, tint.G, tint.B, alpha);
                _pressureBottom.Color = new Color(tint.R, tint.G, tint.B, alpha);
                break;
        }
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
        _dominantDebuff = type;
        _dominantLevel = intensity;
        _flashColor = DebuffColor(type);
        _flashTime = 1.15f;
        _debuffFxTime = 3.4f;
        string displayName = type.ToString().ToUpperInvariant();
        Notify($"DEBUFF APPLIED: {displayName}");
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
