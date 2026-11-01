using Godot;
using System;

namespace SchoolYardGame;

/// <summary>
/// Visible but unobtrusive wind ambience for the school yard.
/// Leaves drift across the play area and never interact with gameplay.
/// </summary>
public partial class SchoolWindEffects : Node2D
{
    private const int LeafCount = 22;
    private const float WindStartX = -520f;
    private const float WindEndX = 520f;

    private readonly RandomNumberGenerator _rng = new();
    private bool _stopping;

    public override void _Ready()
    {
        _rng.Randomize();
        Texture2D leafTexture = CreateLeafTexture();

        for (int i = 0; i < LeafCount; i++)
        {
            float alpha = _rng.RandfRange(0.68f, 0.88f);
            Color leafColor = (i % 3) switch
            {
                0 => new Color(1f, 0.86f, 0.54f, alpha),
                1 => new Color(0.82f, 0.9f, 0.57f, alpha),
                _ => new Color(0.94f, 0.73f, 0.48f, alpha)
            };

            var leaf = new Sprite2D
            {
                Name = $"WindLeaf_{i + 1}",
                Texture = leafTexture,
                Position = new Vector2(
                    _rng.RandfRange(WindStartX, WindEndX),
                    _rng.RandfRange(-255f, 255f)),
                Rotation = _rng.RandfRange(-0.8f, 0.8f),
                Scale = Vector2.One * _rng.RandfRange(0.42f, 0.68f),
                Modulate = leafColor,
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
            float remaining = Mathf.Clamp(
                (WindEndX - leaf.Position.X) / (WindEndX - WindStartX),
                0.08f,
                1f);

            // A slow cross-map breeze with a small vertical drift.
            double duration = _rng.RandfRange(25f, 38f) * remaining;
            Vector2 target = new(
                WindEndX,
                Mathf.Clamp(leaf.Position.Y + _rng.RandfRange(-95f, 95f), -260f, 260f));

            float targetRotation = leaf.Rotation + _rng.RandfRange(-2.6f, 2.6f);
            Tween tween = CreateTween();
            tween.TweenProperty(leaf, "position", target, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(leaf, "rotation", targetRotation, duration);

            await ToSignal(tween, Tween.SignalName.Finished);

            if (_stopping || !IsInsideTree() || !GodotObject.IsInstanceValid(leaf))
                return;

            // Restart beyond the left edge so the breeze remains continuous.
            leaf.Position = new Vector2(
                WindStartX,
                _rng.RandfRange(-255f, 255f));
            leaf.Rotation = _rng.RandfRange(-0.8f, 0.8f);
        }
    }

    private static Texture2D CreateLeafTexture()
    {
        Image image = Image.CreateEmpty(12, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

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
                image.SetPixel(x, y, Colors.White);
        }

        // The contrasting center vein keeps the leaf readable while it rotates.
        image.SetPixel(5, 1, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(6, 2, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(5, 3, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(6, 4, new Color(0.72f, 0.72f, 0.72f, 1f));
        image.SetPixel(5, 5, new Color(0.72f, 0.72f, 0.72f, 1f));

        return ImageTexture.CreateFromImage(image);
    }
}
