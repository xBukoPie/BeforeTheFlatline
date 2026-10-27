using Godot;
using SchoolYardGame;
using System.Collections.Generic;

public partial class HomeMap : Node2D
{
	[Export] public bool IsNight { get; set; } = true;
	[Export] public Color DayColor { get; set; } = new Color(0.77f, 0.77f, 0.81f);
	[Export] public Color NightColor { get; set; } = new Color(0.18f, 0.19f, 0.29f);
	[Export] public float DaySunEnergy { get; set; } = 0.12f;
	[Export] public float IndoorSunFactor { get; set; } = 0.25f;

	private CanvasModulate _ambient; private DirectionalLight2D _sun;
	private SanityManager _sanityManager;
	private Node2D _lights;
	private bool _insideHouse;

	public override void _Ready()
	{
		_ambient = GetNode<CanvasModulate>("AmbientLight");
		_sun = GetNode<DirectionalLight2D>("Sunlight");
        _lights = GetNode<Node2D>("Lights");
        _sanityManager = SanityManager.Instance;

		var interior = GetNode<Area2D>("Zones/InteriorZone");
		interior.BodyEntered += _ => SetInside(true);
		interior.BodyExited += _ => SetInside(false);

        if (DayClock.Instance != null)
        {
            DayClock.Instance.TimeChanged += OnTimeChanged;
            SetNight(DayClock.Instance.IsNight);
        }
        else
        {
            SetNight(IsNight);
        }
	}

    public override void _ExitTree()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.TimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(float hour)
    {
        if (DayClock.Instance != null && IsNight != DayClock.Instance.IsNight)
            SetNight(DayClock.Instance.IsNight);
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
            if (inside)
            {
                int amount = (int)Mathf.Floor(GD.Randi() % 10) + 1;
                _sanityManager?.Recover(amount);
            }
		UpdateSun();
	}

	private void UpdateSun()
	{
		float energy = IsNight ? 0f : DaySunEnergy;
		_sun.Energy = _insideHouse ? energy * IndoorSunFactor : energy;
	}
}
