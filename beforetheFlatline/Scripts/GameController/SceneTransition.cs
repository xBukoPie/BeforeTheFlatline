using Godot;

/// <summary>
/// Autoloaded cross-scene fade. It survives scene replacement and keeps the transition layer
/// above gameplay, dialogue and ending scenes.
/// </summary>
public partial class SceneTransition : CanvasLayer
{
    public static SceneTransition Instance { get; private set; }

    [Export] public float DefaultFadeDuration { get; set; } = 0.38f;

    private ColorRect _fade;
    private bool _busy;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        Layer = 127;
        ProcessMode = ProcessModeEnum.Always;
        _fade = new ColorRect
        {
            Name = "SceneFade",
            Color = Colors.Black,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fade);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public async void ChangeSceneToFile(string path)
    {
        if (_busy || string.IsNullOrWhiteSpace(path))
            return;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushError($"SceneTransition: scene was not found at '{path}'.");
            return;
        }

        _busy = true;
        _fade.Visible = true;
        _fade.MouseFilter = Control.MouseFilterEnum.Stop;
        await FadeTo(1f, DefaultFadeDuration);

        Error result = GetTree().ChangeSceneToFile(path);
        if (result != Error.Ok)
        {
            GD.PushError($"SceneTransition: failed to change to '{path}' ({result}).");
            await FadeTo(0f, DefaultFadeDuration);
            _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
            _fade.Visible = false;
            _busy = false;
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await FadeTo(0f, DefaultFadeDuration);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.Visible = false;
        _busy = false;
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha, float duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
