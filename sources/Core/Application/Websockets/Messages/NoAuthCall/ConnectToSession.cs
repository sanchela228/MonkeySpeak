namespace Core.Websockets.Messages.NoAuthCall;

public class ConnectToSession : IMessage
{
    public string Code { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public string IpEndPoint { get; set; } = string.Empty;
}
