using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

/// <summary>
/// Adds subtle, consistent mouse and keyboard focus feedback to every BaseButton,
/// including buttons created dynamically after a scene has loaded.
/// </summary>
public partial class UIFeedback : Node
{
    private const float HoverScale = 1.035f;
    private const float PressScale = 0.94f;
    private const float ReleaseOvershootScale = 1.045f;

    private readonly HashSet<ulong> _tracked = new();
    private readonly HashSet<ulong> _hovered = new();
    private readonly Dictionary<ulong, Vector2> _baseScales = new();
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
        if (!IsInsideTree())
            return;

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
        if (control is not BaseButton button)
            return;

        ulong id = control.GetInstanceId();
        if (!_tracked.Add(id))
            return;

        _baseScales[id] = control.Scale;
        control.PivotOffset = control.Size * 0.5f;
        control.Resized += () =>
        {
            if (GodotObject.IsInstanceValid(control))
                control.PivotOffset = control.Size * 0.5f;
        };

        control.MouseEntered += () => SetHovered(control, true);
        control.MouseExited += () => SetHovered(control, false);
        control.FocusEntered += () => SetHovered(control, true);
        control.FocusExited += () => SetHovered(control, false);

        button.ButtonDown += () =>
        {
            if (!button.Disabled)
                AnimateScale(control, GetBaseScale(control) * PressScale, 0.065);
        };

        button.ButtonUp += () =>
        {
            if (button.Disabled)
                return;

            AnimateScale(
                control,
                GetBaseScale(control) * ReleaseOvershootScale,
                0.075,
                () => AnimateScale(
                    control,
                    GetBaseScale(control) * (_hovered.Contains(id) ? HoverScale : 1f),
                    0.12));
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

        AnimateScale(
            control,
            GetBaseScale(control) * (hovered ? HoverScale : 1f),
            hovered ? 0.13 : 0.16);
    }

    private Vector2 GetBaseScale(Control control)
    {
        ulong id = control.GetInstanceId();
        return _baseScales.TryGetValue(id, out Vector2 scale) ? scale : control.Scale;
    }

    private void AnimateScale(Control control, Vector2 target, double duration, Action onFinished = null)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree())
            return;

        ulong id = control.GetInstanceId();
        if (_tweens.TryGetValue(id, out Tween previous) && previous != null && previous.IsValid())
            previous.Kill();

        Tween tween = CreateTween();
        tween.TweenProperty(control, "scale", target, duration)
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
        _baseScales.Remove(id);

        if (_tweens.TryGetValue(id, out Tween tween) && tween != null && tween.IsValid())
            tween.Kill();

        _tweens.Remove(id);
    }
}
