using System.Numerics;
using Engine.Managers;
using Raylib_cs;

namespace Engine.Types;

public struct FontFamily
{
    public Color Color { get; set; }
    public string FontPath { get; set; }
    public int Size { get; set; }
    public float Rotation { get; set; }
    public float Spacing { get; set; }
    
    public Font Font => Resources.FontEx(FontPath ?? "default.ttf", Size);

    public Vector2 CalcTextSize(string text) => Raylib.MeasureTextEx(Font, text, Size, Spacing);

    public void ChangeSize(int newSize)
    {
        Size = newSize;
    }
}