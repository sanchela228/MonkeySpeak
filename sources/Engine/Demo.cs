using Raylib_cs;

namespace Engine;

public class DemoW : Node
{
    public override void Update(float deltaTime)
    {
        // throw new NotImplementedException();
    }

    public override void Draw()
    {
        Console.WriteLine(this);
        Raylib.DrawRectangle(
            (int)X, (int)Y, 
            (int)ComputedWidth, (int)ComputedHeight, 
            Color.Red
        );
        
    }

    public override void Dispose()
    {
        // throw new NotImplementedException();
    }
}