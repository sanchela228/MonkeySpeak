namespace Core.Websockets.Messages.AuthCall;

public class Authenticate : IMessage
{
    public string UserId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
