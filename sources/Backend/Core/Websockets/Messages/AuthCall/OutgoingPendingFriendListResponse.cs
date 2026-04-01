using System.Collections.Generic;

namespace Core.Websockets.Messages.AuthCall;

public class OutgoingFriendRequestInfo
{
    public string FriendshipId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string ToUsername { get; set; } = string.Empty;
    public string ToUserCode { get; set; } = string.Empty;
}

public class OutgoingPendingFriendListResponse : IMessage
{
    public List<OutgoingFriendRequestInfo> Friends { get; set; } = new();
    public string Value { get; set; } = string.Empty;
}
