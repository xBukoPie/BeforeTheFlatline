using System.Threading.Tasks;
using Godot;

// Root script of World.tscn. Keeps the Player and Camera alive and swaps rooms under "RoomHolder".
public partial class RoomManager : Node2D
{
	private static string _requestedStartRoom;
	private static string _requestedStartSpawn;

	/// <summary>Choose the room loaded by the next World.tscn instance.</summary>
	public static void RequestStartRoomOnNextLoad(string roomPath, string spawn)
	{
		_requestedStartRoom = roomPath;
		_requestedStartSpawn = spawn;
	}

	[Export(PropertyHint.File, "*.tscn")] public string StartRoom { get; set; } = "res://Scenes/Home/Rooms/Kwarto.tscn";
	[Export] public string StartSpawn { get; set; } = "startSpawn";
	[Export] public bool IsNight { get; set; } = true;
	[Export] public bool AllowNightToggleKey { get; set; } = true;   // press N to flip day/night while testing
	[Export] public float FadeTime { get; set; } = 0.25f;
	[Export] public Color DayColor { get; set; } = new Color(0.9f, 0.92f, 1.0f);
	[Export] public Color NightColor { get; set; } = new Color(0.36f, 0.4f, 0.62f);
	[Export] public float DaySunEnergy { get; set; } = 0.55f;

	private Node2D _holder;
	private Node2D _player;
	private Camera2D _camera;
	private CanvasModulate _ambient;
	private DirectionalLight2D _sun;
	private ColorRect _fade;
	private Room _room;
	private bool _busy;

	public override void _Ready()
	{
        EnsureInteractAction();

		_holder = GetNode<Node2D>("RoomHolder");
		_player = GetNode<Node2D>("Player");
		_camera = GetNode<Camera2D>("Player/Camera2D");
		_ambient = GetNode<CanvasModulate>("RoomHolder/AmbientLight");
		_sun = GetNode<DirectionalLight2D>("RoomHolder/Sunlight");
		_fade = GetNode<ColorRect>("UI/Fade");

		if (SchoolYardGame.DayClock.Instance != null)
		{
			SchoolYardGame.DayClock.Instance.TimeChanged += OnTimeChanged;
			SetNight(SchoolYardGame.DayClock.Instance.IsNight);
		}

		string roomPath = string.IsNullOrWhiteSpace(_requestedStartRoom) ? StartRoom : _requestedStartRoom;
		string spawn = string.IsNullOrWhiteSpace(_requestedStartSpawn) ? StartSpawn : _requestedStartSpawn;
		_requestedStartRoom = null;
		_requestedStartSpawn = null;
		if (SchoolYardGame.DayClock.Instance != null)
			SchoolYardGame.DayClock.Instance.Running = true;
		LoadRoom(roomPath, spawn);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (AllowNightToggleKey && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.N })
		{
			if (SchoolYardGame.DayClock.Instance != null)
				SchoolYardGame.DayClock.Instance.SetHourOfDay(SchoolYardGame.DayClock.Instance.IsNight ? 12f : 20f);
			else
				SetNight(!IsNight);
		}
	}

	public override void _ExitTree()
	{
		if (SchoolYardGame.DayClock.Instance != null)
			SchoolYardGame.DayClock.Instance.TimeChanged -= OnTimeChanged;
	}

	private void OnTimeChanged(float hour)
	{
		if (SchoolYardGame.DayClock.Instance != null && IsNight != SchoolYardGame.DayClock.Instance.IsNight)
			SetNight(SchoolYardGame.DayClock.Instance.IsNight);
	}

	public async void GoTo(string roomPath, string spawn)
	{
		if (_busy || string.IsNullOrEmpty(roomPath))
			return;

		_busy = true;
		_player.ProcessMode = ProcessModeEnum.Disabled;

		try
		{
			await FadeTo(1f);
			LoadRoom(roomPath, spawn);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			_camera.ResetSmoothing();
			await FadeTo(0f);
		}
		catch (System.Exception ex)
		{
			GD.PushError($"RoomManager: Error during room transition to '{roomPath}': {ex.Message}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(_player))
				_player.ProcessMode = ProcessModeEnum.Inherit;
			_busy = false;
		}
	}

	public void SetNight(bool night)
	{
		IsNight = night;
		_ambient.Color = night ? NightColor : DayColor;
		_sun.Energy = (!night && _room != null && _room.Outdoor) ? DaySunEnergy : 0f;
		_room?.ApplyTime(night);
	}

	private void LoadRoom(string path, string spawn)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			GD.PushError($"RoomManager: cannot load room '{path}'.");
			return;
		}

		if (_room != null)
		{
			_holder.RemoveChild(_room);
			_room.QueueFree();
		}

		_room = scene.Instantiate<Room>();
        // The clock stops in school and stays stopped during its challenge/dialogue sequence.
        // Leaving to any home/lot room resumes normal time progression.
        if (SchoolYardGame.DayClock.Instance != null)
            SchoolYardGame.DayClock.Instance.Running =
                !string.Equals(_room.RoomName, "School", System.StringComparison.OrdinalIgnoreCase);
		_holder.AddChild(_room);

		_player.GlobalPosition = _room.GetSpawnPosition(spawn);
		if (_player is CharacterBody2D body)
			body.Velocity = Vector2.Zero;

		ConfigureCamera();
		SetNight(IsNight);
	}

	// Zooms to the room and keeps the camera centered when the room is smaller than the screen.
	private void ConfigureCamera()
	{
		Rect2 bounds = _room.GetBounds();
		float zoom = _room.CameraZoom;
		Vector2 view = GetViewportRect().Size / zoom;
		Vector2 size = new(Mathf.Max(bounds.Size.X, view.X), Mathf.Max(bounds.Size.Y, view.Y));
		Rect2 limits = new(bounds.GetCenter() - size / 2f, size);

		_camera.Zoom = new Vector2(zoom, zoom);
		_camera.LimitLeft = Mathf.RoundToInt(limits.Position.X);
		_camera.LimitTop = Mathf.RoundToInt(limits.Position.Y);
		_camera.LimitRight = Mathf.RoundToInt(limits.End.X);
		_camera.LimitBottom = Mathf.RoundToInt(limits.End.Y);
		_camera.ResetSmoothing();
	}

	private async Task FadeTo(float alpha)
	{
		var tween = CreateTween();
		tween.TweenProperty(_fade, "modulate:a", alpha, FadeTime);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	// Adds an "interact" action (E) if the project does not define one.
	private static void EnsureInteractAction()
	{
		if (InputMap.HasAction("interact"))
			return;

		InputMap.AddAction("interact");
		InputMap.ActionAddEvent("interact", new InputEventKey { PhysicalKeycode = Key.E });
	}
}
