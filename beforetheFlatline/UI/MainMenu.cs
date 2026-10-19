using Godot;

public partial class MainMenu : Control
{
    // Make sure your TestLevel path exactly matches where you saved it earlier
    public void OnPlayButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
    }

    public void OnSettingsButtonPressed()
    {
        GD.Print("Settings opened."); // Placeholder for later
    }

    public void OnSupportButtonPressed()
    {
        GD.Print("Support opened."); // Placeholder for later
    }
}