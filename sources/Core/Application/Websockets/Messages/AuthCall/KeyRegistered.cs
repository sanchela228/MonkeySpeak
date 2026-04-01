namespace Core.Websockets.Messages.AuthCall;

public class KeyRegistered : IMessage
{
    public string UserId { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
