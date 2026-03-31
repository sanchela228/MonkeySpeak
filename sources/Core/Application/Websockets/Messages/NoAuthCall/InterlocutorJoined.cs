namespace Core.Websockets.Messages.NoAuthCall;

public class InterlocutorJoined : IMessage
{
    public string Id { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string IpEndPoint { get; set; } = string.Empty;
}
