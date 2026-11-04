using Godot;

public partial class OpeningSequence : Control
{
    private const string LogoScenePath = "res://Scenes/Menu/OpeningLogo.tscn";
    private const string PresentsScenePath = "res://Scenes/Menu/OpeningPresents.tscn";
    private const string MainMenuPath = "res://Scenes/Menu/Main.tscn";

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var black = new ColorRect
        {
            Name = "PersistentBlackBackground",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(black);

        await PlayLayer(LogoScenePath, 1.0f, 1.9f, 0.9f);
        if (!IsInsideTree())
            return;

        await PlayLayer(PresentsScenePath, 0.85f, 1.75f, 0.8f);
        if (!IsInsideTree())
            return;

        // Keep the same black scene behind the title until the main-menu transition finishes.
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(MainMenuPath);
        else
            GetTree().ChangeSceneToFile(MainMenuPath);
    }

    private async System.Threading.Tasks.Task PlayLayer(
        string scenePath,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        PackedScene scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PushError($"OpeningSequence: could not load '{scenePath}'.");
            return;
        }

        Control layer = scene.Instantiate<Control>();
        layer.ProcessMode = ProcessModeEnum.Always;
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.Modulate = new Color(1f, 1f, 1f, 0f);
        AddChild(layer);

        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(layer, "modulate:a", 1f, fadeIn)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenInterval(hold);
        tween.TweenProperty(layer, "modulate:a", 0f, fadeOut)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        if (GodotObject.IsInstanceValid(layer))
            layer.QueueFree();
    }
}
