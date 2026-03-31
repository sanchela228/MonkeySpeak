namespace Core.Websockets.Messages.AuthCall;

public class GetOutgoingPendingFriendList : IMessage
{
    public string Value { get; set; } = string.Empty;
}
