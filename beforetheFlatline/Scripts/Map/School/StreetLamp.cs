using Godot;

namespace SchoolYardGame;

public partial class StreetLamp : PointLight2D
{
    [Export] public bool Flicker = true;
    private float _baseEnergy;
    private readonly FastNoiseLite _noise = new() { Frequency = 0.8f };
    private float _seed;

    public override void _Ready()
    {
        _baseEnergy = Energy;
        _seed = GD.Randf() * 1000f;
    }

    public override void _Process(double delta)
    {
        float k = Mathf.SmoothStep(0.15f, 0.7f, GameController.Darkness);
        float f = 1f;
        if (Flicker)
        {
            float n = _noise.GetNoise1D(_seed + Time.GetTicksMsec() * 0.01f);
            f = 0.94f + n * 0.12f;
        }
        Energy = _baseEnergy * k * f;
        Visible = k > 0.01f;
    }
}
