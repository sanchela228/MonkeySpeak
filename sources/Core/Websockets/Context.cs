using System.Text.Json;

namespace Core.Websockets;

public class Context
{
    public static string RootNamespace = "Core.Websockets";
    private const string Anchor = "Core.Websockets.";

    public string Type { get; set; } = string.Empty;
    public Guid ApplicationId { get; set; }
    public JsonElement Message { get; set; }

    public static Context Create(IMessage message) => new()
    {
        Type = GetRelativeTypeName(message.GetType()),
        Message = JsonSerializer.SerializeToElement(message, message.GetType())
    };

    public IMessage ToMessage()
    {
        var fullName = $"{RootNamespace}.{Type}";
        var resolvedType = ResolveType(fullName)
                           ?? throw new InvalidOperationException($"Type {fullName} not found");
        return (IMessage)JsonSerializer.Deserialize(Message.GetRawText(), resolvedType)!;
    }

    private static string GetRelativeTypeName(Type t)
    {
        var fullName = t.FullName ?? t.Name;
        var idx = fullName.IndexOf(Anchor, StringComparison.Ordinal);
        if (idx >= 0)
            return fullName[(idx + Anchor.Length)..];

        return t.Name;
    }

    private static Type? ResolveType(string fullName)
    {
        var t = System.Type.GetType(fullName);
        if (t != null)
            return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                if (t != null)
                    return t;
            }
            catch
            {
            }
        }

        return null;
    }
}
