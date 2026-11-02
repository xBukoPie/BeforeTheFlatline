using Godot;
using System;
using System.Collections.Generic;
using SchoolYardGame;

public sealed record StoryCutsceneSlide(string ImagePath, string Text);

public partial class StoryCutsceneOverlay : Control
{
    private string _heading = "MEMORY";
    private IReadOnlyList<StoryCutsceneSlide> _slides = Array.Empty<StoryCutsceneSlide>();
    private Action _onFinished;
    private bool _isEnding;
    private int _slideIndex;
    private bool _transitioning;
    private bool _finished;
    private bool _previousTreePaused;
    private bool _previousClockRunning;

    private TextureRect _art;
    private Label _headingLabel;
    private Label _bodyLabel;
    private Label _counterLabel;
    private Button _continueButton;

    public void Configure(
        string heading,
        IReadOnlyList<StoryCutsceneSlide> slides,
        Action onFinished,
        bool isEnding = false)
    {
        _heading = string.IsNullOrWhiteSpace(heading) ? "MEMORY" : heading;
        _slides = slides ?? Array.Empty<StoryCutsceneSlide>();
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
        GetTree().Paused = true;

        BuildUi();

        if (_slides.Count == 0)
        {
            _bodyLabel.Text = "This memory is unavailable.";
            _continueButton.Text = "Close";
        }
        else
        {
            ShowSlide(0);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_transitioning || _finished)
            return;

        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            Advance();
        }
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Name = "BlackBackground",
            Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _art = new TextureRect
        {
            Name = "CutsceneImage",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_art);

        var shade = new ColorRect
        {
            Name = "TextShade",
            Color = new Color(0f, 0f, 0f, 0.78f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        shade.AnchorLeft = 0f;
        shade.AnchorRight = 1f;
        shade.AnchorTop = 0.66f;
        shade.AnchorBottom = 1f;
        shade.OffsetTop = 0f;
        shade.OffsetBottom = 0f;
        AddChild(shade);

        var margin = new MarginContainer
        {
            Name = "TextMargin"
        };
        margin.AnchorLeft = 0f;
        margin.AnchorRight = 1f;
        margin.AnchorTop = 0.66f;
        margin.AnchorBottom = 1f;
        margin.OffsetLeft = 42f;
        margin.OffsetRight = -42f;
        margin.OffsetTop = 14f;
        margin.OffsetBottom = -20f;
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);

        _headingLabel = new Label
        {
            Text = _heading.ToUpperInvariant(),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _headingLabel.AddThemeFontSizeOverride("font_size", 15);
        _headingLabel.AddThemeColorOverride("font_color", new Color("f4d58d"));
        stack.AddChild(_headingLabel);

        _bodyLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _bodyLabel.AddThemeFontSizeOverride("font_size", 23);
        _bodyLabel.AddThemeColorOverride("font_color", Colors.White);
        _bodyLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bodyLabel.AddThemeConstantOverride("outline_size", 2);
        stack.AddChild(_bodyLabel);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 14);
        stack.AddChild(footer);

        _counterLabel = new Label
        {
            Text = "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _counterLabel.AddThemeFontSizeOverride("font_size", 13);
        _counterLabel.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        footer.AddChild(_counterLabel);

        _continueButton = new Button
        {
            Text = "Continue",
            CustomMinimumSize = new Vector2(190, 44),
            FocusMode = FocusModeEnum.All
        };
        _continueButton.Pressed += Advance;
        footer.AddChild(_continueButton);
        _continueButton.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void ShowSlide(int index)
    {
        _slideIndex = Mathf.Clamp(index, 0, Math.Max(0, _slides.Count - 1));
        StoryCutsceneSlide slide = _slides[_slideIndex];

        Texture2D texture = GD.Load<Texture2D>(slide.ImagePath);
        if (texture == null)
            GD.PushWarning($"StoryCutsceneOverlay: could not load image '{slide.ImagePath}'.");

        _art.Texture = texture;
        _bodyLabel.Text = slide.Text;
        _counterLabel.Text = _slides.Count > 1 ? $"{_slideIndex + 1} / {_slides.Count}" : "";
        bool lastSlide = _slideIndex >= _slides.Count - 1;
        _continueButton.Text = lastSlide ? (_isEnding ? "Return to Title" : "Close Memory") : "Continue";
    }

    private async void Advance()
    {
        if (_transitioning || _finished)
            return;

        if (_slides.Count == 0 || _slideIndex >= _slides.Count - 1)
        {
            await Finish();
            return;
        }

        _transitioning = true;
        await FadeTo(0f, 0.22f);
        if (!IsInsideTree())
            return;

        ShowSlide(_slideIndex + 1);
        await FadeTo(1f, 0.32f);
        _transitioning = false;
    }

    private async System.Threading.Tasks.Task Finish()
    {
        if (_finished)
            return;

        _finished = true;
        _continueButton.Disabled = true;
        await FadeTo(0f, 0.5f);

        if (!IsInsideTree())
            return;

        GetTree().Paused = _previousTreePaused;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = _previousClockRunning;

        Action callback = _onFinished;
        _onFinished = null;
        QueueFree();
        callback?.Invoke();
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha, double duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
