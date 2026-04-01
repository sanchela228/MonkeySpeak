namespace Core.Websockets.Messages.AuthCall;

public class FriendRequestReceived : IMessage
{
    public string FriendshipId { get; set; }
    public string FromUserId { get; set; }
    public string FromUsername { get; set; }
    public string FromUserCode { get; set; }
    public string Value { get; set; }
}
