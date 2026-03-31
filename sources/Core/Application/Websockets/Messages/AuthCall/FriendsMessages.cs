using System.Collections.Generic;

namespace Core.Websockets.Messages.AuthCall;

public class FriendInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string LastSeenAt { get; set; } = string.Empty;
}

public class FriendListResponse : IMessage
{
    public List<FriendInfo> Friends { get; set; } = new();
    public string Value { get; set; } = string.Empty;
}

public class PendingFriendListResponse : IMessage
{
    public List<FriendRequestReceived> Friends { get; set; } = new();
    public string Value { get; set; } = string.Empty;
}

public class OutgoingFriendRequestInfo
{
    public string FriendshipId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string ToUsername { get; set; } = string.Empty;
}

public class OutgoingPendingFriendListResponse : IMessage
{
    public List<OutgoingFriendRequestInfo> Friends { get; set; } = new();
    public string Value { get; set; } = string.Empty;
}

public class GetFriendList : IMessage
{
    public string Value { get; set; } = string.Empty;
}

public class GetPendingFriendList : IMessage
{
    public string Value { get; set; } = string.Empty;
}

public class GetOutgoingPendingFriendList : IMessage
{
    public string Value { get; set; } = string.Empty;
}

public class AddFriend : IMessage
{
    public string FriendUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class AcceptFriend : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class RejectFriend : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class RemoveFriend : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendRequestSent : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendRequestReceived : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string FromUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendRequestRejected : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CancelFriendRequest : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendRequestCancelled : IMessage
{
    public string FriendshipId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendAccepted : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendOnline : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendOffline : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string FriendUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FriendRemoved : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
