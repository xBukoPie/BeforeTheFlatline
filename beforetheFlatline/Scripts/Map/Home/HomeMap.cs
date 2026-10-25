using Godot;

// Attach to the root "Main" node of House.tscn (res://Scripts/HomeMap.cs).
public partial class HomeMap : Node2D
{
	[Export] public bool IsNight { get; set; } = true;
	[Export] public Color DayColor { get; set; } = new Color(0.9f, 0.92f, 1.0f);
	[Export] public Color NightColor { get; set; } = new Color(0.36f, 0.4f, 0.62f);
	[Export] public float DaySunEnergy { get; set; } = 0.55f;
	[Export] public float IndoorSunFactor { get; set; } = 0.25f;

	private CanvasModulate _ambient;
	private DirectionalLight2D _sun;
	private Node2D _lights;
	private bool _insideHouse;

	public override void _Ready()
	{
		_ambient = GetNode<CanvasModulate>("AmbientLight");
		_sun = GetNode<DirectionalLight2D>("Sunlight");
		_lights = GetNode<Node2D>("Lights");

		// The Player is on physics layer 2, so these zones only react to it.
		var interior = GetNode<Area2D>("Zones/InteriorZone");
		interior.BodyEntered += _ => SetInside(true);
		interior.BodyExited += _ => SetInside(false);

		SetNight(IsNight);
	}

	public void SetNight(bool night)
	{
		IsNight = night;
		_ambient.Color = night ? NightColor : DayColor;
		_lights.Visible = night;   // lamps only at night
		UpdateSun();
	}

	private void SetInside(bool inside)
	{
		_insideHouse = inside;
		UpdateSun();
	}

	private void UpdateSun()
	{
		float energy = IsNight ? 0f : DaySunEnergy;
		_sun.Energy = _insideHouse ? energy * IndoorSunFactor : energy;
	}
}
