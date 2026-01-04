using Raylib_cs;

namespace Engine.UI;

public class Circle : Node
{
    public Color Color { get; set; } = Color.White;
    
    public override void Update(float deltaTime)
    {
    }

    public override void Draw()
    {
        var radius = ComputedWidth / 2;
        Raylib.DrawCircle(
            (int)(ComputedPosition.X + radius),
            (int)(ComputedPosition.Y + radius),
            radius,
            Color
        );
    }

    public override void Dispose()
    {
    }
}
