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
        float radius = 0;
        
        if (ComputedWidth > 0)
            radius = ComputedWidth / 2;
        else if ( ComputedHeight > 0)
            radius = ComputedHeight / 2;

        if (radius > 0)
        {
            Raylib.DrawCircle(
                (int)(ComputedPosition.X + radius),
                (int)(ComputedPosition.Y + radius),
                radius,
                Color
            );
        }
    }

    public override void Dispose()
    {
    }
}
