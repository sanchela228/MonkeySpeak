namespace Core.Websockets.Messages.NoAuthCall;

public class InterlocutorLeft : IMessage
{
    public string Value { get; set; } = string.Empty;
    public string InterlocutorId { get; set; } = string.Empty;
}
