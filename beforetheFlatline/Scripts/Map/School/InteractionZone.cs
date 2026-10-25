using Godot;

namespace SchoolYardGame;

/// <summary>Area2D the player can stand in and press E to use. Emits Interacted(zoneId).</summary>
public partial class InteractionZone : Area2D
{
    [Export] public string ZoneId = "";
    [Export] public string Prompt = "Press E";

    [Signal] public delegate void InteractedEventHandler(string zoneId);
    [Signal] public delegate void PromptChangedEventHandler(string text, bool show);

    public override void _Ready()
    {
        BodyEntered += b =>
        {
            if (b is PlayerController p) { p.CurrentZone = this; EmitSignal(SignalName.PromptChanged, Prompt, true); }
        };
        BodyExited += b =>
        {
            if (b is PlayerController p && p.CurrentZone == this)
            {
                p.CurrentZone = null;
                EmitSignal(SignalName.PromptChanged, "", false);
            }
        };
    }

    [Export] public string TargetScenePath = "res://Scenes/School/Rooms/Classroom.tscn";
    // This scene is a UI Control, not a Room. We'll overlay it on the current room.

    public void Interact(PlayerController p)
    {
        // Preserve original interaction signal
        EmitSignal(SignalName.Interacted, ZoneId);

        // Load the Classroom UI (Control) scene
        var packed = GD.Load<PackedScene>(TargetScenePath);
        if (packed == null) return;
        var uiInstance = packed.Instantiate(); // Should be a Control node

        var tree = GetTree();
        // Get RoomManager (the root manager that holds the UI CanvasLayer)
        var rm = tree.Root.GetNodeOrNull<RoomManager>("RoomManager");
        if (rm == null) return;

        // Attach UI to the RoomManager's UI node (CanvasLayer) so it renders on top of the room.
        var uiRoot = rm.GetNodeOrNull<Node>("UI");
        if (uiRoot != null)
            uiRoot.AddChild(uiInstance);
        else
            rm.AddChild(uiInstance); // fallback, still visible

        // Clear any lingering fade overlay (prevents black screen)
        var fade = rm.GetNodeOrNull<ColorRect>("UI/Fade");
        if (fade != null)
            fade.Modulate = new Color(0,0,0,0);

        // No greeting is attached here any more: the overlay scene (e.g. Classroom.tscn)
        // starts its own dialogue event in its _Ready.
    }
}
