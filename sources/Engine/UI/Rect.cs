using Raylib_cs;

namespace Engine.UI;

public class Rect : Node
{
    public Color Color { get; set; } = Color.White;
    
    public override void Update(float deltaTime)
    {
    }

    public override void Draw()
    {
        Raylib.DrawRectangleLines(
            (int)ComputedPosition.X, 
            (int)ComputedPosition.Y, 
            (int)ComputedWidth, 
            (int)ComputedHeight, 
            Color
        );
    }

    public override void Dispose()
    {
    }
}
