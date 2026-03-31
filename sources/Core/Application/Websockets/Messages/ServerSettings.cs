namespace Core.Websockets.Messages;

public class ServerSettings : IMessage
{
    public string AuthMode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
