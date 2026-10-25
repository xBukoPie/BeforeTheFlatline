using Godot;

namespace SchoolYardGame;

/// <summary>Scene root: wires interaction zones to the HUD and handles what each zone does.</summary>
public partial class SchoolYard : Node2D
{
    private Label _prompt;
    private Tween _msgTween;

    public override void _Ready()
    {
        _prompt = GetNode<Label>("HUD/Prompt");

        foreach (var child in GetNode("SchoolYard/Interactions").GetChildren())
        {
            if (child is InteractionZone z)
            {
                z.PromptChanged += OnPromptChanged;
                z.Interacted += OnInteracted;
            }
        }

        // Configure the Camera2D limits just like in Home rooms
        var player = GetNode<Node2D>("../Player");
        var cam = player.GetNode<Camera2D>("Camera2D");
        var ground = GetNode<Sprite2D>("Ground");
        if (ground != null && cam != null)
        {
            var size = ground.RegionEnabled ? ground.RegionRect.Size : ground.Texture.GetSize();
            var bounds = new Rect2(ground.GlobalPosition - size / 2f, size);
            // Use the same zoom as in RoomManager (defaults to 2.5)
            float zoom = 2.5f;
            Vector2 view = GetViewportRect().Size / zoom;
            // Use the larger of the view components for both dimensions to stay within bounds
            float max = Mathf.Max(view.X, view.Y);
            Vector2 limitSize = new Vector2(max, max);
            Rect2 limits = new Rect2(bounds.GetCenter() - limitSize / 2f, limitSize);
            cam.Zoom = new Vector2(zoom, zoom);
            cam.LimitLeft = Mathf.RoundToInt(limits.Position.X);
            cam.LimitTop = Mathf.RoundToInt(limits.Position.Y);
            cam.LimitRight = Mathf.RoundToInt(limits.End.X);
            cam.LimitBottom = Mathf.RoundToInt(limits.End.Y);
            cam.ResetSmoothing();
        }
    }

    private void OnPromptChanged(string text, bool show)
    {
        _msgTween?.Kill();
        _prompt.Text = text;
        _prompt.Visible = show;
    }

    private void OnInteracted(string id)
    {
        // Replace these with scene changes, dialogue, minigames, etc.
        switch (id)
        {
            case "clinic":          Say("The clinic is closed. Come back during the day."); break;
            case "tindahan":        Say("Manang: \"Ano po bibilhin n'yo?\"");               break;
            case "school_entrance": Say("The classrooms are on the second floor.");        break;
            case "flag":            Say("You stand at attention for the flag.");           break;
            case "basketball":      Say("Pickup game starts soon!");                       break;
            case "gate":            Say("The gate leads to the street.");                  break;
        }
        // Example scene change:
        // GetTree().ChangeSceneToFile("res://Scenes/Clinic.tscn");
    }

    private void Say(string text)
    {
        _prompt.Text = text;
        _prompt.Visible = true;
        _msgTween?.Kill();
        _msgTween = CreateTween();
        _msgTween.TweenInterval(2.5);
        _msgTween.TweenCallback(Callable.From(() => _prompt.Visible = false));
    }
}
