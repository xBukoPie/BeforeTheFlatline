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
        // Ensure buttons are connected (they are via scene file, but safety check)
        var playBtn = GetNode<Button>("playButton");
        var settingsBtn = GetNode<Button>("settingsButton");
        var supportBtn = GetNode<Button>("supportButton");
        var algorithmLabBtn = GetNodeOrNull<Button>("algorithmLabButton");
        _fade = GetNode<ColorRect>("UI/Fade");

        playBtn.Pressed += OnPlayButtonPressed;
        settingsBtn.Pressed += OnSettingsButtonPressed;
        supportBtn.Pressed += OnSupportButtonPressed;
        if (algorithmLabBtn != null)
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
        GD.Print("Settings pressed - not implemented yet");
    }

    private void OnSupportButtonPressed()
    {
        GD.Print("Support pressed - not implemented yet");
    }
}