namespace Core.Websockets.Messages.AuthCall;

public class FriendUsernameChanged : IMessage
{
    public string FriendId { get; set; } = string.Empty;
    public string NewUsername { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
