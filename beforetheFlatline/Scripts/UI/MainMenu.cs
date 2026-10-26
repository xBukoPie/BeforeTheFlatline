using Godot;
using SchoolYardGame;
using System.Threading.Tasks;

public partial class MainMenu : Control
{
    [Export] public float FadeTime { get; set; } = 0.25f;
    private ColorRect _fade;
    private bool _isStarting = false;

    public override void _Ready()
    {
        // Keep the in-game clock paused while the player is on the main menu.
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;
        var algorithmLabBtn = GetNodeOrNull<Button>("algorithmLabButton");
        _fade = GetNode<ColorRect>("UI/Fade");

        // Play, Settings, and Support are already connected in Main.tscn.
        // Do not connect them twice. Algorithm Lab is a code-only optional button.
        if (algorithmLabBtn != null && !algorithmLabBtn.IsConnected(Button.SignalName.Pressed, new Callable(this, nameof(OnAlgorithmLabButtonPressed))))
            algorithmLabBtn.Pressed += OnAlgorithmLabButtonPressed;
    }

    private async Task FadeScreen(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, FadeTime);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async void OnPlayButtonPressed()
    {
        if (_isStarting) return;
        _isStarting = true;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = true;
        await FadeScreen(1f);

        if (GetTree() != null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().ChangeSceneToFile("res://Scenes/Home/Rooms/World.tscn");
        }
    }

    private void OnAlgorithmLabButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Challenges/ChallengeHub.tscn");
    }

    private void OnSettingsButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
    }

    private void OnSupportButtonPressed()
    {
        var dialog = new AcceptDialog
        {
            Title = "Support & Controls",
            DialogText = "BEFORE THE FLATLINE\n\nMove: WASD or arrow keys\nInteract: E\nAdvance time outside school: T\n\nUse Settings to adjust master volume, brightness, and fullscreen."
        };
        dialog.CustomMinimumSize = new Vector2(480, 260);
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.CloseRequested += () => dialog.QueueFree();

        dialog.PopupCentered();
    }
}