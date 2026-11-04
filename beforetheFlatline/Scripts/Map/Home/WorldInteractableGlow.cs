using Godot;
using System;

public static class WorldInteractableGlow
{
    private static Texture2D _radialTexture;

    public static Sprite2D AddGlow(Node2D parent, Color tint, float relativeSize = 3.3f)
    {
        if (parent == null)
            return null;

        Vector2 subjectSize = new Vector2(24f, 24f);
        Sprite2D subject = parent.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (subject?.Texture != null)
        {
            Vector2 textureSize = subject.Texture.GetSize();
            subjectSize = textureSize * subject.Scale;
        }

        float subjectDiameter = Mathf.Max(subjectSize.X, subjectSize.Y);
        float textureDiameter = GetRadialTexture().GetSize().X;
        float baseScale = subjectDiameter * Mathf.Max(1f, relativeSize) / textureDiameter;

        var glow = new Sprite2D
        {
            Name = "InteractableGlow",
            Texture = GetRadialTexture(),
            Centered = true,
            ZIndex = -1,
            Scale = Vector2.One * baseScale,
            Modulate = new Color(tint.R, tint.G, tint.B, 0.62f),
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Material = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add
            }
        };
        glow.SetMeta("base_scale", baseScale);

        parent.AddChild(glow);
        return glow;
    }

    public static void Animate(Sprite2D glow, double elapsed, float phase = 0f)
    {
        if (glow == null || !GodotObject.IsInstanceValid(glow))
            return;

        float baseScale = (float)glow.GetMeta("base_scale", 0.25f).AsDouble();
        float pulse = (Mathf.Sin((float)elapsed * 2.8f + phase) + 1f) * 0.5f;
        float scale = Mathf.Max(0.01f, baseScale) * (1.0f + pulse * 0.18f);
        glow.Scale = Vector2.One * scale;
        Color tint = glow.Modulate;
        tint.A = 0.42f + pulse * 0.30f;
        glow.Modulate = tint;
    }

    private static Texture2D GetRadialTexture()
    {
        if (_radialTexture != null && GodotObject.IsInstanceValid(_radialTexture))
            return _radialTexture;

        const int size = 96;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
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
                float alpha = edge * edge * 0.84f;
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        _radialTexture = ImageTexture.CreateFromImage(image);
        return _radialTexture;
    }
}
