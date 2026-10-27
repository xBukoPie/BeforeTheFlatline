using Godot;
using SchoolYardGame;


public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 100.0f;

    // Commented out for WASD movement
    // private Vector2 _targetPosition;

    public InteractionZone CurrentZone { get; set; }
private AnimatedSprite2D _animatedSprite;

    // Tracks the last movement direction to trigger the correct idle animation
    private string _lastDirection = "front";
    private float _debuffElapsed;

    public override void _Ready()
    {
        // Commented out for WASD movement
        // _targetPosition = GlobalPosition;

        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Start the game facing forward
        _animatedSprite.Play("frontIdle");
    }

    /* Commented out mouse movement input
	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("move_to")) // Your right-click action
		{
			_targetPosition = GetGlobalMousePosition();
		}
	}
	*/

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Vector2.Zero;
        _debuffElapsed += (float)delta;

        // Keep the user's requested direction for facing/animation, but apply active sanity
        // debuffs to actual movement. Confusion bends the movement direction; dizziness adds
        // a smaller sway. Stacked effects also reduce the player's top walking speed.
        Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Vector2 movementDirection = inputDirection;
        float speedMultiplier = 1f;

        var sanity = SanityManager.Instance;
        if (sanity != null)
        {
            speedMultiplier = sanity.GetMovementSpeedMultiplier();
            float dizzinessSway = Mathf.Sin(_debuffElapsed * 2.1f) * sanity.DizzinessSeverity * 0.12f;
            float confusionSway = Mathf.Sin(_debuffElapsed * 1.55f + 1.2f) * sanity.ConfusionSeverity * 0.38f;
            movementDirection = inputDirection.Rotated(dizzinessSway + confusionSway);
        }

        velocity = movementDirection * Speed * speedMultiplier;
        Velocity = velocity;
        MoveAndSlide();

        // Facing follows the deliberate input; walking animation follows physical movement.
        UpdateAnimation(inputDirection, GetRealVelocity());
    }

    private void UpdateAnimation(Vector2 inputDirection, Vector2 actualVelocity)
    {
        // 1. Update facing direction based on INPUT
        // This ensures you can still turn around in place while stuck against a wall
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

        // 2. Play Idle or Walk based on ACTUAL physical movement
        // Using a small threshold (< 1.0f) protects against physics jitter when sliding against walls
        if (actualVelocity.Length() < 1.0f)
        {
            // Idle State
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
            // Walking State
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