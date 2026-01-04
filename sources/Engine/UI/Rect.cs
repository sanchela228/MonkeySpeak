using Raylib_cs;

namespace Engine.UI;

public class Rect : Node
{
    public Color Color { get; set; } = Color.Blank;
    public Color? BorderColor { get; set; } = Color.Black;
    public float Rounded { get; set; } = 0;
    public int RoundedSegments { get; set; } = 10;
    
    public override void Update(float deltaTime)
    {
    }

    public override void Draw()
    {
        if (Rounded > 0)
        {
            var rectLinesEx = new Rectangle(
                (int)ComputedPosition.X,
                (int)ComputedPosition.Y,
                (int)ComputedWidth,
                (int)ComputedHeight
            );
            
            Raylib.DrawRectangleRounded(
                rectLinesEx,
                Rounded,
                RoundedSegments,
                Color
            );

            if (Border > 0)
            {
                var rectLinesRoundedEx = new Rectangle(
                    (int)ComputedPosition.X,
                    (int)ComputedPosition.Y,
                    (int)ComputedWidth,
                    (int)ComputedHeight
                );

                Raylib.DrawRectangleRoundedLinesEx(
                    rectLinesRoundedEx, 
                    Rounded,
                    RoundedSegments,
                    Border, 
                    BorderColor ?? Color
                );
            }
        }
        else
        {
            Raylib.DrawRectangle(
                (int)ComputedPosition.X, 
                (int)ComputedPosition.Y, 
                (int)ComputedWidth, 
                (int)ComputedHeight, 
                Color
            );
            
            if (Border > 0)
            {
                var rectLinesEx = new Rectangle(
                    (int)ComputedPosition.X,
                    (int)ComputedPosition.Y,
                    (int)ComputedWidth,
                    (int)ComputedHeight
                );

                Raylib.DrawRectangleLinesEx(rectLinesEx, Border, BorderColor ?? Color);
            }
        }
    }

    public override void Dispose()
    {
    }
}
