using Godot;
using SchoolYardGame;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 100.0f;

    public InteractionZone CurrentZone { get; set; }
private AnimatedSprite2D _animatedSprite;

    private string _lastDirection = "front";

    public override void _Ready()
    {

        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        _animatedSprite.Play("frontIdle");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Vector2.Zero;

        Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        velocity = inputDirection * Speed;

        Velocity = velocity;
        MoveAndSlide();

        UpdateAnimation(inputDirection, GetRealVelocity());
    }

    private void UpdateAnimation(Vector2 inputDirection, Vector2 actualVelocity)
    {
        if (inputDirection != Vector2.Zero)
        {
            if (Mathf.Abs(inputDirection.X) > Mathf.Abs(inputDirection.Y))
            {
                _lastDirection = "side";
                _animatedSprite.FlipH = inputDirection.X < 0; // Flips sprite if walking left
            }
            else
            {
                _lastDirection = inputDirection.Y < 0 ? "back" : "front";
            }
        }

        if (actualVelocity.Length() < 1.0f)
        {
            if (_lastDirection == "side")
            {
                _animatedSprite.Play("rightIdle");
            }
            else if (_lastDirection == "back")
            {
                _animatedSprite.Play("backIdle");
            }
            else
            {
                _animatedSprite.Play("frontIdle");
            }
        }
        else
        {
            if (_lastDirection == "side")
            {
                _animatedSprite.Play("rightWalk");
            }
            else if (_lastDirection == "back")
            {
                _animatedSprite.Play("backWalk");
            }
            else
            {
                _animatedSprite.Play("frontWalk");
            }
        }
    }
}
