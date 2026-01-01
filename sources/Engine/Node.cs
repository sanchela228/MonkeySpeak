using System;
using System.Numerics;

namespace Engine;

public enum PositionMode
{
    Relative,
    Absolute
}

public abstract class Node
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    
    public PositionMode PositionMode { get; set; } = PositionMode.Relative;
    
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 ComputedPosition { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = Vector2.Zero;
    
    public float X
    {
        get => Position.X;
        set => Position = new Vector2(value, Position.Y);
    }
    
    public float Y
    {
        get => Position.Y;
        set => Position = new Vector2(Position.X, value);
    }
    
    public float Width
    {
        get => Size.X;
        set => Size = new Vector2(value, Size.Y);
    }
    
    public float Height
    {
        get => Size.Y;
        set => Size = new Vector2(Size.X, value);
    }
    
    public float MarginTop { get; set; } = 0;
    public float MarginBottom { get; set; } = 0;
    public float MarginLeft { get; set; } = 0;
    public float MarginRight { get; set; } = 0;
    
    public float Margin
    {
        set
        {
            MarginTop = value;
            MarginBottom = value;
            MarginLeft = value;
            MarginRight = value;
        }
    }
    
    private Node? _parent = null;
    protected List<Node> _children = new();
    private int _order = 100;
    
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<Node> Children => _children.AsReadOnly();
    
    public Node? Parent
    {
        get => _parent;
        set => _parent = value;
    }
    
    public Scene? ParentScene
    {
        get
        {
            var current = _parent;
            while (current != null)
            {
                if (current is Scene scene)
                    return scene;
                current = current._parent;
            }
            return null;
        }
    }
    
    public int Order 
    { 
        get => _order;
        set 
        { 
            _order = value;
            _parent?._children.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
    
    public void AddChild(Node child)
    {
        child._parent = this;
        _children.Add(child);
        if (_children.Count > 1)
            _children.Sort((a, b) => a.Order.CompareTo(b.Order));
    }
    
    public void RemoveChild(Node child)
    {
        child._parent = null;
        _children.Remove(child);
    }
    
    public void ClearChildren()
    {
        foreach (var child in _children)
            child._parent = null;
        _children.Clear();
    }
    
    public void RootUpdate(float deltaTime)
    {
        if (!IsActive) return;
        Update(deltaTime);
        foreach (var child in _children)
            child.RootUpdate(deltaTime);
    }
    
    public void RootDraw()
    {
        if (!IsActive) return;
        Draw();
        foreach (var child in _children)
            child.RootDraw();
    }
    
    public void RootDispose()
    {
        foreach (var child in _children.ToList())
            child.RootDispose();
        Dispose();
    }
    
    public virtual void MeasureSize()
    {
        foreach (var child in _children)
            child.MeasureSize();
        
        if (Width == 0 || Height == 0)
        {
            float totalHeight = 0;
            float maxWidth = 0;
            
            foreach (var child in _children)
            {
                if (child.PositionMode == PositionMode.Relative)
                {
                    totalHeight += child.MarginTop + child.Height + child.MarginBottom;
                    var w = child.MarginLeft + child.Width + child.MarginRight;
                    if (w > maxWidth) maxWidth = w;
                }
            }
            
            if (Width == 0) Width = maxWidth;
            if (Height == 0) Height = totalHeight;
        }
    }
    
    public virtual void ArrangeChildren()
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
    
    public void CalculateLayout()
    {
        MeasureSize();
        ArrangeChildren();
    }
    
    public abstract void Update(float deltaTime);
    public abstract void Draw();
    public abstract void Dispose();
}
