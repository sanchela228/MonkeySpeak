namespace Core.Websockets.Messages.AuthCall;

public class InviteToCall : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CallInviteSent : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CallInviteResponse : IMessage
{
    public string RoomCode { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string FromUsername { get; set; } = string.Empty;
    public bool Accepted { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CancelCallInvite : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CallInviteCancelled : IMessage
{
    public string RoomCode { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string FromUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
