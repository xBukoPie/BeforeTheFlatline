using Godot;

namespace SchoolYardGame;

/// <summary>
/// When attached to a node (e.g., the Classroom UI scene), this script
/// automatically triggers a random greeting via SchoolGreeting once the node
/// enters the scene tree.
/// </summary>
public partial class ClassroomGreetingTrigger : Node
{
    public override void _Ready()
    {
        // Defer slightly to ensure the scene is fully ready.
        CallDeferred(nameof(TriggerGreeting));
    }

    private void TriggerGreeting()
    {
        var greeting = new SchoolGreeting();
        AddChild(greeting);
    }
}