using Godot;
using System;

namespace SchoolYardGame;

/// <summary>
/// Lightweight, screen-independent wind ambience for the school yard.
/// Leaves drift slowly across the map and never interact with gameplay.
/// </summary>
public partial class SchoolWindEffects : Node2D
{
    private readonly RandomNumberGenerator _rng = new();
    private bool _stopping;

    public override void _Ready()
    {
        _rng.Randomize();
        Texture2D leafTexture = CreateLeafTexture();

        // Keep the ambience sparse so it adds movement without obscuring the characters.
        for (int i = 0; i < 9; i++)
        {
            var leaf = new Sprite2D
            {
                Name = $"WindLeaf_{i + 1}",
                Texture = leafTexture,
                Position = new Vector2(
                    _rng.RandfRange(-500f, 500f),
                    _rng.RandfRange(-255f, 255f)),
                Rotation = _rng.RandfRange(-0.6f, 0.6f),
                Scale = Vector2.One * _rng.RandfRange(0.16f, 0.25f),
                Modulate = new Color(0.91f, 0.82f, 0.61f, _rng.RandfRange(0.35f, 0.58f)),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                ZAsRelative = false,
                ZIndex = 8
            };

            AddChild(leaf);
            DriftLeaf(leaf);
        }
    }

    public override void _ExitTree()
    {
        _stopping = true;
    }

    private async void DriftLeaf(Sprite2D leaf)
    {
        while (!_stopping && IsInsideTree() && GodotObject.IsInstanceValid(leaf))
        {
            float remaining = Mathf.Clamp((500f - leaf.Position.X) / 1000f, 0.08f, 1f);
            double duration = _rng.RandfRange(20f, 31f) * remaining;

            Vector2 target = new(
                520f,
                Mathf.Clamp(leaf.Position.Y + _rng.RandfRange(-75f, 75f), -260f, 260f));

            float targetRotation = leaf.Rotation + _rng.RandfRange(-2.2f, 2.2f);
            Tween tween = CreateTween();
            tween.TweenProperty(leaf, "position", target, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(leaf, "rotation", targetRotation, duration);

            await ToSignal(tween, Tween.SignalName.Finished);

            if (_stopping || !IsInsideTree() || !GodotObject.IsInstanceValid(leaf))
                return;

            // Restart beyond the left edge so the breeze feels continuous.
            leaf.Position = new Vector2(
                -520f,
                _rng.RandfRange(-255f, 255f));
            leaf.Rotation = _rng.RandfRange(-0.6f, 0.6f);
        }
    }

    private static Texture2D CreateLeafTexture()
    {
        Image image = Image.CreateEmpty(12, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        Color baseColor = Colors.White;
        int[][] rows =
        {
            new[] { 5, 6 },
            new[] { 3, 4, 5, 6, 7, 8 },
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            new[] { 3, 4, 5, 6, 7, 8 },
            new[] { 5, 6 }
        };

        for (int y = 0; y < rows.Length; y++)
        {
            foreach (int x in rows[y])
                image.SetPixel(x, y, baseColor);
        }

        // A small central vein gives the generated leaf a readable silhouette.
        image.SetPixel(5, 1, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(6, 2, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(5, 3, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(6, 4, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(5, 5, new Color(0.72f, 0.72f, 0.72f, 1f));

        return ImageTexture.CreateFromImage(image);
    }
}
