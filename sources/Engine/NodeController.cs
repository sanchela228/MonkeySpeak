namespace Engine;

public abstract class NodeController
{
    public Node Node { get; internal set; } = null!;
    
    public virtual void OnBind() { }
    public virtual void OnUpdate(float deltaTime) { }
    public virtual void OnDispose() { }
}
