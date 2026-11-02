using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Adds hover, keyboard-focus, and press feedback to buttons without changing
/// the layout of menu controls that already use a custom scale.
/// </summary>
public partial class UIFeedback : Node
{
    private const float HoverScale = 1.035f;
    private const float PressScale = 0.94f;
    private const float ReleaseOvershootScale = 1.045f;

    private static readonly Color HoverTint = new(1.16f, 1.12f, 1.06f, 1f);
    private static readonly Color PressTint = new(0.78f, 0.82f, 0.9f, 1f);
    private static readonly Color ReleaseTint = new(1.08f, 1.05f, 1f, 1f);

    private readonly HashSet<ulong> _tracked = new();
    private readonly HashSet<ulong> _trackedSliders = new();
    private readonly HashSet<ulong> _hovered = new();
    private readonly HashSet<ulong> _scaleFeedback = new();
    private readonly Dictionary<ulong, Vector2> _baseScales = new();
    private readonly Dictionary<ulong, Color> _baseModulates = new();
    private readonly Dictionary<ulong, Tween> _tweens = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GetTree().NodeAdded += OnNodeAdded;
        CallDeferred(MethodName.ScanExistingTree);
    }

    public override void _ExitTree()
    {
        if (GetTree() != null)
            GetTree().NodeAdded -= OnNodeAdded;

        foreach (Tween tween in _tweens.Values)
        {
            if (tween != null && tween.IsValid())
                tween.Kill();
        }

        _tweens.Clear();
    }

    private void ScanExistingTree()
    {
        if (IsInsideTree())
            ScanNode(GetTree().Root);
    }

    private void ScanNode(Node node)
    {
        if (node is Control control)
            AttachControl(control);

        foreach (Node child in node.GetChildren())
            ScanNode(child);
    }

    private void OnNodeAdded(Node node)
    {
        if (node is Control control)
            AttachControl(control);
    }

    private void AttachControl(Control control)
    {
        ulong id = control.GetInstanceId();

        // Sliders are not BaseButtons, so wire their drag signal before the button-only path.
        if (control is Slider slider && _trackedSliders.Add(id))
        {
            slider.DragStarted += () => AudioManager.Instance?.PlayUiDrag();
            control.TreeExiting += () => _trackedSliders.Remove(id);
        }

        if (control is not BaseButton button || !_tracked.Add(id))
            return;

        _baseScales[id] = control.Scale;
        _baseModulates[id] = control.Modulate;

        // Controls with a custom scale (notably the image-based Main Menu buttons)
        // must keep their original top-left pivot. Centering their pivot shifts their
        // child sprites and makes the entire menu appear to move.
        bool hasUnitScale = Mathf.IsEqualApprox(control.Scale.X, 1f) &&
                            Mathf.IsEqualApprox(control.Scale.Y, 1f);
        if (hasUnitScale)
        {
            _scaleFeedback.Add(id);
            control.PivotOffset = control.Size * 0.5f;
            control.Resized += () =>
            {
                if (GodotObject.IsInstanceValid(control))
                    control.PivotOffset = control.Size * 0.5f;
            };
        }

        control.MouseEntered += () => SetHovered(control, true);
        control.MouseExited += () => SetHovered(control, false);
        control.FocusEntered += () => SetHovered(control, true);
        control.FocusExited += () => SetHovered(control, false);

        button.Pressed += () => AudioManager.Instance?.PlayUiClick();

        button.ButtonDown += () =>
        {
            if (button.Disabled)
                return;

            if (_scaleFeedback.Contains(id))
                AnimateScale(control, GetBaseScale(control) * PressScale, 0.065);
            else
                AnimateModulate(control, GetBaseModulate(control) * PressTint, 0.065);
        };

        button.ButtonUp += () =>
        {
            if (button.Disabled)
                return;

            if (_scaleFeedback.Contains(id))
            {
                AnimateScale(
                    control,
                    GetBaseScale(control) * ReleaseOvershootScale,
                    0.075,
                    () => AnimateScale(
                        control,
                        GetBaseScale(control) * (_hovered.Contains(id) ? HoverScale : 1f),
                        0.12));
            }
            else
            {
                AnimateModulate(
                    control,
                    GetBaseModulate(control) * ReleaseTint,
                    0.075,
                    () => AnimateModulate(
                        control,
                        GetBaseModulate(control) * (_hovered.Contains(id) ? HoverTint : Colors.White),
                        0.12));
            }
        };

        control.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        control.TreeExiting += () => ForgetControl(id);
    }

    private void SetHovered(Control control, bool hovered)
    {
        if (!GodotObject.IsInstanceValid(control) || control is BaseButton { Disabled: true })
            return;

        ulong id = control.GetInstanceId();
        if (hovered)
            _hovered.Add(id);
        else
            _hovered.Remove(id);

        if (_scaleFeedback.Contains(id))
        {
            AnimateScale(
                control,
                GetBaseScale(control) * (hovered ? HoverScale : 1f),
                hovered ? 0.13 : 0.16);
        }
        else
        {
            AnimateModulate(
                control,
                GetBaseModulate(control) * (hovered ? HoverTint : Colors.White),
                hovered ? 0.13 : 0.16);
        }
    }

    private Vector2 GetBaseScale(Control control)
    {
        ulong id = control.GetInstanceId();
        return _baseScales.TryGetValue(id, out Vector2 scale) ? scale : control.Scale;
    }

    private Color GetBaseModulate(Control control)
    {
        ulong id = control.GetInstanceId();
        return _baseModulates.TryGetValue(id, out Color color) ? color : control.Modulate;
    }

    private void AnimateScale(Control control, Vector2 target, double duration, Action onFinished = null)
    {
        AnimateProperty(control, "scale", target, duration, onFinished);
    }

    private void AnimateModulate(Control control, Color target, double duration, Action onFinished = null)
    {
        AnimateProperty(control, "modulate", target, duration, onFinished);
    }

    private void AnimateProperty(Control control, NodePath property, Variant target, double duration, Action onFinished)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree())
            return;

        ulong id = control.GetInstanceId();
        if (_tweens.TryGetValue(id, out Tween previous) && previous != null && previous.IsValid())
            previous.Kill();

        Tween tween = CreateTween();
        tween.TweenProperty(control, property, target, duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        if (onFinished != null)
            tween.Finished += onFinished;

        _tweens[id] = tween;
    }

    private void ForgetControl(ulong id)
    {
        _tracked.Remove(id);
        _hovered.Remove(id);
        _scaleFeedback.Remove(id);
        _baseScales.Remove(id);
        _baseModulates.Remove(id);

        if (_tweens.TryGetValue(id, out Tween tween) && tween != null && tween.IsValid())
            tween.Kill();

        _tweens.Remove(id);
    }
}
