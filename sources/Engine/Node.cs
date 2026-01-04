using System;
using System.Numerics;
using Engine.Binding;
using Engine.Types;
using Raylib_cs;

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
    
    public NodeController? Controller { get; private set; }
    
    public void SetController(NodeController controller)
    {
        Controller = controller;
        Controller.Node = this;
        Controller.OnBind();
    }
    
    public string? OnPressed { get; set; }
    public string? OnPress { get; set; }
    public string? OnRelease { get; set; }
    public string? OnHover { get; set; }
    public string? OnHoverExit { get; set; }
    
    private bool _isHovered = false;
    private bool _wasHovered = false;
    public bool IsHovered => _isHovered;
    
    public PositionMode PositionMode { get; set; } = PositionMode.Relative;
    
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 ComputedPosition { get; set; } = Vector2.Zero;
    
    public DynamicFloat Width { get; set; } = new(0);
    public DynamicFloat Height { get; set; } = new(0);
    
    public float ComputedWidth { get; protected set; }
    public float ComputedHeight { get; protected set; }
    
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
    
    public DynamicFloat MarginTop { get; set; } = new(0);
    public DynamicFloat MarginBottom { get; set; } = new(0);
    public DynamicFloat MarginLeft { get; set; } = new(0);
    public DynamicFloat MarginRight { get; set; } = new(0);
    
    public float ComputedMarginTop { get; protected set; }
    public float ComputedMarginBottom { get; protected set; }
    public float ComputedMarginLeft { get; protected set; }
    public float ComputedMarginRight { get; protected set; }
    
    public DynamicFloat Margin
    {
        set
        {
            MarginTop = value;
            MarginBottom = value;
            MarginLeft = value;
            MarginRight = value;
        }
    }
    
    public float BorderTop { get; set; } = 0;
    public float BorderBottom { get; set; } = 0;
    public float BorderLeft { get; set; } = 0;
    public float BorderRight { get; set; } = 0;
    
    public float ComputedBorderTop { get; protected set; }
    public float ComputedBorderBottom { get; protected set; }
    public float ComputedBorderLeft { get; protected set; }
    public float ComputedBorderRight { get; protected set; }

    private float _border = 0;
    public float Border
    {
        set
        {
            _border = value;
            
            BorderTop = value;
            BorderBottom = value;
            BorderLeft = value;
            BorderRight = value;
        }
        get => _border;
    }
    
    public DynamicFloat PaddingTop { get; set; } = new(0);
    public DynamicFloat PaddingBottom { get; set; } = new(0);
    public DynamicFloat PaddingLeft { get; set; } = new(0);
    public DynamicFloat PaddingRight { get; set; } = new(0);
    
    public float ComputedPaddingTop { get; protected set; }
    public float ComputedPaddingBottom { get; protected set; }
    public float ComputedPaddingLeft { get; protected set; }
    public float ComputedPaddingRight { get; protected set; }
    
    public DynamicFloat Padding
    {
        set
        {
            PaddingTop = value;
            PaddingBottom = value;
            PaddingLeft = value;
            PaddingRight = value;
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
        ProcessMouseEvents();
        Controller?.OnUpdate(deltaTime);
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
        Controller?.OnDispose();
        Dispose();
    }

    public void ResolveSize(float parentWidth, float parentHeight)
    {
        ComputedWidth = Width.Resolve(parentWidth);
        ComputedHeight = Height.Resolve(parentHeight);
        
        ComputedMarginTop = MarginTop.IsAuto ? 0 : MarginTop.Resolve(parentHeight);
        ComputedMarginBottom = MarginBottom.IsAuto ? 0 : MarginBottom.Resolve(parentHeight);
        ComputedMarginLeft = MarginLeft.IsAuto ? 0 : MarginLeft.Resolve(parentWidth);
        ComputedMarginRight = MarginRight.IsAuto ? 0 : MarginRight.Resolve(parentWidth);
        
        ComputedBorderTop = BorderTop;
        ComputedBorderBottom = BorderBottom;
        ComputedBorderLeft = BorderLeft;
        ComputedBorderRight = BorderRight;
        
        ComputedPaddingTop = PaddingTop.Resolve(ComputedHeight);
        ComputedPaddingBottom = PaddingBottom.Resolve(ComputedHeight);
        ComputedPaddingLeft = PaddingLeft.Resolve(ComputedWidth);
        ComputedPaddingRight = PaddingRight.Resolve(ComputedWidth);
    }
    
    public virtual void MeasureSize()
    {
        float parentWidth = _parent?.ComputedWidth ?? 800;
        float parentHeight = _parent?.ComputedHeight ?? 600;
        
        // Учитываем border и padding родителя для content area
        if (_parent != null)
        {
            parentWidth -= _parent.ComputedBorderLeft + _parent.ComputedBorderRight 
                         + _parent.ComputedPaddingLeft + _parent.ComputedPaddingRight;
            parentHeight -= _parent.ComputedBorderTop + _parent.ComputedBorderBottom 
                          + _parent.ComputedPaddingTop + _parent.ComputedPaddingBottom;
        }
        
        ResolveSize(parentWidth, parentHeight);
        
        foreach (var child in _children)
            child.MeasureSize();
        
        if (Width.IsAuto || Height.IsAuto)
        {
            float totalHeight = 0;
            float maxWidth = 0;
            
            foreach (var child in _children)
            {
                if (child.PositionMode == PositionMode.Relative)
                {
                    totalHeight += child.ComputedMarginTop + child.ComputedHeight + child.ComputedMarginBottom;
                    var w = child.ComputedMarginLeft + child.ComputedWidth + child.ComputedMarginRight;
                    if (w > maxWidth) maxWidth = w;
                }
            }
            
            if (Width.IsAuto) ComputedWidth = maxWidth;
            if (Height.IsAuto) ComputedHeight = totalHeight;
        }
    }
    
    public virtual void ArrangeChildren()
    {
        float offsetX = ComputedBorderLeft + ComputedPaddingLeft;
        float offsetY = ComputedBorderTop + ComputedPaddingTop;
        float currentY = offsetY;
        float contentWidth = ComputedWidth - ComputedBorderLeft - ComputedBorderRight - ComputedPaddingLeft - ComputedPaddingRight;
        float contentHeight = ComputedHeight - ComputedBorderTop - ComputedBorderBottom - ComputedPaddingTop - ComputedPaddingBottom;
        
        foreach (var child in _children)
        {
            ResolveAutoMargins(child, contentWidth, contentHeight);
            
            if (child.PositionMode == PositionMode.Absolute)
            {
                child.ComputedPosition = ComputedPosition + child.Position 
                    + new Vector2(offsetX + child.ComputedMarginLeft, offsetY + child.ComputedMarginTop);
            }
            else
            {
                currentY += child.ComputedMarginTop;
                child.ComputedPosition = new Vector2(
                    ComputedPosition.X + offsetX + child.ComputedMarginLeft + child.Position.X,
                    ComputedPosition.Y + currentY + child.Position.Y
                );
                currentY += child.ComputedHeight + child.ComputedMarginBottom;
            }
            
            child.ArrangeChildren();
        }
    }
    
    protected void ResolveAutoMargins(Node child, float contentWidth, float contentHeight)
    {
        if (child.MarginLeft.IsAuto && child.MarginRight.IsAuto)
        {
            float freeSpace = contentWidth - child.ComputedWidth;
            child.ComputedMarginLeft = freeSpace / 2;
            child.ComputedMarginRight = freeSpace / 2;
        }
        else if (child.MarginLeft.IsAuto)
        {
            child.ComputedMarginLeft = contentWidth - child.ComputedWidth - child.ComputedMarginRight;
        }
        else if (child.MarginRight.IsAuto)
        {
            child.ComputedMarginRight = contentWidth - child.ComputedWidth - child.ComputedMarginLeft;
        }
        
        if (child.MarginTop.IsAuto && child.MarginBottom.IsAuto)
        {
            float freeSpace = contentHeight - child.ComputedHeight;
            child.ComputedMarginTop = freeSpace / 2;
            child.ComputedMarginBottom = freeSpace / 2;
        }
        else if (child.MarginTop.IsAuto)
        {
            child.ComputedMarginTop = contentHeight - child.ComputedHeight - child.ComputedMarginBottom;
        }
        else if (child.MarginBottom.IsAuto)
        {
            child.ComputedMarginBottom = contentHeight - child.ComputedHeight - child.ComputedMarginTop;
        }
    }
    
    public void CalculateLayout()
    {
        MeasureSize();
        ArrangeChildren();
    }
    
    protected void ProcessMouseEvents()
    {
        var mousePos = Raylib.GetMousePosition();
        
        _wasHovered = _isHovered;
        _isHovered = mousePos.X >= ComputedPosition.X 
                  && mousePos.X <= ComputedPosition.X + ComputedWidth
                  && mousePos.Y >= ComputedPosition.Y 
                  && mousePos.Y <= ComputedPosition.Y + ComputedHeight;
        
        // OnHover
        if (_isHovered && !_wasHovered && OnHover != null)
            BindingResolver.Execute(OnHover, this);
        
        // OnHoverExit
        if (!_isHovered && _wasHovered && OnHoverExit != null)
            BindingResolver.Execute(OnHoverExit, this);
        
        if (_isHovered)
        {
            // OnPressed
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && OnPressed != null)
                BindingResolver.Execute(OnPressed, this);
            
            // OnPress
            if (Raylib.IsMouseButtonDown(MouseButton.Left) && OnPress != null)
                BindingResolver.Execute(OnPress, this);
            
            // OnRelease
            if (Raylib.IsMouseButtonReleased(MouseButton.Left) && OnRelease != null)
                BindingResolver.Execute(OnRelease, this);
        }
    }
    
    public abstract void Update(float deltaTime);
    public abstract void Draw();
    public abstract void Dispose();
}
