namespace Core.Domain.Friends;

public class OutgoingFriendRequest
{
    public string FriendshipId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string ToUsername { get; set; } = string.Empty;
}
