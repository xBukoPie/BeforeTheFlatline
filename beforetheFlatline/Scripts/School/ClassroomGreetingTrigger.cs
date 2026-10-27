using Godot;

namespace SchoolYardGame;

public partial class ClassroomGreetingTrigger : Node
{
    public override void _Ready()
    {
        CallDeferred(nameof(TriggerGreeting));
    }

    private void TriggerGreeting()
    {
        var greeting = new SchoolGreeting();
        AddChild(greeting);
    }
}
