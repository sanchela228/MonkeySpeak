namespace Core.Websockets.Messages.NoAuthCall;

public class SuccessConnectedSession : IMessage
{
    public string Value { get; set; } = string.Empty;
}
