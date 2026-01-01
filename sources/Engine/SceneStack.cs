using System;
using System.Collections.Generic;

namespace Engine;

public class SceneStack
{
    private readonly Stack<Node> _stack = new();
    private readonly Scene _owner;
    
    public SceneStack(Scene owner)
    {
        _owner = owner;
    }
    
    public Node? Current => _stack.Count > 0 ? _stack.Peek() : null;
    public bool CanGoBack => _stack.Count > 1;
    public int Count => _stack.Count;
    
    public void Push(Node content)
    {
        if (Current != null)
            Current.IsActive = false;
        
        content.Parent = _owner;
        _owner.AddChildInternal(content);
        _stack.Push(content);
    }
    
    public void Pop()
    {
        if (_stack.Count <= 1)
            return;
        
        var current = _stack.Pop();
        current.RootDispose();
        _owner.RemoveChildInternal(current);
        
        if (Current != null)
            Current.IsActive = true;
    }
    
    public void Replace(Node content)
    {
        if (Current != null)
        {
            var current = _stack.Pop();
            current.RootDispose();
            _owner.RemoveChildInternal(current);
        }
        
        content.Parent = _owner;
        _owner.AddChildInternal(content);
        _stack.Push(content);
    }
    
    public void Clear()
    {
        while (_stack.Count > 0)
        {
            var node = _stack.Pop();
            node.RootDispose();
            _owner.RemoveChildInternal(node);
        }
    }
    
    internal void SetInitialContent(List<Node> children)
    {
        foreach (var child in children)
        {
            child.Parent = _owner;
            _owner.AddChildInternal(child);
        }
    }
}
