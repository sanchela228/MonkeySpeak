namespace Core.Websockets.Messages.NoAuthCall;

public class ErrorConnectToSession : IMessage
{
    public string Value { get; set; } = string.Empty;
}
