using System.Numerics;
using Engine.Managers;
using Engine.Types;
using Raylib_cs;

namespace Engine.UI;

public enum TextAlign
{
    Left,
    Center,
    Right
}

public class Text : Node
{
    public string Content { get; set; } = "";
    public FontFamily FontFamily { get; set; }
    public int MaxLength { get; set; } = 0;
    public bool ClampDots { get; set; } = false;
    public bool Wrap { get; set; } = true;
    public bool UseLanguage { get; set; } = true;
    public TextAlign Align { get; set; } = TextAlign.Left;
    
    private List<string> _lines = new();
    private float _lineHeight;
    private float _maxHeight;
    
    public override void Update(float deltaTime)
    {
    }

    public override void MeasureSize()
    {
        float parentWidth = Parent?.ComputedWidth ?? 800;
        float parentHeight = Parent?.ComputedHeight ?? 600;
        
        if (Parent != null)
        {
            parentWidth -= Parent.ComputedBorderLeft + Parent.ComputedBorderRight 
                         + Parent.ComputedPaddingLeft + Parent.ComputedPaddingRight;
            parentHeight -= Parent.ComputedBorderTop + Parent.ComputedBorderBottom 
                          + Parent.ComputedPaddingTop + Parent.ComputedPaddingBottom;
        }
        
        ResolveSize(parentWidth, parentHeight);
        
        var sampleSize = FontFamily.CalcTextSize("Ay");
        _lineHeight = sampleSize.Y;
        
        string text = Content;
        if (MaxLength > 0 && text.Length > MaxLength)
            text = text[..MaxLength];
        
        _lines.Clear();
        _wrapWidth = (Width.IsAuto || ComputedWidth == 0) ? parentWidth : ComputedWidth;
        
        if (Wrap && _wrapWidth > 0)
        {
            WrapText(text, _wrapWidth);
        }
        else
        {
            _lines.Add(text);
        }
        
        float maxLineWidth = 0;
        foreach (var line in _lines)
        {
            var lineSize = FontFamily.CalcTextSize(line);
            if (lineSize.X > maxLineWidth)
                maxLineWidth = lineSize.X;
        }
        
        if (Width.IsAuto)
            ComputedWidth = maxLineWidth;
        
        _maxHeight = Height.IsAuto ? parentHeight : ComputedHeight;
        
        if (Height.IsAuto)
            ComputedHeight = _lines.Count * _lineHeight;
        
        foreach (var child in Children)
            child.MeasureSize();
    }
    
    private void WrapText(string text, float maxWidth)
    {
        var words = text.Split(' ');
        string currentLine = "";
        
        foreach (var word in words)
        {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var testSize = FontFamily.CalcTextSize(testLine);
            
            if (testSize.X > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                _lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }
        
        if (!string.IsNullOrEmpty(currentLine))
            _lines.Add(currentLine);
        
        if (_lines.Count == 0)
            _lines.Add("");
    }

    private float _wrapWidth;
    
    public override void Draw()
    {
        float y = ComputedPosition.Y;
        float availableWidth = _wrapWidth > 0 ? _wrapWidth : ComputedWidth;
        
        int maxLines = _lineHeight > 0 ? (int)(_maxHeight / _lineHeight) : _lines.Count;
        bool needsClamp = ClampDots && _lines.Count > maxLines && maxLines > 0;
        
        int linesToDraw = needsClamp ? maxLines : _lines.Count;
        
        for (int i = 0; i < linesToDraw; i++)
        {
            string line = _lines[i];
            
            bool isLastVisibleLine = (i == linesToDraw - 1) && needsClamp;
            
            if (isLastVisibleLine)
            {
                line = ClampWithDots(line, availableWidth);
            }
            else if (ClampDots && !Wrap)
            {
                var clampSize = FontFamily.CalcTextSize(line);
                if (clampSize.X > availableWidth)
                {
                    line = ClampWithDots(line, availableWidth);
                }
            }
            
            var lineSize = FontFamily.CalcTextSize(line);
            float x = Align switch
            {
                TextAlign.Center => ComputedPosition.X + (availableWidth - lineSize.X) / 2,
                TextAlign.Right => ComputedPosition.X + availableWidth - lineSize.X,
                _ => ComputedPosition.X
            };
            
            Raylib.DrawTextEx(
                FontFamily.Font,
                line,
                new Vector2(x, y),
                FontFamily.Size,
                FontFamily.Spacing,
                FontFamily.Color
            );
            
            y += _lineHeight;
        }
    }
    
    private string ClampWithDots(string text, float maxWidth)
    {
        const string dots = "...";
        var dotsSize = FontFamily.CalcTextSize(dots);
        float targetWidth = maxWidth - dotsSize.X;
        
        if (targetWidth <= 0)
            return dots;
        
        int left = 0;
        int right = text.Length;
        
        while (left < right)
        {
            int mid = (left + right + 1) / 2;
            var testSize = FontFamily.CalcTextSize(text[..mid]);
            
            if (testSize.X <= targetWidth)
                left = mid;
            else
                right = mid - 1;
        }
        
        if (left == 0)
            return dots;
        
        return text[..left] + dots;
    }

    public override void Dispose()
    {
    }
}