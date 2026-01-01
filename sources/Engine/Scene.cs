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
                (int)Width,
                (int)Height,
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
        foreach (var child in _children)
            child.MeasureSize();
        
        if (Width == 0 || Height == 0)
        {
            float totalWidth = 0;
            float totalHeight = 0;
            float maxWidth = 0;
            float maxHeight = 0;
            
            foreach (var child in _children)
            {
                if (child.PositionMode == PositionMode.Relative)
                {
                    var childW = child.MarginLeft + child.Width + child.MarginRight;
                    var childH = child.MarginTop + child.Height + child.MarginBottom;
                    
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
                if (Width == 0) Width = maxWidth;
                if (Height == 0) Height = totalHeight;
            }
            else if (Layout == LayoutMode.Horizontal)
            {
                if (Width == 0) Width = totalWidth;
                if (Height == 0) Height = maxHeight;
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
                    child.ComputedPosition = ComputedPosition + child.Position;
                }
                else
                {
                    currentY += child.MarginTop;
                    child.ComputedPosition = new Vector2(
                        ComputedPosition.X + child.MarginLeft + child.Position.X,
                        ComputedPosition.Y + currentY + child.Position.Y
                    );
                    currentY += child.Height + child.MarginBottom;
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
                    child.ComputedPosition = ComputedPosition + child.Position;
                }
                else
                {
                    currentX += child.MarginLeft;
                    child.ComputedPosition = new Vector2(
                        ComputedPosition.X + currentX + child.Position.X,
                        ComputedPosition.Y + child.MarginTop + child.Position.Y
                    );
                    currentX += child.Width + child.MarginRight;
                }
                
                child.ArrangeChildren();
            }
        }
        else
        {
            foreach (var child in _children)
            {
                child.ComputedPosition = ComputedPosition + child.Position;
                child.ArrangeChildren();
            }
        }
    }
}
