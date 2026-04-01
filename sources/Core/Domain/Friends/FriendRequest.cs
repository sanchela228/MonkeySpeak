namespace Core.Domain.Friends;

public class FriendRequest
{
    public string FriendshipId { get; set; } = string.Empty;
    public string FromUserId { get; set; } = string.Empty;
    public string FromUsername { get; set; } = string.Empty;
    public string FromUserCode { get; set; } = string.Empty;

    public string Handle => string.IsNullOrWhiteSpace(FromUserCode) ? FromUsername : $"{FromUsername}#{FromUserCode}";
}