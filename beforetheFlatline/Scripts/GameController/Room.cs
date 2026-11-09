using Godot;

public partial class Room : Node2D
{
	public const int BackgroundZIndex = -100;
	public const int PlayerZIndex = 0;
	public const int ObjectBehindPlayerZIndex = -1;
	public const int ObjectInFrontOfPlayerZIndex = 1;
	public const int WorldLightMask = 1;

	[Export] public string RoomName { get; set; } = "";
	[Export] public bool Outdoor { get; set; } = false;      // outdoor rooms get sunlight and night-only lamps
	[Export] public float CameraZoom { get; set; } = 2.5f;

	public override void _Ready()
	{
		// Direct room sprites are painted backdrops; keep them below movable world objects.
		foreach (Node child in GetChildren())
		{
			if (child is not Sprite2D sprite)
				continue;

			sprite.ZAsRelative = true;
			sprite.ZIndex = BackgroundZIndex;
			sprite.LightMask = WorldLightMask;
		}
	}

	public Rect2 GetBounds()
	{
		var ground = GetNode<Sprite2D>("Ground");
		Vector2 size = ground.RegionEnabled ? ground.RegionRect.Size : ground.Texture.GetSize();
		return new Rect2(ground.GlobalPosition - size / 2f, size);
	}

	public Vector2 GetSpawnPosition(string spawnName)
	{
		var marker = GetNodeOrNull<Marker2D>($"Spawns/{spawnName}");
		if (marker != null)
			return marker.GlobalPosition;

		GD.PushWarning($"Room '{Name}': spawn '{spawnName}' not found, using the first spawn.");
		var first = GetNodeOrNull<Node>("Spawns")?.GetChildOrNull<Marker2D>(0);
		return first?.GlobalPosition ?? GlobalPosition;
	}

	public void ApplyTime(bool night) => ApplyNightBlend(night ? 1f : 0f);

	// Outdoor lamps fade with the night level instead of switching on abruptly.
	// Indoor light fixtures remain present while the room's ambient tint transitions.
	public void ApplyNightBlend(float nightBlend)
	{
		float blend = Mathf.Clamp(nightBlend, 0f, 1f);
		if (GetNodeOrNull<Node2D>("Lights") is not Node2D lights)
			return;

		lights.Visible = !Outdoor || blend > 0.005f;
		ApplyLightEnergy(lights, blend);
	}

	private void ApplyLightEnergy(Node node, float nightBlend)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Light2D light)
			{
				if (!light.HasMeta("day_night_base_energy"))
					light.SetMeta("day_night_base_energy", light.Energy);

				float baseEnergy = (float)light.GetMeta("day_night_base_energy").AsDouble();
				if (Outdoor)
					light.Energy = baseEnergy * nightBlend;
			}
			ApplyLightEnergy(child, nightBlend);
		}
	}
}
