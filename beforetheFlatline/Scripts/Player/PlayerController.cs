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

        /* Commented out mouse movement logic
		// Move towards the target position if the player is more than 5 pixels away
		if (GlobalPosition.DistanceTo(_targetPosition) > 5.0f)
		{
			Vector2 direction = GlobalPosition.DirectionTo(_targetPosition);
			velocity = direction * Speed;
		}
		*/

        // New WASD / Arrow Key movement logic
        Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        velocity = inputDirection * Speed;

        Velocity = velocity;
        MoveAndSlide();

        // Pass BOTH the raw input direction (for looking) and the real movement (for walking)
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