namespace Core.Websockets.Messages.AuthCall;

public class FriendRequestSent : IMessage
{
    public string FriendshipId { get; set; }
    public string FriendId { get; set; }
    public string FriendUsername { get; set; }
    public string Value { get; set; }
}
