using Godot;
using System;

public static class WorldInteractableGlow
{
    private static Texture2D _radialTexture;

    public static Sprite2D AddGlow(Node2D parent, Color tint, float scale = 2.2f)
    {
        if (parent == null)
            return null;

        var glow = new Sprite2D
        {
            Name = "InteractableGlow",
            Texture = GetRadialTexture(),
            Centered = true,
            ZIndex = -1,
            Scale = Vector2.One * Mathf.Max(1f, scale),
            Modulate = new Color(tint.R, tint.G, tint.B, 0.48f),
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Material = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add
            }
        };

        parent.AddChild(glow);
        return glow;
    }

    public static void Animate(Sprite2D glow, double elapsed, float phase = 0f)
    {
        if (glow == null || !GodotObject.IsInstanceValid(glow))
            return;

        float pulse = (Mathf.Sin((float)elapsed * 2.8f + phase) + 1f) * 0.5f;
        float scale = 1.0f + pulse * 0.13f;
        glow.Scale = Vector2.One * scale;
        Color tint = glow.Modulate;
        tint.A = 0.28f + pulse * 0.22f;
        glow.Modulate = tint;
    }

    private static Texture2D GetRadialTexture()
    {
        if (_radialTexture != null && GodotObject.IsInstanceValid(_radialTexture))
            return _radialTexture;

        const int size = 96;
        Image image = Image.Create(size, size, false, Image.Format.Rgba8);
        float center = (size - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Clamp(1f - distance, 0f, 1f);
                float alpha = edge * edge * 0.72f;
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        _radialTexture = ImageTexture.CreateFromImage(image);
        return _radialTexture;
    }
}
