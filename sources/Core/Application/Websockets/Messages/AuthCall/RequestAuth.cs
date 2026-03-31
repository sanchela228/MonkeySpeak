namespace Core.Websockets.Messages.AuthCall;

public class RequestAuth : IMessage
{
    public string UserId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
