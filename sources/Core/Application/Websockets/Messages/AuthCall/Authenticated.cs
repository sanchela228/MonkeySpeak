namespace Core.Websockets.Messages.AuthCall;

public class Authenticated : IMessage
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
