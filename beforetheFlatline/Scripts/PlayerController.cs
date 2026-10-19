using Godot;

public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 100.0f;

	private Vector2 _targetPosition;
	private AnimatedSprite2D _animatedSprite;

	// Tracks the last movement direction to trigger the correct idle animation
	private string _lastDirection = "front";

	public override void _Ready()
	{
		_targetPosition = GlobalPosition;
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		// Start the game facing forward
		_animatedSprite.Play("frontIdle");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("move_to")) // Your right-click action
		{
			_targetPosition = GetGlobalMousePosition();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Vector2.Zero;

		// Move towards the target position if the player is more than 5 pixels away
		if (GlobalPosition.DistanceTo(_targetPosition) > 5.0f)
		{
			Vector2 direction = GlobalPosition.DirectionTo(_targetPosition);
			velocity = direction * Speed;
		}

		Velocity = velocity;
		MoveAndSlide();

		UpdateAnimation(velocity);
	}

	private void UpdateAnimation(Vector2 velocity)
	{
		// 1. Handle Idle State
		if (velocity == Vector2.Zero)
		{
			if (_lastDirection == "side")
			{
				_animatedSprite.Play("rightIdle");
				// Note: FlipH stays exactly as it was when walking, 
				// so rightIdle handles both left and right automatically.
			}
			else if (_lastDirection == "back")
			{
				_animatedSprite.Play("backIdle");
			}
			else
			{
				_animatedSprite.Play("frontIdle");
			}
			return;
		}

		// 2. Handle Walking State & Update Last Direction
		// Determines if horizontal movement is stronger than vertical
		if (Mathf.Abs(velocity.X) > Mathf.Abs(velocity.Y))
		{
			_animatedSprite.Play("rightWalk"); // Adjust string to match your exact animation name
			_animatedSprite.FlipH = velocity.X < 0; // Flips sprite if walking left
			_lastDirection = "side";
		}
		else
		{
			if (velocity.Y < 0)
			{
				_animatedSprite.Play("backWalk"); // Adjust string to match your exact animation name
				_lastDirection = "back";
			}
			else
			{
				_animatedSprite.Play("frontWalk"); // Adjust string to match your exact animation name
				_lastDirection = "front";
			}
		}
	}
}
