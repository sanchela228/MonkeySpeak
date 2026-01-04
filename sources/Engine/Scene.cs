using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace Engine;

public enum LayoutMode
{
    None,
    Vertical,
    Horizontal
}

public class Scene : Node
{
    public SceneStack Navigation { get; }
    public LayoutMode Layout { get; set; } = LayoutMode.Vertical;
    public Color? BackgroundColor { get; set; } = null;
    
    public Scene()
    {
        Navigation = new SceneStack(this);
    }
    
    internal void AddChildInternal(Node child)
    {
        _children.Add(child);
        if (_children.Count > 1)
            _children.Sort((a, b) => a.Order.CompareTo(b.Order));
    }
    
    internal void RemoveChildInternal(Node child)
    {
        _children.Remove(child);
    }
    
    public override void Update(float deltaTime)
    {
    }
    
    public override void Draw()
    {
        if (BackgroundColor.HasValue)
        {
            Raylib.DrawRectangle(
                (int)ComputedPosition.X,
                (int)ComputedPosition.Y,
                (int)ComputedWidth,
                (int)ComputedHeight,
                BackgroundColor.Value
            );
        }
    }
    
    public override void Dispose()
    {
        Navigation.Clear();
    }
    
    public override void MeasureSize()
    {
        float parentWidth = Parent?.ComputedWidth ?? 800;
        float parentHeight = Parent?.ComputedHeight ?? 600;
        
        ResolveSize(parentWidth, parentHeight);
        
        foreach (var child in _children)
            child.MeasureSize();
        
        if (Width.IsZero || Height.IsZero)
        {
            float totalWidth = 0;
            float totalHeight = 0;
            float maxWidth = 0;
            float maxHeight = 0;
            
            foreach (var child in _children)
            {
                if (child.PositionMode == PositionMode.Relative)
                {
                    var childW = child.ComputedMarginLeft + child.ComputedWidth + child.ComputedMarginRight;
                    var childH = child.ComputedMarginTop + child.ComputedHeight + child.ComputedMarginBottom;
                    
                    if (Layout == LayoutMode.Vertical)
                    {
                        totalHeight += childH;
                        if (childW > maxWidth) maxWidth = childW;
                    }
                    else if (Layout == LayoutMode.Horizontal)
                    {
                        totalWidth += childW;
                        if (childH > maxHeight) maxHeight = childH;
                    }
                }
            }
            
            if (Layout == LayoutMode.Vertical)
            {
                if (Width.IsZero) ComputedWidth = maxWidth;
                if (Height.IsZero) ComputedHeight = totalHeight;
            }
            else if (Layout == LayoutMode.Horizontal)
            {
                if (Width.IsZero) ComputedWidth = totalWidth;
                if (Height.IsZero) ComputedHeight = maxHeight;
            }
        }
    }
    
    public override void ArrangeChildren()
    {
        if (Layout == LayoutMode.Vertical)
        {
            float currentY = 0;
            
            foreach (var child in _children)
            {
                if (child.PositionMode == PositionMode.Absolute)
                {
                    child.ComputedPosition = ComputedPosition + child.Position + new Vector2(child.ComputedMarginLeft, child.ComputedMarginTop);
                }
                else
                {
                    currentY += child.ComputedMarginTop;
                    child.ComputedPosition = new Vector2(
                        ComputedPosition.X + child.ComputedMarginLeft + child.Position.X,
                        ComputedPosition.Y + currentY + child.Position.Y
                    );
                    currentY += child.ComputedHeight + child.ComputedMarginBottom;
                }
                
                child.ArrangeChildren();
            }
        }
        else if (Layout == LayoutMode.Horizontal)
        {
            float currentX = 0;
            
            foreach (var child in _children)
            {
                if (child.PositionMode == PositionMode.Absolute)
                {
                    child.ComputedPosition = ComputedPosition + child.Position + new Vector2(child.ComputedMarginLeft, child.ComputedMarginTop);
                }
                else
                {
                    currentX += child.ComputedMarginLeft;
                    child.ComputedPosition = new Vector2(
                        ComputedPosition.X + currentX + child.Position.X,
                        ComputedPosition.Y + child.ComputedMarginTop + child.Position.Y
                    );
                    currentX += child.ComputedWidth + child.ComputedMarginRight;
                }
                
                child.ArrangeChildren();
            }
        }
        else
        {
            foreach (var child in _children)
            {
                child.ComputedPosition = ComputedPosition + child.Position + new Vector2(child.ComputedMarginLeft, child.ComputedMarginTop);
                child.ArrangeChildren();
            }
        }
    }
}
