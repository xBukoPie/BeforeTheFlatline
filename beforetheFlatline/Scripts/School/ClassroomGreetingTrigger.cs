using Godot;

namespace SchoolYardGame;

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