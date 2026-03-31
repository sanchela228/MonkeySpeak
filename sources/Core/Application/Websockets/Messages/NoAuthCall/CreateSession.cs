namespace Core.Websockets.Messages.NoAuthCall;

public class CreateSession : IMessage
{
    public string Value { get; set; } = string.Empty;

    public string IpEndPoint { get; set; } = string.Empty;
}
