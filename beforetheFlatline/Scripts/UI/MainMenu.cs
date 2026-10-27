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
        _fade = GetNode<ColorRect>("UI/Fade");
        // Play, Settings, and Support are already connected in Main.tscn.
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
        // Starting from the title after a bad ending must create a clean run; otherwise
        // sanity would remain at zero and the bad ending would immediately trigger again.
        DayClock.Instance?.ResetForNewRun();
        SanityManager.Instance?.ResetForNewRun();
        StatusEffectsOverlay.Instance?.ResetForNewRun();
        DailyTestSession.Reset();
        ChallengeProgress.Reset();

        // Keep the clock stopped during loading. RoomManager starts it only once the
        // world scene has initialized, so the player does not lose in-game time on the loader.
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadSceneWithLoadingScreen("res://Scenes/Home/Rooms/World.tscn");
        }
        else
        {
            await FadeScreen(1f);
            if (GetTree() != null)
            {
                if (DayClock.Instance != null)
                    DayClock.Instance.Running = true;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GetTree().ChangeSceneToFile("res://Scenes/Home/Rooms/World.tscn");
            }
        }
    }

    private void OnSettingsButtonPressed()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
        else
            GetTree().ChangeSceneToFile("res://Scenes/Menu/SettingsMenu.tscn");
    }

    private void OnSupportButtonPressed()
    {
        var dialog = new AcceptDialog
        {
            Title = "Support & Controls",
            DialogText = "BEFORE THE FLATLINE\n\nMove: WASD or arrow keys\nInteract: E\nAdvance time outside school: T\n\nUse Settings to adjust master volume, brightness, and fullscreen."
        };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.Canceled += () => dialog.QueueFree();

        dialog.PopupCentered();
    }
}