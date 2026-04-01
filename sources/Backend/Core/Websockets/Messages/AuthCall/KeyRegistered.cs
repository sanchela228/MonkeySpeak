namespace Core.Websockets.Messages.AuthCall;

public class KeyRegistered : IMessage
{
    public string UserId { get; set; }
    public string UserCode { get; set; }
    public string Fingerprint { get; set; }
    public string Value { get; set; }
}
