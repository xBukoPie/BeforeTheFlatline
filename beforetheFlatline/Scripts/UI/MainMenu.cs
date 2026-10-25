using Godot;
using System.Threading.Tasks;

public partial class MainMenu : Control
{
    [Export] public float FadeTime { get; set; } = 0.25f;
    private ColorRect _fade;

    public override void _Ready()
    {
        // Ensure buttons are connected (they are via scene file, but safety check)
        var playBtn = GetNode<Button>("playButton");
        var settingsBtn = GetNode<Button>("settingsButton");
        var supportBtn = GetNode<Button>("supportButton");
        _fade = GetNode<ColorRect>("UI/Fade");

        playBtn.Pressed += OnPlayButtonPressed;
        settingsBtn.Pressed += OnSettingsButtonPressed;
        supportBtn.Pressed += OnSupportButtonPressed;
    }

    private async Task FadeScreen(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, FadeTime);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async void OnPlayButtonPressed()
    {
        await FadeScreen(1f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        GetTree().ChangeSceneToFile("res://Scenes/Home/Rooms/World.tscn");
        await FadeScreen(0f);
        await FadeScreen(1f); 
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