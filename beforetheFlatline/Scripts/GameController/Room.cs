using Godot;

// Root script of every room scene. Expects children: "Ground" (Sprite2D), optional "Lights" and "Spawns".
public partial class Room : Node2D
{
	[Export] public string RoomName { get; set; } = "";
	[Export] public bool Outdoor { get; set; } = false;      // outdoor rooms get sunlight and night-only lamps
	[Export] public float CameraZoom { get; set; } = 2.5f;

	// World-space rectangle covered by the Ground sprite (camera limits are derived from it).
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

	// Indoor lamps are always on; outdoor lamps only at night.
	public void ApplyTime(bool night)
	{
		if (GetNodeOrNull<Node2D>("Lights") is Node2D lights)
			lights.Visible = night || !Outdoor;
	}
}
