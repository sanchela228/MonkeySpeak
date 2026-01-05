using Engine.Types;

namespace Engine.UI;

public class Text : Node
{
    public FontFamily FontFamily { get; set; }
    public int MaxLength { get; set; } = 0;
    public bool ClampDots { get; set; } = false;
    public bool Wrap { get; set; } = true;
    public bool UseLanguage { get; set; } = true;
    public override void Update(float deltaTime)
    {
        
    }

    public override void Draw()
    {
        
    }

    public override void Dispose()
    {
        
    }
}