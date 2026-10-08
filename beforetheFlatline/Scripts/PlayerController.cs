using Godot;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 200.0f;

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;

        // Gets input from the keys you just mapped
        Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        if (direction != Vector2.Zero)
        {
            velocity = direction * Speed;
        }
        else
        {
            velocity = velocity.MoveToward(Vector2.Zero, Speed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}