using Godot;

namespace SchoolYardGame;

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

    public void Interact(PlayerController p)
    {
        EmitSignal(SignalName.Interacted, ZoneId);

        var packed = GD.Load<PackedScene>(TargetScenePath);
        if (packed == null) return;
        var uiInstance = packed.Instantiate(); // Should be a Control node

        var tree = GetTree();
        var rm = tree.Root.GetNodeOrNull<RoomManager>("RoomManager");
        if (rm == null) return;

        var uiRoot = rm.GetNodeOrNull<Node>("UI");
        if (uiRoot != null)
            uiRoot.AddChild(uiInstance);
        else
            rm.AddChild(uiInstance); // fallback, still visible

        var fade = rm.GetNodeOrNull<ColorRect>("UI/Fade");
        if (fade != null)
            fade.Modulate = new Color(0,0,0,0);

    }
}
