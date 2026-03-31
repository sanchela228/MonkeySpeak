namespace Core.Websockets.Messages.NoAuthCall;

public class HangupSession : IMessage
{
    public string Value { get; set; } = string.Empty;
}
