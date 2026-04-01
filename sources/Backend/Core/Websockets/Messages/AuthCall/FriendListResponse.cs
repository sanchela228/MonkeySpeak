namespace Core.Websockets.Messages.AuthCall;

public class FriendInfo
{
    public string UserId { get; set; }
    public string Username { get; set; }
    public string UserCode { get; set; }
    public bool IsOnline { get; set; }
    public string LastSeenAt { get; set; }
}

public class FriendListResponse : IMessage
{
    public List<FriendInfo> Friends { get; set; } = new();
    public string Value { get; set; }
}

public class PendingFriendListResponse : IMessage
{
    public List<FriendRequestReceived> Friends { get; set; } = new();
    public string Value { get; set; }
}
